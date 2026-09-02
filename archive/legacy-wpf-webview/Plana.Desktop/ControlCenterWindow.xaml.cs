using System.ComponentModel;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using Plana.Core.Actions;
using Plana.Core.Plugins;
using Plana.Desktop.Actions;
using Plana.Desktop.Extensions;
using Plana.Desktop.Localization;
using Plana.Core.Settings;
using Plana.Desktop.Plugins;

namespace Plana.Desktop;

public partial class ControlCenterWindow : Window
{
    private static readonly LanguageOption[] Languages =
    [
        new("en", "English"),
        new("zh-CN", "简体中文"),
    ];

    private readonly DesktopSettings settings;
    private readonly Func<Task> saveAndApply;
    private readonly Func<string, Task<ActionResult>> executeAction;
    private readonly Action resetPosition;
    private readonly ExtensionLibrary extensionLibrary;
    private List<InteractionRow> interactionRows = [];
    private List<PackRow> packRows = [];
    private List<ActionRow> actionRows = [];
    private IReadOnlyList<ActionDescriptor> currentActions = [];
    private bool allowClose;

    internal ControlCenterWindow(
        DesktopSettings settings,
        ActionPackLoadResult loadResult,
        IReadOnlyList<PluginDiagnostic> pluginDiagnostics,
        IReadOnlyList<ActionDescriptor> actions,
        IReadOnlyDictionary<string, PluginRuntimeInfo> pluginRuntime,
        ExtensionLibrary extensionLibrary,
        Func<Task> saveAndApply,
        Func<string, Task<ActionResult>> executeAction,
        Action resetPosition)
    {
        InitializeComponent();
        this.settings = settings;
        this.extensionLibrary = extensionLibrary;
        this.saveAndApply = saveAndApply;
        this.executeAction = executeAction;
        this.resetPosition = resetPosition;

        AlwaysOnTopCheckBox.IsChecked = settings.AlwaysOnTop;
        StartWithWindowsCheckBox.IsChecked = settings.StartWithWindows;
        LanguageComboBox.ItemsSource = Languages;
        LanguageComboBox.SelectedValue = settings.UiCulture;
        if (LanguageComboBox.SelectedIndex < 0) LanguageComboBox.SelectedValue = "en";
        ScaleSlider.Value = Math.Clamp(settings.Scale, 0.75, 1.5);

        Refresh(loadResult, pluginDiagnostics, actions, pluginRuntime);

        Closing += OnClosing;
    }

    internal void Refresh(
        ActionPackLoadResult loadResult,
        IReadOnlyList<PluginDiagnostic> pluginDiagnostics,
        IReadOnlyList<ActionDescriptor> actions,
        IReadOnlyDictionary<string, PluginRuntimeInfo> pluginRuntime)
    {
        var options = new[] { new ActionOption(null, LocalizationCatalog.Text("NoneOption")) }
            .Concat(actions.Select(action => new ActionOption(action.Action.Id, $"{action.Action.Label} — {action.PackName}")))
            .ToList();
        foreach (var missingActionId in InteractionPolicy.ConfigurableInteractions
                     .Select(interaction => settings.InteractionBindings.GetValueOrDefault(interaction))
                     .Where(actionId => !string.IsNullOrWhiteSpace(actionId) && options.All(option => !string.Equals(option.Id, actionId, StringComparison.OrdinalIgnoreCase)))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            options.Add(new ActionOption(missingActionId, LocalizationCatalog.Text("MissingActionOption", missingActionId!)));
        }
        interactionRows = InteractionPolicy.ConfigurableInteractions.Select(interaction => new InteractionRow(
            interaction,
            LocalizationCatalog.Text(interaction == "click" ? "InteractionClick" : "InteractionDoubleClick"),
            options,
            settings.InteractionBindings.GetValueOrDefault(interaction)))
            .ToList();
        InteractionList.ItemsSource = interactionRows;

        packRows = loadResult.Discoveries.Select(discovery => PackRow.Create(discovery, settings)).ToList();
        PackList.ItemsSource = packRows;
        PackSummaryText.Text = packRows.Count == 0
            ? LocalizationCatalog.Text("NoPacksSummary")
            : LocalizationCatalog.Text("PacksSummary", packRows.Count, packRows.Count == 1 ? string.Empty : "s");

        var pluginRows = pluginDiagnostics.Select(plugin => PluginRow.Create(plugin, settings, pluginRuntime)).ToList();
        PluginList.ItemsSource = pluginRows;
        PluginSummaryText.Text = pluginRows.Count == 0
            ? LocalizationCatalog.Text("NoPluginsSummary")
            : LocalizationCatalog.Text("PluginsSummary", pluginRows.Count, pluginRows.Count == 1 ? string.Empty : "s");

        actionRows = actions.Select(ActionRow.Create).ToList();
        currentActions = actions;
        ApplyActionFilter();

    }

