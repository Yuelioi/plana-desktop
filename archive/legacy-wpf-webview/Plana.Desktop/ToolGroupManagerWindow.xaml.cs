using System.Windows;
using System.Windows.Controls;
using Plana.Core.Actions;
using Plana.Desktop.Localization;
using Plana.Core.Settings;

namespace Plana.Desktop;

public partial class ToolGroupManagerWindow : Window
{
    private readonly DesktopSettings settings;
    private readonly Func<Task> saveAndApply;
    private readonly IReadOnlyList<ActionDescriptor> actions;
    private ToolGroupSettings? selectedGroup;
    private List<ActionChoice> actionChoices = [];

    internal ToolGroupManagerWindow(
        DesktopSettings settings,
        IReadOnlyList<ActionDescriptor> actions,
        Func<Task> saveAndApply)
    {
        InitializeComponent();
        this.settings = settings;
        this.actions = actions;
        this.saveAndApply = saveAndApply;
        RefreshGroups();
        BeginNewGroup();
    }

    private void RefreshGroups()
    {
        GroupList.ItemsSource = null;
        GroupList.ItemsSource = settings.ToolGroups;
    }

    private void BeginNewGroup()
    {
        selectedGroup = null;
        GroupList.SelectedItem = null;
        GroupNameTextBox.Clear();
        PopulateActions([]);
        EditorErrorText.Text = string.Empty;
    }

    private void PopulateActions(IEnumerable<string> selectedIds)
    {
        var selected = new HashSet<string>(selectedIds, StringComparer.OrdinalIgnoreCase);
        actionChoices = actions.Select(action => new ActionChoice(
            action.Action.Id,
            action.Action.Label,
            $"{action.PackName} · {action.Action.Kind}",
            selected.Contains(action.Action.Id))).ToList();
        AvailableActionsList.ItemsSource = actionChoices;
    }

    private void OnGroupSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GroupList.SelectedItem is not ToolGroupSettings group) return;
        selectedGroup = group;
        GroupNameTextBox.Text = group.Name;
        PopulateActions(group.ActionIds);
        EditorErrorText.Text = string.Empty;
    }

    private void OnNewGroupClick(object sender, RoutedEventArgs e) => BeginNewGroup();

    private async void OnDeleteGroupClick(object sender, RoutedEventArgs e)
    {
        if (selectedGroup is null) return;
        settings.ToolGroups.Remove(selectedGroup);
        await saveAndApply();
        RefreshGroups();
        BeginNewGroup();
    }

    private async void OnSaveGroupClick(object sender, RoutedEventArgs e)
    {
        var name = GroupNameTextBox.Text.Trim();
        var selectedActionIds = actionChoices.Where(action => action.IsSelected).Select(action => action.Id).ToList();
        if (string.IsNullOrWhiteSpace(name))
        {
            EditorErrorText.Text = LocalizationCatalog.Text("ToolGroupNameRequired");
            return;
        }
        if (selectedActionIds.Count == 0)
        {
            EditorErrorText.Text = LocalizationCatalog.Text("ToolGroupActionRequired");
            return;
        }

        selectedGroup ??= new ToolGroupSettings();
        selectedGroup.Name = name;
        selectedGroup.ActionIds = selectedActionIds;
        if (!settings.ToolGroups.Contains(selectedGroup)) settings.ToolGroups.Add(selectedGroup);
        await saveAndApply();
        RefreshGroups();
        GroupList.SelectedItem = selectedGroup;
        EditorErrorText.Text = string.Empty;
    }

    private sealed class ActionChoice(string id, string label, string detail, bool isSelected)
    {
        public string Id { get; } = id;
        public string Label { get; } = label;
        public string Detail { get; } = detail;
        public bool IsSelected { get; set; } = isSelected;
    }
}
