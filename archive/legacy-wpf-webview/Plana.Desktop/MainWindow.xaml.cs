using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;
using Plana.Core.Actions;
using Plana.Core.Plugins;
using Plana.Desktop.Actions;
using Plana.Desktop.Localization;
using Plana.Core.Settings;
using Plana.Desktop.Extensions;
using Plana.Desktop.Plugins;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace Plana.Desktop;

public partial class MainWindow : Window, IRendererBridge
{
    private const double QuickToolbarHeight = 82;
    private const int WmNcLButtonDown = 0x00A1;
    private const int WmNcHitTest = 0x0084;
    private const int HtCaption = 0x0002;
    private const int HtTransparent = -1;
    private static readonly System.Windows.Point[] PetHitPolygon =
    [
        new(.43, .01), new(.72, .01), new(.84, .11), new(.88, .31),
        new(.82, .50), new(1, .59), new(1, .67), new(.81, .70),
        new(.79, 1), new(.34, 1), new(.28, .81), new(.16, .69),
        new(0, .64), new(0, .58), new(.22, .55), new(.27, .37), new(.31, .18),
    ];

    private readonly Forms.NotifyIcon trayIcon;
    private readonly DesktopSettingsStore settingsStore;
    private readonly ExtensionLibrary extensionLibrary;
    private readonly PluginRuntimeManager pluginRuntimeManager;
    private DesktopSettings settings = new();
    private ActionEngine? actionEngine;
    private ActionPackLoadResult packLoadResult = new([]);
    private IReadOnlyList<PluginDiagnostic> pluginDiagnostics = [];
    private ControlCenterWindow? controlCenter;
    private bool allowClose;
    private bool rendererReady;

    public MainWindow()
    {
        InitializeComponent();
        var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PlanaDesktop");
        settingsStore = new DesktopSettingsStore(Path.Combine(appData, "settings.json"));
        extensionLibrary = new ExtensionLibrary(appData);
        pluginRuntimeManager = new PluginRuntimeManager(Path.Combine(AppContext.BaseDirectory, "PluginHost", "Plana.PluginHost.exe"));
        pluginRuntimeManager.StateChanged += OnPluginRuntimeStateChanged;
        trayIcon = CreateTrayIcon();
        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        settings = await settingsStore.LoadAsync();
        LocalizationCatalog.ApplyCulture(settings.UiCulture);
        ApplyWindowSettings();
        var hostHandlers = new IActionHandler[]
        {
            new RendererActionHandler(this),
            new OpenUrlActionHandler(),
            new OpenPathActionHandler(folder: false),
            new OpenPathActionHandler(folder: true),
            new LaunchProcessActionHandler(),
            new RunCommandActionHandler(),
            new RunScriptActionHandler(),
        };
        var handlers = hostHandlers
            .Append<IActionHandler>(new PluginActionHandler(
                pluginRuntimeManager,
                new PluginHostRequestBroker(hostHandlers)))
            .ToArray();
        actionEngine = new ActionEngine(handlers, new DesktopCapabilityPolicy(settings, SaveSettingsAsync));
        await LoadPluginsAsync();
        await LoadActionPacksAsync();
        await InitializeRendererAsync();
        RebuildMenus();
    }

    private async Task LoadActionPacksAsync()
    {
        var loader = new ActionPackLoader();
        var bundled = await loader.LoadDirectoryAsync(Path.Combine(AppContext.BaseDirectory, "StarterPacks"));
        var installed = await loader.LoadDirectoryAsync(extensionLibrary.ActionPacksDirectory);
        var loaded = new ActionPackLoadResult([.. bundled.Discoveries, .. installed.Discoveries]);
        var accepted = new List<ActionPack>
        {
            BuiltInActionPack.Create(),
            UserLauncherPack.Create(settings.UserActions, settings.ProjectLaunchers),
        };
        accepted.AddRange(pluginRuntimeManager.SnapshotActionPacks());
        var discoveries = new List<ActionPackDiscovery>();
        foreach (var discovery in loaded.Discoveries)
        {
            if (!discovery.IsValid)
            {
                discoveries.Add(discovery);
                continue;
            }

            try
            {
                actionEngine!.ReplacePacks([.. accepted, discovery.Pack!]);
                discoveries.Add(discovery);
                if (!settings.DisabledActionPacks.Contains(discovery.Pack!.Id)) accepted.Add(discovery.Pack);
            }
            catch (ActionPackException exception)
            {
                discoveries.Add(discovery with { Error = exception.Message });
            }
        }
        actionEngine!.ReplacePacks(accepted);
        packLoadResult = new ActionPackLoadResult(discoveries);
        RefreshQuickToolbar();
    }