    private void OnActionSearchChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => ApplyActionFilter();

    private void OnNavigationChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (SettingsTabs is not null && NavigationList.SelectedIndex >= 0)
        {
            SettingsTabs.SelectedIndex = NavigationList.SelectedIndex;
        }
    }

    private void ApplyActionFilter()
    {
        if (ActionList is null) return;
        var query = ActionSearchBox?.Text.Trim() ?? string.Empty;
        ActionList.ItemsSource = string.IsNullOrWhiteSpace(query)
            ? actionRows
            : actionRows.Where(row => row.SearchText.Contains(query, StringComparison.CurrentCultureIgnoreCase)).ToList();
    }

    private async void OnRunActionClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string actionId }) return;
        var result = await executeAction(actionId);
        StatusText.Foreground = result.Succeeded ? System.Windows.Media.Brushes.ForestGreen : System.Windows.Media.Brushes.Firebrick;
        StatusText.Text = result.Message;
    }

    private void OnOpenPacksFolderClick(object sender, RoutedEventArgs e) => extensionLibrary.OpenActionPacksDirectory();

    private void OnOpenPluginsFolderClick(object sender, RoutedEventArgs e) => extensionLibrary.OpenPluginsDirectory();

    private void OnOpenPackLocationClick(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: string manifestPath }) extensionLibrary.OpenManifestLocation(manifestPath);
    }

    private async void OnImportPackClick(object sender, RoutedEventArgs e)
    {
        var folder = SelectExtensionFolder(LocalizationCatalog.Text("SelectPackFolderTitle"));
        if (folder is null) return;
        await ImportExtensionAsync(() => extensionLibrary.ImportActionPackAsync(folder));
    }

    private async void OnImportPluginClick(object sender, RoutedEventArgs e)
    {
        var folder = SelectExtensionFolder(LocalizationCatalog.Text("SelectPluginFolderTitle"));
        if (folder is null) return;
        await ImportExtensionAsync(() => extensionLibrary.ImportPluginAsync(folder));
    }

    private async void OnImportSamplePluginClick(object sender, RoutedEventArgs e)
    {
        var sampleDirectory = Path.Combine(AppContext.BaseDirectory, "SamplePlugins", "hello");
        await ImportExtensionAsync(() => extensionLibrary.ImportPluginAsync(sampleDirectory));
    }

    private async void OnEnablePluginClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string pluginId }) return;
        settings.DisabledPlugins.Remove(pluginId);
        await saveAndApply();
        StatusText.Foreground = System.Windows.Media.Brushes.ForestGreen;
        StatusText.Text = LocalizationCatalog.Text("PluginEnabledMessage", pluginId);
    }

    private async void OnDisablePluginClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string pluginId }) return;
        settings.DisabledPlugins.Add(pluginId);
        await saveAndApply();
        StatusText.Foreground = System.Windows.Media.Brushes.ForestGreen;
        StatusText.Text = LocalizationCatalog.Text("PluginDisabledStatus", pluginId);
    }

    private async void OnReloadExtensionsClick(object sender, RoutedEventArgs e)
    {
        await saveAndApply();
        StatusText.Foreground = System.Windows.Media.Brushes.ForestGreen;
        StatusText.Text = LocalizationCatalog.Text("ExtensionsReloadedStatus");
    }

    private string? SelectExtensionFolder(string title)
    {
        var dialog = new OpenFolderDialog { Title = title, Multiselect = false };
        return dialog.ShowDialog(this) == true ? dialog.FolderName : null;
    }

    private async Task ImportExtensionAsync(Func<Task<ExtensionImportResult>> import)
    {
        try
        {
            var result = await import();
            StatusText.Foreground = result.Succeeded ? System.Windows.Media.Brushes.ForestGreen : System.Windows.Media.Brushes.Firebrick;
            StatusText.Text = LocalizationCatalog.Text($"ExtensionImport{result.Code}", result.Detail ?? string.Empty);
            if (result.Succeeded) await saveAndApply();
        }
        catch (Exception exception)
        {
            StatusText.Foreground = System.Windows.Media.Brushes.Firebrick;
            StatusText.Text = LocalizationCatalog.Text("ExtensionImportFailed", exception.Message);
        }
    }

    private async void OnAddLauncherClick(object sender, RoutedEventArgs e)
    {
        var editor = new LauncherEditorWindow { Owner = this };
        if (editor.ShowDialog() != true || editor.Result is null) return;
        settings.ProjectLaunchers.Add(editor.Result);
        await saveAndApply();
        StatusText.Foreground = System.Windows.Media.Brushes.ForestGreen;
        StatusText.Text = LocalizationCatalog.Text("LauncherAddedStatus");
    }

    private async void OnAddCustomActionClick(object sender, RoutedEventArgs e)
    {
        var editor = new ActionEditorWindow { Owner = this };
        if (editor.ShowDialog() != true || editor.Result is null) return;
        settings.UserActions.Add(editor.Result);
        await saveAndApply();
        StatusText.Foreground = System.Windows.Media.Brushes.ForestGreen;
        StatusText.Text = LocalizationCatalog.Text("CustomActionAddedStatus");
    }

    private void OnManageToolGroupsClick(object sender, RoutedEventArgs e)
    {
        var manager = new ToolGroupManagerWindow(settings, currentActions, saveAndApply) { Owner = this };
        manager.ShowDialog();
    }

    private async void OnRemoveLauncherClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string actionId }) return;
        if (actionId.StartsWith("user.launcher.", StringComparison.OrdinalIgnoreCase))
        {
            var launcherId = actionId["user.launcher.".Length..];
            settings.ProjectLaunchers.RemoveAll(launcher => string.Equals(launcher.Id, launcherId, StringComparison.OrdinalIgnoreCase));
            StatusText.Text = LocalizationCatalog.Text("LauncherRemovedStatus");
        }
        else if (actionId.StartsWith("user.action.", StringComparison.OrdinalIgnoreCase))
        {
            var userActionId = actionId["user.action.".Length..];
            settings.UserActions.RemoveAll(action => string.Equals(action.Id, userActionId, StringComparison.OrdinalIgnoreCase));
            StatusText.Text = LocalizationCatalog.Text("CustomActionRemovedStatus");
        }
        await saveAndApply();
        StatusText.Foreground = System.Windows.Media.Brushes.ForestGreen;
    }

    public void ForceClose()
    {
        allowClose = true;
        Close();
    }

    internal void NavigateTo(int sectionIndex, bool focusActionSearch = false, string? searchQuery = null)
    {
        NavigationList.SelectedIndex = Math.Clamp(sectionIndex, 0, NavigationList.Items.Count - 1);
        SettingsTabs.SelectedIndex = NavigationList.SelectedIndex;
        if (focusActionSearch)
        {
            if (searchQuery is not null) ActionSearchBox.Text = searchQuery;
            Dispatcher.BeginInvoke(() =>
            {
                ActionSearchBox.Focus();
                ActionSearchBox.SelectAll();
            });
        }
    }

    private void OnScaleValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ScaleValueText is not null) ScaleValueText.Text = $"{e.NewValue:P0}";
    }

    private void OnResetPositionClick(object sender, RoutedEventArgs e)
    {
        resetPosition();
        StatusText.Text = LocalizationCatalog.Text("PositionResetStatus");
    }

    private void OnRevokeCapabilitiesClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string packId }) return;
        settings.CapabilityGrants.Remove(packId);
        var row = packRows.FirstOrDefault(item => string.Equals(item.PackId, packId, StringComparison.OrdinalIgnoreCase));
        if (row is not null) row.CanRevoke = false;
        StatusText.Text = LocalizationCatalog.Text("CapabilitiesRevokedStatus");
    }

    private async void OnSaveClick(object sender, RoutedEventArgs e)
    {
        settings.AlwaysOnTop = AlwaysOnTopCheckBox.IsChecked == true;
        settings.Scale = ScaleSlider.Value;
        settings.UiCulture = LanguageComboBox.SelectedValue as string ?? "en";

        settings.InteractionBindings.Clear();
        foreach (var row in interactionRows.Where(row => !string.IsNullOrWhiteSpace(row.SelectedActionId)))
        {
            settings.InteractionBindings[row.Id] = row.SelectedActionId!;
        }

        settings.DisabledActionPacks.Clear();
        foreach (var row in packRows.Where(row => row.CanEnable && !row.IsEnabled && row.PackId is not null))
        {
            settings.DisabledActionPacks.Add(row.PackId!);
        }

        try
        {
            await saveAndApply();
            StatusText.Foreground = System.Windows.Media.Brushes.ForestGreen;
            StatusText.Text = LocalizationCatalog.Text("ChangesSavedStatus");
        }
        catch (Exception exception)
        {
            StatusText.Foreground = System.Windows.Media.Brushes.Firebrick;
            StatusText.Text = LocalizationCatalog.Text("ChangesSaveFailedStatus", exception.Message);
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Hide();

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (allowClose) return;
        e.Cancel = true;
        Hide();
    }

    private sealed record ActionOption(string? Id, string Label);
    private sealed record LanguageOption(string Culture, string Label);

    private sealed class ActionRow
    {
        public required string Id { get; init; }
        public required string Label { get; init; }
        public required string Detail { get; init; }
        public required string SearchText { get; init; }
        public bool CanRemove { get; init; }

        public static ActionRow Create(ActionDescriptor descriptor)
        {
            var detail = $"{descriptor.PackName} · {descriptor.Action.Kind}";
            return new ActionRow
            {
                Id = descriptor.Action.Id,
                Label = descriptor.Action.Label,
                Detail = detail,
                SearchText = $"{descriptor.Action.Label} {detail} {descriptor.Action.Id}",
                CanRemove = string.Equals(descriptor.PackId, UserLauncherPack.PackId, StringComparison.OrdinalIgnoreCase),
            };
        }
    }

    private sealed class InteractionRow(string id, string label, IReadOnlyList<ActionOption> options, string? selectedActionId)
    {
        public string Id { get; } = id;
        public string Label { get; } = label;
        public IReadOnlyList<ActionOption> Options { get; } = options;
        public string? SelectedActionId { get; set; } = selectedActionId;
    }

    private sealed class PackRow : INotifyPropertyChanged
    {
        private bool canRevoke;

        public string? PackId { get; init; }
        public required string Name { get; init; }
        public required string Metadata { get; init; }
        public required string CapabilitiesText { get; init; }
        public required string SourceText { get; init; }
        public required string ManifestPath { get; init; }
        public string? Error { get; init; }
        public bool CanEnable { get; init; }
        public bool IsEnabled { get; set; }
        public bool CanRevoke
        {
            get => canRevoke;
            set
            {
                if (canRevoke == value) return;
                canRevoke = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanRevoke)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public static PackRow Create(ActionPackDiscovery discovery, DesktopSettings settings)
        {
            var pack = discovery.Pack;
            var capabilities = pack?.Actions.SelectMany(action => action.Capabilities).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToArray() ?? [];
            return new PackRow
            {
                PackId = pack?.Id,
                Name = discovery.DisplayName,
                Metadata = pack is null ? discovery.ManifestPath : LocalizationCatalog.Text("PackMetadata", pack.Publisher, pack.Version),
                CapabilitiesText = capabilities.Length == 0
                    ? LocalizationCatalog.Text("CapabilitiesNone")
                    : LocalizationCatalog.Text("CapabilitiesList", string.Join(", ", capabilities)),
                SourceText = discovery.ManifestPath.StartsWith(
                    Path.Combine(AppContext.BaseDirectory, "StarterPacks"),
                    StringComparison.OrdinalIgnoreCase)
                    ? LocalizationCatalog.Text("PackSourceBundled")
                    : LocalizationCatalog.Text("PackSourceInstalled"),
                ManifestPath = discovery.ManifestPath,
                Error = discovery.Error,
                CanEnable = discovery.IsValid,
                IsEnabled = discovery.IsValid && !settings.DisabledActionPacks.Contains(pack!.Id),
                CanRevoke = pack is not null && settings.CapabilityGrants.ContainsKey(pack.Id),
            };
        }
    }

    private sealed class PluginRow
    {
        public required string Name { get; init; }
        public required string Metadata { get; init; }
        public required string CapabilitiesText { get; init; }
        public required string Status { get; init; }
        public string? Error { get; init; }
        public string? PluginId { get; init; }
        public bool CanEnable { get; init; }
        public bool CanDisable { get; init; }

        public static PluginRow Create(
            PluginDiagnostic diagnostic,
            DesktopSettings settings,
            IReadOnlyDictionary<string, PluginRuntimeInfo> runtimeStates)
        {
            var plugin = diagnostic.Manifest;
            var enabled = diagnostic.IsValid && plugin is not null && !settings.DisabledPlugins.Contains(plugin.Id);
            var runtime = plugin is null ? null : runtimeStates.GetValueOrDefault(plugin.Id);
            var statusKey = !diagnostic.IsValid
                ? "PluginInvalidStatus"
                : enabled
                    ? runtime?.Status switch
                    {
                        "Ready" => "PluginReadyStatus",
                        "Failed" => "PluginFailedStatus",
                        "Exited" => "PluginExitedStatus",
                        _ => "PluginStartingStatus",
                    }
                    : "PluginDisabledSimpleStatus";
            return new PluginRow
            {
                Name = plugin?.Id ?? Path.GetFileName(Path.GetDirectoryName(diagnostic.ManifestPath)) ?? "Unknown plugin",
                Metadata = plugin is null
                    ? diagnostic.ManifestPath
                    : LocalizationCatalog.Text("PluginMetadata", plugin.Publisher, plugin.Version, plugin.HostApi),
                CapabilitiesText = plugin is null || plugin.Capabilities.Count == 0
                    ? LocalizationCatalog.Text("CapabilitiesNone")
                    : LocalizationCatalog.Text("CapabilitiesList", string.Join(", ", plugin.Capabilities.Order())),
                Status = LocalizationCatalog.Text(statusKey),
                Error = diagnostic.Error ?? runtime?.Message,
                PluginId = plugin?.Id,
                CanEnable = diagnostic.IsValid && !enabled,
                CanDisable = enabled,
            };
        }
    }
}
