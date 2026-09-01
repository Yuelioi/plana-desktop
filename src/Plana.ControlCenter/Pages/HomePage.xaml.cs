using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Plana.Core.Actions;
using Plana.Core.Settings;
using Plana_ControlCenter.Services;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Plana_ControlCenter.Pages;

public sealed partial class HomePage : Page
{
    private readonly List<ActionListItem> _allActions = [];
    private UserActionSettings? _editingUserAction;
    public ObservableCollection<ActionListItem> VisibleActions { get; } = [];

    public HomePage()
    {
        InitializeComponent();
        Loaded += HomePage_Loaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is string query && !string.IsNullOrWhiteSpace(query)) SearchBox.Text = query;
    }

    private async void HomePage_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyLanguage();
        await LoadActionsAsync();
    }

    private async Task LoadActionsAsync()
    {
        _allActions.Clear();
        _allActions.AddRange(App.Settings.UserActions.Select(action => new ActionListItem(
            $"user.action.{action.Id}", action.Name,
            string.IsNullOrWhiteSpace(action.Description) ? DescribeKind(action.Kind) : action.Description,
            GlyphFor(action.Kind), action.Kind, ToDefinition(action), null) { UserAction = action, IsPinned = App.Settings.PinnedCompanionActionIds.Contains($"user.action.{action.Id}", StringComparer.OrdinalIgnoreCase) }));
        _allActions.AddRange(App.Settings.ProjectLaunchers.Select(project => new ActionListItem(
            $"user.launcher.{project.Id}", project.Name, project.Folder, "\uE756", ActionKinds.LaunchProcess,
            ToDefinition(project), project.Folder) { IsPinned = App.Settings.PinnedCompanionActionIds.Contains($"user.launcher.{project.Id}", StringComparer.OrdinalIgnoreCase) }));

        var packs = await new ActionPackLoader().LoadDirectoryAsync(Path.Combine(App.DataDirectory, "packs"));
        foreach (var pack in packs.ValidPacks.Where(pack => !App.Settings.DisabledActionPacks.Contains(pack.Id)))
        {
            _allActions.AddRange(pack.Actions.Select(action => new ActionListItem(
                action.Id, action.Label, $"{pack.Name} · {DescribeKind(action.Kind)}", GlyphFor(action.Kind), action.Kind,
                action, pack.SourceDirectory) { IsPinned = App.Settings.PinnedCompanionActionIds.Contains(action.Id, StringComparer.OrdinalIgnoreCase) }));
        }
        FilterActions(SearchBox.Text);
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs args) => FilterActions(SearchBox.Text);
    private async void SearchBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs args)
    {
        if (args.Key == Windows.System.VirtualKey.Enter && VisibleActions.Count == 1)
        {
            args.Handled = true;
            await ExecuteActionAsync(VisibleActions[0]);
        }
    }

    private void FilterActions(string? query)
    {
        VisibleActions.Clear();
        var needle = query?.Trim() ?? string.Empty;
        foreach (var action in _allActions.Where(action => needle.Length == 0 ||
                     action.Name.Contains(needle, StringComparison.CurrentCultureIgnoreCase) ||
                     action.Detail.Contains(needle, StringComparison.CurrentCultureIgnoreCase) ||
                     action.Kind.Contains(needle, StringComparison.OrdinalIgnoreCase)))
        {
            VisibleActions.Add(action);
        }
        EmptyState.Visibility = VisibleActions.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void ActionList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ActionListItem action) await ExecuteActionAsync(action);
    }

    private async void RunActionButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is ActionListItem action) await ExecuteActionAsync(action);
    }

    private async void PinActionButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not ActionListItem action) return;
        var pinned = App.Settings.PinnedCompanionActionIds;
        if (pinned.RemoveAll(id => id.Equals(action.Id, StringComparison.OrdinalIgnoreCase)) == 0)
        {
            if (pinned.Count >= 4)
            {
                ActionStatus.Title = App.IsChinese ? "桌宠快捷动作已满" : "Companion shortcuts are full";
                ActionStatus.Message = App.IsChinese ? "最多固定 4 个动作，请先取消一个。" : "You can pin up to four actions. Unpin one first.";
                ActionStatus.Severity = InfoBarSeverity.Warning;
                ActionStatus.IsOpen = true;
                return;
            }
            pinned.Add(action.Id);
        }
        await App.SettingsStore.SaveAsync(App.Settings);
        await LoadActionsAsync();
    }

    private async Task ExecuteActionAsync(ActionListItem item)
    {
        var result = await ActionExecutionService.ExecuteAsync(item.Definition, item.WorkingDirectory);
        ActionStatus.Title = item.Name;
        ActionStatus.Message = result.Message;
        ActionStatus.Severity = result.Succeeded ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        ActionStatus.IsOpen = true;
    }

    private async void AddActionButton_Click(object sender, RoutedEventArgs e)
    {
        _editingUserAction = null;
        ApplyActionEditorLanguage();
        ActionNameInput.Text = string.Empty;
        ActionDescriptionInput.Text = string.Empty;
        ActionKindInput.SelectedIndex = 0;
        ActionTargetInput.Text = string.Empty;
        ActionScriptInput.Text = string.Empty;
        ActionArgumentsInput.Text = string.Empty;
        ActionEditorError.Text = string.Empty;
        ActionEditorDialog.Title = App.IsChinese ? "新建动作" : "New action";
        ActionEditorDialog.PrimaryButtonText = App.IsChinese ? "添加" : "Add action";
        ActionEditorDialog.CloseButtonText = App.IsChinese ? "取消" : "Cancel";
        try
        {
            ActionEditorDialog.XamlRoot = XamlRoot;
            await ActionEditorDialog.ShowAsync();
        }
        catch (Exception exception)
        {
            ActionStatus.Title = App.IsChinese ? "无法打开编辑器" : "Could not open editor";
            ActionStatus.Message = exception.Message;
            ActionStatus.Severity = InfoBarSeverity.Error;
            ActionStatus.IsOpen = true;
        }
    }

    private async void EditActionButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not ActionListItem { UserAction: { } action }) return;
        _editingUserAction = action;
        ApplyActionEditorLanguage();
        ActionNameInput.Text = action.Name;
        ActionDescriptionInput.Text = action.Description;
        ActionKindInput.SelectedItem = ActionKindInput.Items.OfType<ComboBoxItem>().FirstOrDefault(item => item.Tag?.ToString() == action.Kind);
        ActionTargetInput.Text = action.Kind switch
        {
            ActionKinds.OpenUrl => action.Parameters.GetValueOrDefault("url", string.Empty),
            ActionKinds.OpenFile or ActionKinds.OpenFolder => action.Parameters.GetValueOrDefault("path", string.Empty),
            ActionKinds.RunCommand => action.Parameters.GetValueOrDefault("command", string.Empty),
            ActionKinds.RunScript => action.Parameters.GetValueOrDefault("interpreter", string.Empty),
            _ => action.Parameters.GetValueOrDefault("executable", string.Empty),
        };
        ActionScriptInput.Text = action.Parameters.GetValueOrDefault("script", string.Empty);
        ActionArgumentsInput.Text = string.Join(Environment.NewLine, action.Parameters
            .Where(pair => pair.Key.StartsWith("arg.", StringComparison.OrdinalIgnoreCase))
            .OrderBy(pair => pair.Key)
            .Select(pair => pair.Value));
        ActionEditorError.Text = string.Empty;
        ActionEditorDialog.Title = App.IsChinese ? "编辑动作" : "Edit action";
        ActionEditorDialog.PrimaryButtonText = App.IsChinese ? "保存" : "Save";
        ActionEditorDialog.CloseButtonText = App.IsChinese ? "取消" : "Cancel";
        ActionEditorDialog.XamlRoot = XamlRoot;
        await ActionEditorDialog.ShowAsync();
    }

    private async void DeleteActionButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not ActionListItem { UserAction: { } action } item) return;
        App.Settings.UserActions.Remove(action);
        App.Settings.PinnedCompanionActionIds.RemoveAll(id => id.Equals(item.Id, StringComparison.OrdinalIgnoreCase));
        foreach (var group in App.Settings.ToolGroups) group.ActionIds.RemoveAll(id => id.Equals(item.Id, StringComparison.OrdinalIgnoreCase));
        foreach (var interaction in App.Settings.InteractionBindings.Where(pair => pair.Value.Equals(item.Id, StringComparison.OrdinalIgnoreCase)).Select(pair => pair.Key).ToArray())
            App.Settings.InteractionBindings.Remove(interaction);
        await App.SettingsStore.SaveAsync(App.Settings);
        await LoadActionsAsync();
        ActionStatus.Title = action.Name;
        ActionStatus.Message = App.IsChinese ? "动作已删除。" : "Action deleted.";
        ActionStatus.Severity = InfoBarSeverity.Success;
        ActionStatus.IsOpen = true;
    }

    private void ActionKindInput_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var kind = (ActionKindInput.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        ActionScriptInput.Visibility = kind == ActionKinds.RunScript ? Visibility.Visible : Visibility.Collapsed;
        if (ActionTargetInput is null) return;
        ActionTargetInput.Header = kind switch
        {
            ActionKinds.OpenUrl => App.IsChinese ? "网址" : "Website URL",
            ActionKinds.OpenFile => App.IsChinese ? "文件路径" : "File path",
            ActionKinds.OpenFolder => App.IsChinese ? "文件夹路径" : "Folder path",
            ActionKinds.RunCommand => App.IsChinese ? "命令" : "Command",
            ActionKinds.RunScript => App.IsChinese ? "解释器" : "Interpreter",
            _ => App.IsChinese ? "程序" : "Executable",
        };
    }

    private async void ActionEditorDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            var kind = (ActionKindInput.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            var name = ActionNameInput.Text.Trim();
            var description = ActionDescriptionInput.Text.Trim();
            var target = ActionTargetInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(target))
            {
                ActionEditorError.Text = App.IsChinese ? "名称、类型和目标不能为空。" : "Name, type, and target are required.";
                args.Cancel = true;
                return;
            }

            var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var key = kind switch
            {
                ActionKinds.OpenUrl => "url",
                ActionKinds.OpenFile or ActionKinds.OpenFolder => "path",
                ActionKinds.RunCommand => "command",
                ActionKinds.RunScript => "interpreter",
                _ => "executable",
            };
            parameters[key] = target;
            if (kind == ActionKinds.RunScript)
            {
                var script = ActionScriptInput.Text.Trim();
                if (string.IsNullOrWhiteSpace(script))
                {
                    ActionEditorError.Text = App.IsChinese ? "请选择脚本文件。" : "A script file is required.";
                    args.Cancel = true;
                    return;
                }
                parameters["script"] = script;
            }
            var arguments = ActionArgumentsInput.Text.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            for (var index = 0; index < arguments.Length; index++) parameters[$"arg.{index}"] = arguments[index];

            if (_editingUserAction is null)
            {
                _editingUserAction = new UserActionSettings();
                App.Settings.UserActions.Add(_editingUserAction);
            }
            _editingUserAction.Name = name;
            _editingUserAction.Description = description;
            _editingUserAction.Kind = kind;
            _editingUserAction.Parameters = parameters;
            await App.SettingsStore.SaveAsync(App.Settings);
            await LoadActionsAsync();
            ActionStatus.Title = name;
            ActionStatus.Message = App.IsChinese ? "动作已保存，可以立即搜索和运行。" : "Action saved. It is ready to search and run.";
            ActionStatus.Severity = InfoBarSeverity.Success;
            ActionStatus.IsOpen = true;
        }
        finally
        {
            deferral.Complete();
        }
    }

    private void ApplyLanguage()
    {
        if (!App.IsChinese) return;
        PageTitle.Text = "动作";
        SearchBox.PlaceholderText = "搜索动作";
        AddActionLabel.Text = "新建动作";
        NameColumnHeader.Text = "名称";
        DescriptionColumnHeader.Text = "描述";
        TypeColumnHeader.Text = "类型";
        EmptyTitle.Text = "没有匹配的动作";
        EmptyDescription.Text = "尝试其他关键词，或新建一个动作。";
    }

    private void ApplyActionEditorLanguage()
    {
        if (!App.IsChinese) return;
        ActionNameInput.Header = "名称";
        ActionNameInput.PlaceholderText = "例如：打开 Codex 项目";
        ActionDescriptionInput.Header = "描述";
        ActionDescriptionInput.PlaceholderText = "说明这个动作会做什么";
        ActionKindInput.Header = "类型";
        ActionTargetInput.PlaceholderText = "输入路径、网址、程序或命令";
        ActionScriptInput.Header = "脚本文件";
        ActionScriptInput.PlaceholderText = @"C:\项目\工具.ps1";
        ActionArgumentsInput.Header = "参数";
        ActionArgumentsInput.PlaceholderText = "每行一个参数";
        foreach (var item in ActionKindInput.Items.OfType<ComboBoxItem>())
        {
            item.Content = item.Tag?.ToString() switch
            {
                ActionKinds.OpenUrl => "打开网页",
                ActionKinds.OpenFile => "打开文件",
                ActionKinds.OpenFolder => "打开文件夹",
                ActionKinds.LaunchProcess => "启动程序",
                ActionKinds.RunCommand => "运行命令",
                ActionKinds.RunScript => "运行脚本",
                _ => item.Content,
            };
        }
    }

    private static string DescribeKind(string kind) => kind switch
    {
        ActionKinds.OpenFile => App.IsChinese ? "打开文件" : "Open file",
        ActionKinds.OpenFolder => App.IsChinese ? "打开文件夹" : "Open folder",
        ActionKinds.OpenUrl => App.IsChinese ? "打开网页" : "Open website",
        ActionKinds.RunScript => App.IsChinese ? "运行脚本" : "Run script",
        ActionKinds.LaunchProcess => App.IsChinese ? "启动程序" : "Launch program",
        _ => kind,
    };

    private static string GlyphFor(string kind) => kind switch
    {
        ActionKinds.OpenFile => "\uE8A5",
        ActionKinds.OpenFolder => "\uE8B7",
        ActionKinds.OpenUrl => "\uE774",
        ActionKinds.RunScript => "\uE943",
        ActionKinds.LaunchProcess => "\uE756",
        _ => "\uE945",
    };

    private static ActionDefinition ToDefinition(UserActionSettings action) => new(
        $"user.action.{action.Id}", action.Name, action.Kind, action.Parameters, CapabilityFor(action.Kind), action.RequiresConfirmation);

    private static ActionDefinition ToDefinition(ProjectLauncherSettings project)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["executable"] = project.Executable };
        for (var index = 0; index < project.Arguments.Count; index++)
        {
            parameters[$"arg.{index}"] = project.Arguments[index].Replace("{folder}", project.Folder, StringComparison.OrdinalIgnoreCase);
        }
        return new ActionDefinition($"user.launcher.{project.Id}", project.Name, ActionKinds.LaunchProcess, parameters, CapabilityFor(ActionKinds.LaunchProcess));
    }

    private static HashSet<string> CapabilityFor(string kind) =>
        [kind switch
        {
            ActionKinds.OpenUrl => Capabilities.OpenUrl,
            ActionKinds.OpenFile => Capabilities.OpenFile,
            ActionKinds.OpenFolder => Capabilities.OpenFolder,
            ActionKinds.RunCommand => Capabilities.RunCommand,
            ActionKinds.RunScript => Capabilities.RunScript,
            _ => Capabilities.LaunchProcess,
        }];
}

