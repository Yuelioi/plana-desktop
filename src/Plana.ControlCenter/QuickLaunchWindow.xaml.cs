using System.Collections.ObjectModel;
using System.Diagnostics;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Plana.Core.Actions;
using Plana.Core.Commands;
using Plana_ControlCenter.Services;
using Windows.Graphics;
using WinRT.Interop;

namespace Plana_ControlCenter;

public sealed partial class QuickLaunchWindow : Window
{
    private readonly Dictionary<string, (ActionDefinition Definition, string? WorkingDirectory)> _actions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ObservableCollection<QuickLaunchRow> _suggestions = [];
    private CommandCatalog _catalog = new([]);
    private readonly Dictionary<string, IReadOnlyList<string>> _groupActions = new(StringComparer.OrdinalIgnoreCase);

    public QuickLaunchWindow()
    {
        InitializeComponent();
        SearchBox.ItemsSource = _suggestions;
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(LauncherSurface);
        AppWindow.SetIcon("Assets/AppIcon.ico");
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
        }
        AppWindow.Resize(new SizeInt32(680, 132));
        AppWindow.Closing += (_, args) => { args.Cancel = true; AppWindow.Hide(); };
        BuildGroups();
    }

    public async Task ShowAsync(string? query = null)
    {
        await LoadCatalogAsync();
        CenterOnDisplay();
        Activate();
        SearchBox.Text = query ?? string.Empty;
        Filter(SearchBox.Text);
        SearchBox.Focus(FocusState.Programmatic);
    }

    private void BuildGroups()
    {
        if (!App.IsChinese) SearchBox.PlaceholderText = "Search actions";
    }

    private async Task LoadCatalogAsync()
    {
        _actions.Clear();
        var commands = new List<CommandDescriptor>();
        foreach (var action in App.Settings.UserActions) AddAction(commands, ToDefinition(action), action.Name, string.IsNullOrWhiteSpace(action.Description) ? DescribeKind(action.Kind) : action.Description, App.IsChinese ? "我的动作" : "My actions", null, "actions");
        foreach (var project in App.Settings.ProjectLaunchers) AddAction(commands, ToDefinition(project), project.Name, project.Folder, App.IsChinese ? "项目" : "Projects", project.Folder, "projects");
        var packs = await new ActionPackLoader().LoadDirectoryAsync(Path.Combine(App.DataDirectory, "packs"));
        foreach (var pack in packs.ValidPacks.Where(pack => !App.Settings.DisabledActionPacks.Contains(pack.Id)))
            foreach (var action in pack.Actions) AddAction(commands, action, action.Label, DescribeKind(action.Kind), pack.Name, pack.SourceDirectory, "extensions");
        _groupActions.Clear();
        GroupBar.Children.Clear();
        foreach (var group in App.Settings.ToolGroups)
        {
            _groupActions[group.Id] = group.ActionIds;
            var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            content.Children.Add(new TextBlock { Text = group.Name, VerticalAlignment = VerticalAlignment.Center });
            content.Children.Add(new SymbolIcon { Symbol = Symbol.More, VerticalAlignment = VerticalAlignment.Center });
            var button = new Button { Tag = group.Id, Content = content, MinHeight = 30, Padding = new Thickness(13, 4, 11, 4), CornerRadius = new CornerRadius(15) };
            button.Click += Group_Click;
            GroupBar.Children.Add(button);
        }
        _catalog = new CommandCatalog(commands);
        Filter(SearchBox.Text);
    }

    private void AddAction(List<CommandDescriptor> commands, ActionDefinition definition, string title, string subtitle, string source, string? workingDirectory, string group)
    {
        _actions[definition.Id] = (definition, workingDirectory);
        commands.Add(new CommandDescriptor(definition.Id, title, subtitle, GlyphFor(definition.Kind), $"group:{group}|{source}", [definition.Kind], new CommandTarget(CommandTargetKinds.Action, definition.Id)));
    }

    private void Group_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button selected || selected.Tag is not string groupId || !_groupActions.TryGetValue(groupId, out var actionIds)) return;
        var ids = actionIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rows = _catalog.Search(null).Where(item => ids.Contains(item.Id)).Select(item => new QuickLaunchRow(item)).ToArray();
        var menu = new MenuFlyout();
        if (rows.Length == 0)
        {
            menu.Items.Add(new MenuFlyoutItem { Text = App.IsChinese ? "这个动作组是空的" : "This action group is empty", IsEnabled = false });
        }
        foreach (var row in rows)
        {
            var item = new MenuFlyoutItem { Text = row.Title, Icon = new SymbolIcon { Symbol = row.Icon } };
            item.Click += async (_, _) => await ExecuteAsync(row);
            menu.Items.Add(item);
        }
        menu.ShowAt(selected);
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput) Filter(sender.Text);
    }
    private void SearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args) { if (args.SelectedItem is QuickLaunchRow row) sender.Text = row.Title; }
    private async void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        var row = args.ChosenSuggestion as QuickLaunchRow ?? _suggestions.FirstOrDefault();
        if (row is not null) await ExecuteAsync(row);
    }

    private void Filter(string? query)
    {
        _suggestions.Clear();
        foreach (var descriptor in _catalog.Search(query).Take(12)) _suggestions.Add(new QuickLaunchRow(descriptor));
        SearchBox.IsSuggestionListOpen = _suggestions.Count > 0 && !string.IsNullOrWhiteSpace(query);
    }

    private async Task ExecuteAsync(QuickLaunchRow row)
    {
        var target = row.Descriptor.Target;
        if (target.Kind == CommandTargetKinds.Action && _actions.TryGetValue(target.Value, out var action))
        {
            var result = await ActionExecutionService.ExecuteAsync(action.Definition, action.WorkingDirectory);
            if (result.Succeeded) AppWindow.Hide();
            else SearchBox.PlaceholderText = result.Message;
            return;
        }
        AppWindow.Hide();
        Process.Start(new ProcessStartInfo($"plana://{target.Value}") { UseShellExecute = true });
    }

    private void LauncherSurface_KeyDown(object sender, KeyRoutedEventArgs e) { if (e.Key == Windows.System.VirtualKey.Escape) { AppWindow.Hide(); e.Handled = true; } }
    private void CenterOnDisplay()
    {
        var id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(WindowNative.GetWindowHandle(this));
        var area = DisplayArea.GetFromWindowId(id, DisplayAreaFallback.Primary).WorkArea;
        AppWindow.Move(new PointInt32(area.X + (area.Width - 680) / 2, area.Y + Math.Max(72, area.Height / 5)));
    }

    private static ActionDefinition ToDefinition(Plana.Core.Settings.UserActionSettings a) => new($"user.action.{a.Id}", a.Name, a.Kind, a.Parameters, CapabilityFor(a.Kind), a.RequiresConfirmation);
    private static ActionDefinition ToDefinition(Plana.Core.Settings.ProjectLauncherSettings p) { var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["executable"] = p.Executable }; for (var i = 0; i < p.Arguments.Count; i++) values[$"arg.{i}"] = p.Arguments[i].Replace("{folder}", p.Folder, StringComparison.OrdinalIgnoreCase); return new($"user.launcher.{p.Id}", p.Name, ActionKinds.LaunchProcess, values, CapabilityFor(ActionKinds.LaunchProcess)); }
    private static string DescribeKind(string kind) => kind switch { ActionKinds.OpenFile => App.IsChinese ? "打开文件" : "Open file", ActionKinds.OpenFolder => App.IsChinese ? "打开文件夹" : "Open folder", ActionKinds.OpenUrl => App.IsChinese ? "打开网页" : "Open website", ActionKinds.RunScript => App.IsChinese ? "运行脚本" : "Run script", ActionKinds.RunCommand => App.IsChinese ? "运行命令" : "Run command", ActionKinds.LaunchProcess => App.IsChinese ? "启动程序" : "Launch program", ActionKinds.PluginInvoke => App.IsChinese ? "插件命令" : "Plugin command", _ => kind };
    private static string GlyphFor(string kind) => kind switch { ActionKinds.OpenFile => "\uE8A5", ActionKinds.OpenFolder => "\uE8B7", ActionKinds.OpenUrl => "\uE774", ActionKinds.RunScript => "\uE943", ActionKinds.LaunchProcess => "\uE756", ActionKinds.PluginInvoke => "\uEA3A", _ => "\uE945" };
    private static HashSet<string> CapabilityFor(string kind) => [kind switch { ActionKinds.OpenUrl => Capabilities.OpenUrl, ActionKinds.OpenFile => Capabilities.OpenFile, ActionKinds.OpenFolder => Capabilities.OpenFolder, ActionKinds.RunCommand => Capabilities.RunCommand, ActionKinds.RunScript => Capabilities.RunScript, _ => Capabilities.LaunchProcess }];
}

public sealed class QuickLaunchRow(CommandDescriptor descriptor)
{
    public CommandDescriptor Descriptor { get; } = descriptor;
    public string Title => Descriptor.Title;
    public string Subtitle => Descriptor.Subtitle;
    public string Source => Descriptor.Source.Contains('|') ? Descriptor.Source[(Descriptor.Source.IndexOf('|') + 1)..] : Descriptor.Source;
    public Symbol Icon => Descriptor.IconGlyph switch
    {
        "\uE8B7" => Symbol.Folder,
        "\uE774" => Symbol.Link,
        "\uEA3A" => Symbol.Library,
        "\uE756" => Symbol.Play,
        _ => Symbol.Document,
    };
}
