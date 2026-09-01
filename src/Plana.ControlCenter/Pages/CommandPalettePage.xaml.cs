using System.Collections.ObjectModel;
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Plana.Core.Actions;
using Plana.Core.Commands;
using Plana_ControlCenter.Services;

namespace Plana_ControlCenter.Pages;

public sealed partial class CommandPalettePage : Page
{
    private readonly Dictionary<string, (ActionDefinition Definition, string? WorkingDirectory)> _actions = new(StringComparer.OrdinalIgnoreCase);
    private CommandCatalog _catalog = new([]);
    public ObservableCollection<CommandRow> VisibleCommands { get; } = [];

    public CommandPalettePage()
    {
        InitializeComponent();
        Loaded += CommandPalettePage_Loaded;
    }

    private async void CommandPalettePage_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyLanguage();
        await LoadCatalogAsync();
        SearchBox.Focus(FocusState.Programmatic);
    }

    private async Task LoadCatalogAsync()
    {
        _actions.Clear();
        var commands = new List<CommandDescriptor>
        {
            Navigation("builtin.navigate.chat", App.IsChinese ? "和普拉娜对话" : "Chat with Plana", App.IsChinese ? "使用已配置的 AI 服务" : "Use the configured AI provider", "\uE8BD", "chat", ["ai", "ask", "对话"]),
            Navigation("builtin.navigate.settings", App.IsChinese ? "设置" : "Settings", App.IsChinese ? "桌宠、交互、语言和 AI" : "Companion, interactions, language, and AI", "\uE713", "settings", ["preferences", "配置"]),
            Navigation("builtin.navigate.actions", App.IsChinese ? "管理操作" : "Manage Actions", App.IsChinese ? "创建、编辑和运行操作" : "Create, edit, and run Actions", "\uE80F", "actions", ["commands", "快捷方式"]),
            Navigation("builtin.navigate.groups", App.IsChinese ? "工具组" : "Tool groups", App.IsChinese ? "管理收藏和桌宠快捷工具" : "Manage collections and Companion quick tools", "\uE8FD", "groups", ["collections", "favorites", "收藏"]),
            Navigation("builtin.navigate.extensions", App.IsChinese ? "扩展" : "Extensions", App.IsChinese ? "管理 Action Packs 和 Plugins" : "Manage Action Packs and Plugins", "\uEA3A", "extensions", ["plugins", "packs", "插件"]),
        };

        foreach (var action in App.Settings.UserActions)
            AddAction(commands, ToDefinition(action), action.Name, string.IsNullOrWhiteSpace(action.Description) ? DescribeKind(action.Kind) : action.Description, App.IsChinese ? "我的操作" : "Your Actions", null);
        foreach (var project in App.Settings.ProjectLaunchers)
            AddAction(commands, ToDefinition(project), project.Name, project.Folder, App.IsChinese ? "项目" : "Projects", project.Folder);

        var packs = await new ActionPackLoader().LoadDirectoryAsync(Path.Combine(App.DataDirectory, "packs"));
        foreach (var pack in packs.ValidPacks.Where(pack => !App.Settings.DisabledActionPacks.Contains(pack.Id)))
            foreach (var action in pack.Actions)
                AddAction(commands, action, action.Label, DescribeKind(action.Kind), pack.Name, pack.SourceDirectory);

        foreach (var group in App.Settings.ToolGroups)
            commands.Add(new CommandDescriptor($"tool-group.{group.Id}", group.Name,
                App.IsChinese ? $"{group.ActionIds.Count} 个操作" : $"{group.ActionIds.Count} Actions",
                "\uE8FD", App.IsChinese ? "工具组" : "Tool groups", [], new CommandTarget(CommandTargetKinds.ToolGroup, group.Id)));

        _catalog = new CommandCatalog(commands);
        Filter(SearchBox.Text);
    }

    private void AddAction(List<CommandDescriptor> commands, ActionDefinition definition, string title, string subtitle, string source, string? workingDirectory)
    {
        _actions[definition.Id] = (definition, workingDirectory);
        commands.Add(new CommandDescriptor(definition.Id, title, subtitle, GlyphFor(definition.Kind), source,
            [definition.Kind], new CommandTarget(CommandTargetKinds.Action, definition.Id)));
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args) => Filter(sender.Text);
    private async void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (VisibleCommands.Count > 0) await ExecuteAsync(VisibleCommands[0]);
    }

    private void Filter(string? query)
    {
        VisibleCommands.Clear();
        foreach (var command in _catalog.Search(query)) VisibleCommands.Add(new CommandRow(command));
        EmptyState.Visibility = VisibleCommands.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (VisibleCommands.Count > 0) CommandList.SelectedIndex = 0;
    }

    private async void CommandList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is CommandRow row) await ExecuteAsync(row);
    }

    private async Task ExecuteAsync(CommandRow row)
    {
        var target = row.Descriptor.Target;
        if (target.Kind == CommandTargetKinds.Action && _actions.TryGetValue(target.Value, out var action))
        {
            var result = await ActionExecutionService.ExecuteAsync(action.Definition, action.WorkingDirectory);
            CommandStatus.Title = row.Title;
            CommandStatus.Message = result.Message;
            CommandStatus.Severity = result.Succeeded ? InfoBarSeverity.Success : InfoBarSeverity.Error;
            CommandStatus.IsOpen = true;
            return;
        }
        var route = target.Kind == CommandTargetKinds.ToolGroup ? "groups" : target.Value;
        Process.Start(new ProcessStartInfo($"plana://{route}") { UseShellExecute = true });
    }

    private static CommandDescriptor Navigation(string id, string title, string subtitle, string glyph, string route, IReadOnlyList<string> aliases) =>
        new(id, title, subtitle, glyph, "Plana Desktop", aliases, new CommandTarget(CommandTargetKinds.Navigate, route));

    private void ApplyLanguage()
    {
        if (!App.IsChinese) return;
        PageTitle.Text = "快速启动";
        PageDescription.Text = "在一个入口搜索操作、工具、设置和对话。";
        SearchBox.PlaceholderText = "输入命令名称或别名";
        EmptyTitle.Text = "没有匹配的命令";
        EmptyDescription.Text = "尝试名称、别名、来源或描述。";
    }

    private static ActionDefinition ToDefinition(Plana.Core.Settings.UserActionSettings action) => new(
        $"user.action.{action.Id}", action.Name, action.Kind, action.Parameters, CapabilityFor(action.Kind), action.RequiresConfirmation);

    private static ActionDefinition ToDefinition(Plana.Core.Settings.ProjectLauncherSettings project)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["executable"] = project.Executable };
        for (var index = 0; index < project.Arguments.Count; index++) parameters[$"arg.{index}"] = project.Arguments[index].Replace("{folder}", project.Folder, StringComparison.OrdinalIgnoreCase);
        return new ActionDefinition($"user.launcher.{project.Id}", project.Name, ActionKinds.LaunchProcess, parameters, CapabilityFor(ActionKinds.LaunchProcess));
    }

    private static string DescribeKind(string kind) => kind switch
    {
        ActionKinds.OpenFile => App.IsChinese ? "打开文件" : "Open file",
        ActionKinds.OpenFolder => App.IsChinese ? "打开文件夹" : "Open folder",
        ActionKinds.OpenUrl => App.IsChinese ? "打开网页" : "Open website",
        ActionKinds.RunScript => App.IsChinese ? "运行脚本" : "Run script",
        ActionKinds.RunCommand => App.IsChinese ? "运行命令" : "Run command",
        ActionKinds.LaunchProcess => App.IsChinese ? "启动程序" : "Launch program",
        ActionKinds.PluginInvoke => App.IsChinese ? "插件命令" : "Plugin command",
        _ => kind,
    };

    private static string GlyphFor(string kind) => kind switch
    {
        ActionKinds.OpenFile => "\uE8A5", ActionKinds.OpenFolder => "\uE8B7", ActionKinds.OpenUrl => "\uE774",
        ActionKinds.RunScript => "\uE943", ActionKinds.LaunchProcess => "\uE756", ActionKinds.PluginInvoke => "\uEA3A",
        _ => "\uE945",
    };

    private static HashSet<string> CapabilityFor(string kind) =>
        [kind switch
        {
            ActionKinds.OpenUrl => Capabilities.OpenUrl, ActionKinds.OpenFile => Capabilities.OpenFile,
            ActionKinds.OpenFolder => Capabilities.OpenFolder, ActionKinds.RunCommand => Capabilities.RunCommand,
            ActionKinds.RunScript => Capabilities.RunScript, _ => Capabilities.LaunchProcess,
        }];
}

public sealed class CommandRow(CommandDescriptor descriptor)
{
    public CommandDescriptor Descriptor { get; } = descriptor;
    public string Title => Descriptor.Title;
    public string Subtitle => Descriptor.Subtitle;
    public string Source => Descriptor.Source;
    public string Glyph => Descriptor.IconGlyph;
}