public sealed class ActionListItem(string id, string name, string detail, string glyph, string kind)
{
    public string Id { get; set; } = id;
    public string Name { get; set; } = name;
    public string Detail { get; set; } = detail;
    public string Description => Detail;
    public string TypeLabel => Kind switch
    {
        ActionKinds.OpenFile => App.IsChinese ? "打开文件" : "Open file",
        ActionKinds.OpenFolder => App.IsChinese ? "打开文件夹" : "Open folder",
        ActionKinds.OpenUrl => App.IsChinese ? "打开网页" : "Open website",
        ActionKinds.RunScript => App.IsChinese ? "运行脚本" : "Run script",
        ActionKinds.RunCommand => App.IsChinese ? "运行命令" : "Run command",
        ActionKinds.LaunchProcess => App.IsChinese ? "启动程序" : "Launch program",
        ActionKinds.PluginInvoke => App.IsChinese ? "插件" : "Plugin",
        _ => Kind,
    };
    public string Glyph { get; set; } = glyph;
    public string Kind { get; set; } = kind;
    public ActionDefinition Definition { get; set; } = null!;
    public string? WorkingDirectory { get; set; }
    public UserActionSettings? UserAction { get; set; }
    public bool CanManage => UserAction is not null;
    public bool IsPinned { get; set; }
    public Symbol PinSymbol => IsPinned ? Symbol.SolidStar : Symbol.OutlineStar;
    public string PinLabel => IsPinned ? (App.IsChinese ? "从桌宠快捷位移除" : "Remove from Companion") : (App.IsChinese ? "固定到桌宠（最多 4 个）" : "Pin to Companion (up to 4)");

    public ActionListItem(string id, string name, string detail, string glyph, string kind, ActionDefinition definition, string? workingDirectory)
        : this(id, name, detail, glyph, kind)
    {
        Definition = definition;
        WorkingDirectory = workingDirectory;
    }
}
