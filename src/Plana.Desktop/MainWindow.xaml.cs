using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;
using Plana.Core.Actions;
using Plana.Desktop.Actions;
using Plana.Desktop.Settings;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace Plana.Desktop;

public partial class MainWindow : Window, IRendererBridge
{
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
    private DesktopSettings settings = new();
    private ActionEngine? actionEngine;
    private bool allowClose;
    private bool rendererReady;

    public MainWindow()
    {
        InitializeComponent();
        var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PlanaDesktop");
        settingsStore = new DesktopSettingsStore(Path.Combine(appData, "settings.json"));
        trayIcon = CreateTrayIcon();
        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        settings = await settingsStore.LoadAsync();
        ApplyWindowSettings();
        var handlers = new IActionHandler[]
        {
            new RendererActionHandler(this),
            new OpenUrlActionHandler(),
            new LaunchProcessActionHandler(),
            new RunCommandActionHandler(),
        };
        actionEngine = new ActionEngine(handlers, new DesktopCapabilityPolicy(settings, SaveSettingsAsync));
        await LoadActionPacksAsync();
        await InitializeRendererAsync();
        RebuildMenus();
    }

    private async Task LoadActionPacksAsync()
    {
        var packDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PlanaDesktop", "packs");
        var packs = await new ActionPackLoader().LoadDirectoryAsync(packDirectory);
        actionEngine!.ReplacePacks([BuiltInActionPack.Create(), .. packs]);
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
            ShowBalloon("Renderer message failed", exception.Message);
        }
    }

    private async Task ExecuteInteractionAsync(string interaction)
    {
        if (actionEngine is null || !settings.InteractionBindings.TryGetValue(interaction, out var actionId)) return;
        var result = await actionEngine.ExecuteAsync(actionId, new ActionContext(Variables: new Dictionary<string, string>
        {
            ["appData"] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PlanaDesktop"),
        }));
        if (!result.Succeeded) ShowBalloon("Action failed", result.Message);
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
        menu.Items.Add("Show Plana", null, (_, _) => ShowFromTray());
        menu.Items.Add("Hide Plana", null, (_, _) => Hide());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Quit", null, async (_, _) => await QuitAsync());
        trayIcon.ContextMenuStrip?.Dispose();
        trayIcon.ContextMenuStrip = menu;
    }

    private async Task ExecuteActionFromMenuAsync(string actionId)
    {
        var result = await actionEngine!.ExecuteAsync(actionId);
        if (!result.Succeeded) ShowBalloon("Action failed", result.Message);
    }

    private void ShowActionMenu()
    {
        trayIcon.ContextMenuStrip?.Show(Forms.Cursor.Position);
    }

    private Forms.NotifyIcon CreateTrayIcon()
    {
        var icon = new Forms.NotifyIcon
        {
            Text = "Plana Desktop",
            Icon = Drawing.SystemIcons.Application,
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

    private void ApplyWindowSettings()
    {
        Width = Math.Clamp(settings.Width, 240, 680);
        Height = Math.Clamp(settings.Height, 360, 1040);
        Topmost = settings.AlwaysOnTop;
        if (settings.Left is double left && settings.Top is double top && IsVisibleOnAnyScreen(left, top))
        {
            Left = left;
            Top = top;
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
        settings.Top = Top;
        settings.Width = Width;
        settings.Height = Height;
        await SaveSettingsAsync();
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
        if (IsPointInsidePet(clientPoint)) return 0;
        handled = true;
        return HtTransparent;
    }

    private bool IsPointInsidePet(System.Windows.Point point)
    {
        var normalized = new System.Windows.Point(point.X / Math.Max(ActualWidth, 1), point.Y / Math.Max(ActualHeight, 1));
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
}