    private async Task LoadPluginsAsync()
    {
        pluginDiagnostics = await new PluginManifestLoader().LoadDirectoryAsync(extensionLibrary.PluginsDirectory);
        await pluginRuntimeManager.ReconcileAsync(pluginDiagnostics, settings, settings.UiCulture);
    }

    private async Task InitializeRendererAsync()
    {
        await WebView.EnsureCoreWebView2Async();
        WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "plana.local",
            Path.Combine(AppContext.BaseDirectory, "Renderer"),
            CoreWebView2HostResourceAccessKind.DenyCors);
        WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        WebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
        WebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
        WebView.Source = new Uri("https://plana.local/index.html");
    }

    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var message = JsonDocument.Parse(e.WebMessageAsJson);
            var type = message.RootElement.GetProperty("type").GetString();
            switch (type)
            {
                case "ready": rendererReady = true; break;
                case "drag": BeginNativeDrag(); await SavePlacementAsync(); break;
                case "interaction":
                    var interaction = message.RootElement.GetProperty("interaction").GetString() ?? "click";
                    await ExecuteInteractionAsync(interaction);
                    break;
                case "context": ShowActionMenu(); break;
                case "hide": Hide(); break;
            }
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            ShowBalloon(LocalizationCatalog.Text("RendererMessageFailed"), exception.Message);
        }
    }

    private async Task ExecuteInteractionAsync(string interaction)
    {
        if (actionEngine is null || !settings.InteractionBindings.TryGetValue(interaction, out var actionId)) return;
        var result = await actionEngine.ExecuteAsync(actionId, new ActionContext(Variables: new Dictionary<string, string>
        {
            ["appData"] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PlanaDesktop"),
        }));
        if (!result.Succeeded) ShowBalloon(LocalizationCatalog.Text("ActionFailed"), result.Message);
    }

    public async Task PlayAnimationAsync(string animation, CancellationToken cancellationToken)
    {
        if (!rendererReady) throw new InvalidOperationException("The renderer is not ready.");
        var argument = JsonSerializer.Serialize(animation);
        await WebView.CoreWebView2.ExecuteScriptAsync($"window.plana.playAnimation({argument})");
    }

    private void RebuildMenus()
    {
        var menu = new Forms.ContextMenuStrip();
        foreach (var descriptor in actionEngine?.ListActions() ?? [])
        {
            var actionId = descriptor.Action.Id;
            menu.Items.Add(descriptor.Action.Label, null, async (_, _) => await ExecuteActionFromMenuAsync(actionId));
        }
        if (menu.Items.Count > 0) menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(LocalizationCatalog.Text("TraySettings"), null, async (_, _) => await ShowControlCenterAsync());
        menu.Items.Add(LocalizationCatalog.Text("TrayShow"), null, (_, _) => ShowFromTray());
        menu.Items.Add(LocalizationCatalog.Text("TrayHide"), null, (_, _) => Hide());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(LocalizationCatalog.Text("TrayQuit"), null, async (_, _) => await QuitAsync());
        trayIcon.ContextMenuStrip?.Dispose();
        trayIcon.ContextMenuStrip = menu;
    }

    private async Task ExecuteActionFromMenuAsync(string actionId)
    {
        var result = await actionEngine!.ExecuteAsync(actionId);
        if (!result.Succeeded) ShowBalloon(LocalizationCatalog.Text("ActionFailed"), result.Message);
    }

    private Task<ActionResult> ExecuteActionFromControlCenterAsync(string actionId) =>
        actionEngine?.ExecuteAsync(actionId)
        ?? Task.FromResult(ActionResult.Failure("The Action Engine is not ready."));

    private void ShowActionMenu()
    {
        trayIcon.ContextMenuStrip?.Show(Forms.Cursor.Position);
    }

    private Forms.NotifyIcon CreateTrayIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        var icon = new Forms.NotifyIcon
        {
            Text = "Plana Desktop",
            Icon = File.Exists(iconPath) ? new Drawing.Icon(iconPath) : Drawing.SystemIcons.Application,
            Visible = true,
        };
        icon.DoubleClick += (_, _) => ShowFromTray();
        return icon;
    }

    private void ShowBalloon(string title, string message)
    {
        trayIcon.ShowBalloonTip(4000, title, message, Forms.ToolTipIcon.Warning);
    }

    private void ShowFromTray()
    {
        Show();
        Activate();
        Topmost = settings.AlwaysOnTop;
    }

    private async Task ShowControlCenterAsync(int sectionIndex = 0, bool focusActionSearch = false, string? searchQuery = null)
    {
        if (TryOpenModernControlCenter(sectionIndex, focusActionSearch, searchQuery)) return;

        await LoadPluginsAsync();
        await LoadActionPacksAsync();
        controlCenter ??= new ControlCenterWindow(
            settings,
            packLoadResult,
            pluginDiagnostics,
            actionEngine?.ListActions() ?? [],
            pluginRuntimeManager.Snapshot(),
            extensionLibrary,
            SaveAndApplySettingsAsync,
            ExecuteActionFromControlCenterAsync,
            ResetPosition);
        controlCenter.Refresh(packLoadResult, pluginDiagnostics, actionEngine?.ListActions() ?? [], pluginRuntimeManager.Snapshot());
        controlCenter.Show();
        if (controlCenter.WindowState == WindowState.Minimized) controlCenter.WindowState = WindowState.Normal;
        controlCenter.NavigateTo(sectionIndex, focusActionSearch, searchQuery);
        controlCenter.Activate();
    }

    private static bool TryOpenModernControlCenter(int sectionIndex, bool focusActionSearch, string? searchQuery)
    {
        var destination = sectionIndex == 0 && !focusActionSearch ? "settings" : "actions";
        var uri = $"plana://{destination}";
        if (!string.IsNullOrWhiteSpace(searchQuery)) uri += $"?query={Uri.EscapeDataString(searchQuery)}";

        try
        {
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    private async void OnQuickSettingsClick(object sender, RoutedEventArgs e) => await ShowControlCenterAsync(0);

    private async void OnQuickSearchClick(object sender, RoutedEventArgs e) =>
        await ShowControlCenterAsync(1, focusActionSearch: true, QuickSearchTextBox.Text.Trim());

    private async void OnQuickSearchKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Enter) return;
        e.Handled = true;
        await ShowControlCenterAsync(1, focusActionSearch: true, QuickSearchTextBox.Text.Trim());
    }

    private void OnQuickGroupChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (QuickGroupComboBox.SelectedItem is not QuickGroupOption group)
        {
            QuickActionComboBox.ItemsSource = null;
            QuickRunButton.IsEnabled = false;
            return;
        }
        var catalog = (actionEngine?.ListActions() ?? []).ToDictionary(action => action.Action.Id, StringComparer.OrdinalIgnoreCase);
        var actions = group.ActionIds
            .Where(catalog.ContainsKey)
            .Select(actionId => new QuickActionOption(actionId, catalog[actionId].Action.Label))
            .ToList();
        QuickActionComboBox.ItemsSource = actions;
        QuickActionComboBox.SelectedIndex = actions.Count > 0 ? 0 : -1;
        QuickRunButton.IsEnabled = actions.Count > 0;
    }

    private async void OnQuickRunClick(object sender, RoutedEventArgs e)
    {
        if (QuickActionComboBox.SelectedValue is string actionId) await ExecuteActionFromMenuAsync(actionId);
    }

    private void RefreshQuickToolbar()
    {
        if (QuickGroupComboBox is null) return;
        var selectedId = (QuickGroupComboBox.SelectedItem as QuickGroupOption)?.Id;
        var groups = settings.ToolGroups
            .Where(group => !string.IsNullOrWhiteSpace(group.Name))
            .Select(group => new QuickGroupOption(group.Id, group.Name, group.ActionIds))
            .ToList();
        if (groups.Count == 0)
        {
            groups.Add(new QuickGroupOption(string.Empty, LocalizationCatalog.Text("NoToolGroups"), []));
        }
        QuickGroupComboBox.ItemsSource = groups;
        QuickGroupComboBox.SelectedItem = groups.FirstOrDefault(group => group.Id == selectedId) ?? groups[0];
    }

    private void ApplyWindowSettings()
    {
        var scale = Math.Clamp(settings.Scale, 0.75, 1.5);
        Width = Math.Clamp(settings.Width * scale, 240, 680);
        Height = Math.Clamp(settings.Height * scale, 360, 1040) + QuickToolbarHeight;
        Topmost = settings.AlwaysOnTop;
        if (settings.Left is double left && settings.Top is double top && IsVisibleOnAnyScreen(left, top))
        {
            Left = left;
            Top = top - QuickToolbarHeight;
            return;
        }
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 24;
        Top = workArea.Bottom - Height - 12;
    }

    private static bool IsVisibleOnAnyScreen(double left, double top) => Forms.Screen.AllScreens.Any(screen =>
        screen.WorkingArea.IntersectsWith(new Drawing.Rectangle((int)left, (int)top, 80, 80)));

    private async Task SavePlacementAsync()
    {
        settings.Left = Left;
        settings.Top = Top + QuickToolbarHeight;
        var scale = Math.Clamp(settings.Scale, 0.75, 1.5);
        settings.Width = Width / scale;
        settings.Height = Math.Max(Height - QuickToolbarHeight, 1) / scale;
        await SaveSettingsAsync();
    }

    private async Task SaveAndApplySettingsAsync()
    {
        LocalizationCatalog.ApplyCulture(settings.UiCulture);
        await SaveSettingsAsync();
        ApplyWindowSettings();
        await LoadPluginsAsync();
        await LoadActionPacksAsync();
        RebuildMenus();
        controlCenter?.Refresh(packLoadResult, pluginDiagnostics, actionEngine?.ListActions() ?? [], pluginRuntimeManager.Snapshot());
    }

    private void ResetPosition()
    {
        settings.Left = null;
        settings.Top = null;
        ApplyWindowSettings();
    }

    private void OnPluginRuntimeStateChanged(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(async () =>
        {
            if (actionEngine is not null)
            {
                await LoadActionPacksAsync();
                RebuildMenus();
            }
            controlCenter?.Refresh(
                packLoadResult,
                pluginDiagnostics,
                actionEngine?.ListActions() ?? [],
                pluginRuntimeManager.Snapshot());
        });
    }

    private Task SaveSettingsAsync() => settingsStore.SaveAsync(settings);

    private void BeginNativeDrag()
    {
        var handle = new WindowInteropHelper(this).Handle;
        ReleaseCapture();
        SendMessage(handle, WmNcLButtonDown, HtCaption, 0);
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        ((HwndSource)PresentationSource.FromVisual(this)).AddHook(WindowMessageHook);
    }

    private nint WindowMessageHook(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message != WmNcHitTest) return 0;
        var screenPoint = new System.Windows.Point(unchecked((short)(long)lParam), unchecked((short)((long)lParam >> 16)));
        var clientPoint = PointFromScreen(screenPoint);
        if (IsPointInsideQuickToolbar(clientPoint) || IsPointInsidePet(clientPoint)) return 0;
        handled = true;
        return HtTransparent;
    }

    private bool IsPointInsideQuickToolbar(System.Windows.Point point)
    {
        if (!QuickToolbar.IsVisible || QuickToolbar.ActualWidth <= 0 || QuickToolbar.ActualHeight <= 0) return false;
        var origin = QuickToolbar.TranslatePoint(new System.Windows.Point(), this);
        return new Rect(origin.X, origin.Y, QuickToolbar.ActualWidth, QuickToolbar.ActualHeight).Contains(point);
    }

    private bool IsPointInsidePet(System.Windows.Point point)
    {
        var rendererOrigin = WebView.TranslatePoint(new System.Windows.Point(), this);
        var rendererPoint = new System.Windows.Point(point.X - rendererOrigin.X, point.Y - rendererOrigin.Y);
        if (rendererPoint.X < 0 || rendererPoint.Y < 0
            || rendererPoint.X > WebView.ActualWidth || rendererPoint.Y > WebView.ActualHeight) return false;
        var normalized = new System.Windows.Point(
            rendererPoint.X / Math.Max(WebView.ActualWidth, 1),
            rendererPoint.Y / Math.Max(WebView.ActualHeight, 1));
        var inside = false;
        for (int current = 0, previous = PetHitPolygon.Length - 1; current < PetHitPolygon.Length; previous = current++)
        {
            var a = PetHitPolygon[current];
            var b = PetHitPolygon[previous];
            if ((a.Y > normalized.Y) != (b.Y > normalized.Y)
                && normalized.X < (b.X - a.X) * (normalized.Y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
        }
        return inside;
    }

    private async Task QuitAsync()
    {
        await SavePlacementAsync();
        allowClose = true;
        trayIcon.Visible = false;
        trayIcon.Dispose();
        pluginRuntimeManager.StateChanged -= OnPluginRuntimeStateChanged;
        await pluginRuntimeManager.DisposeAsync();
        controlCenter?.ForceClose();
        WebView.Dispose();
        Close();
        System.Windows.Application.Current.Shutdown();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (allowClose) return;
        e.Cancel = true;
        Hide();
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern nint SendMessage(nint hWnd, int msg, nint wParam, nint lParam);

    private sealed record QuickGroupOption(string Id, string Name, IReadOnlyList<string> ActionIds);
    private sealed record QuickActionOption(string Id, string Label);
}
