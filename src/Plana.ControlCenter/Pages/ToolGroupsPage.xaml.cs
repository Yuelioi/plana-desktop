using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Plana.Core.Actions;
using Plana.Core.Settings;

namespace Plana_ControlCenter.Pages;

public sealed partial class ToolGroupsPage : Page
{
    private readonly List<ToolGroupRow> _allGroups = [];
    private readonly List<ToolGroupActionOption> _actions = [];
    private ToolGroupSettings? _editingGroup;
    public ObservableCollection<ToolGroupRow> VisibleGroups { get; } = [];

    public ToolGroupsPage()
    {
        InitializeComponent();
        Loaded += ToolGroupsPage_Loaded;
    }

    private async void ToolGroupsPage_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyLanguage();
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        _actions.Clear();
        _actions.AddRange(App.Settings.UserActions.Select(action => new ToolGroupActionOption($"user.action.{action.Id}", action.Name, string.IsNullOrWhiteSpace(action.Description) ? action.Kind : action.Description)));
        _actions.AddRange(App.Settings.ProjectLaunchers.Select(project => new ToolGroupActionOption($"user.launcher.{project.Id}", project.Name, project.Folder)));
        var packs = await new ActionPackLoader().LoadDirectoryAsync(Path.Combine(App.DataDirectory, "packs"));
        _actions.AddRange(packs.ValidPacks.Where(pack => !App.Settings.DisabledActionPacks.Contains(pack.Id)).SelectMany(pack => pack.Actions.Select(action => new ToolGroupActionOption(action.Id, action.Label, pack.Name))));

        _allGroups.Clear();
        _allGroups.AddRange(App.Settings.ToolGroups.Select(group => new ToolGroupRow(group, Summarize(group))));
        Filter(SearchBox.Text);
    }

    private string Summarize(ToolGroupSettings group)
    {
        var names = group.ActionIds.Select(id => _actions.FirstOrDefault(action => action.Id.Equals(id, StringComparison.OrdinalIgnoreCase))?.Name).Where(name => name is not null).ToArray();
        return names.Length == 0 ? (App.IsChinese ? "空工具组" : "Empty group") : string.Join("、", names!);
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args) => Filter(sender.Text);

    private void Filter(string? query)
    {
        VisibleGroups.Clear();
        var needle = query?.Trim() ?? string.Empty;
        foreach (var group in _allGroups.Where(group => needle.Length == 0 || group.Name.Contains(needle, StringComparison.CurrentCultureIgnoreCase) || group.Summary.Contains(needle, StringComparison.CurrentCultureIgnoreCase)))
            VisibleGroups.Add(group);
    }

    private async void NewGroupButton_Click(object sender, RoutedEventArgs e)
    {
        _editingGroup = null;
        await ShowEditorAsync(null);
    }

    private async void EditGroupButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not ToolGroupRow row) return;
        _editingGroup = row.Settings;
        await ShowEditorAsync(row.Settings);
    }

    private async Task ShowEditorAsync(ToolGroupSettings? group)
    {
        GroupNameInput.Text = group?.Name ?? string.Empty;
        GroupActionsList.ItemsSource = _actions;
        GroupActionsList.SelectedItems.Clear();
        if (group is not null)
            foreach (var action in _actions.Where(action => group.ActionIds.Contains(action.Id, StringComparer.OrdinalIgnoreCase))) GroupActionsList.SelectedItems.Add(action);
        EditorError.Text = string.Empty;
        GroupDialog.Title = group is null ? (App.IsChinese ? "新建工具组" : "New Tool Group") : (App.IsChinese ? "编辑工具组" : "Edit Tool Group");
        GroupDialog.PrimaryButtonText = App.IsChinese ? "保存" : "Save";
        GroupDialog.CloseButtonText = App.IsChinese ? "取消" : "Cancel";
        GroupDialog.XamlRoot = XamlRoot;
        await GroupDialog.ShowAsync();
    }

    private async void DeleteGroupButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not ToolGroupRow row) return;
        App.Settings.ToolGroups.Remove(row.Settings);
        if (App.Settings.SelectedToolGroupId == row.Settings.Id) App.Settings.SelectedToolGroupId = App.Settings.ToolGroups.FirstOrDefault()?.Id;
        await App.SettingsStore.SaveAsync(App.Settings);
        await ReloadAsync();
    }

    private async void GroupDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            var name = GroupNameInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                EditorError.Text = App.IsChinese ? "请输入工具组名称。" : "Enter a Tool Group name.";
                args.Cancel = true;
                return;
            }
            if (_editingGroup is null)
            {
                _editingGroup = new ToolGroupSettings();
                App.Settings.ToolGroups.Add(_editingGroup);
            }
            _editingGroup.Name = name;
            _editingGroup.ActionIds = GroupActionsList.SelectedItems.Cast<ToolGroupActionOption>().Select(action => action.Id).ToList();
            App.Settings.SelectedToolGroupId ??= _editingGroup.Id;
            await App.SettingsStore.SaveAsync(App.Settings);
            await ReloadAsync();
        }
        finally { deferral.Complete(); }
    }

    private void ApplyLanguage()
    {
        if (!App.IsChinese) return;
        PageTitle.Text = "工具组";
        PageDescription.Text = "整理桌宠工具栏中的可选快捷操作。";
        SearchBox.PlaceholderText = "搜索工具组";
        NewGroupLabel.Text = "新建工具组";
        NameHeader.Text = "名称";
        ActionsHeader.Text = "操作";
        GroupNameInput.Header = "名称";
        GroupNameInput.PlaceholderText = "例如：Codex 项目";
        OptionalActionsLabel.Text = "操作（可选）";
        OptionalActionsHelp.Text = "允许创建空工具组；可以现在关联操作，也可以以后再编辑。";
    }
}

public sealed record ToolGroupActionOption(string Id, string Name, string Detail);
public sealed class ToolGroupRow(ToolGroupSettings settings, string summary)
{
    public ToolGroupSettings Settings { get; set; } = settings;
    public string Summary { get; set; } = summary;
    public string Name => Settings.Name;
}
