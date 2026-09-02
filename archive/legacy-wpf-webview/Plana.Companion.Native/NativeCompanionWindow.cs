using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Plana.Core.Companion;
using Plana.Core.Actions;
using Plana.Core.Settings;
using Plana.Core.Plugins;
using Forms = System.Windows.Forms;

namespace Plana.Companion.Native;

internal sealed class NativeCompanionWindow : ICompanionController
{
    private static readonly (double X, double Y)[] CharacterHitPolygon =
    [
        (.43, .01), (.72, .01), (.84, .11), (.88, .31), (.82, .50), (1, .59), (1, .67),
        (.81, .70), (.79, 1), (.34, 1), (.28, .81), (.16, .69), (0, .64), (0, .58),
        (.22, .55), (.27, .37), (.31, .18),
    ];
    private const string WindowClassName = "PlanaNativeCompanionWindow";
    private const uint WsPopup = 0x80000000;
    private const uint WsExLayered = 0x00080000;
    private const uint WsExToolWindow = 0x00000080;
    private const uint WsExNoActivate = 0x08000000;
    private const uint LwaColorKey = 0x00000001;
    private const int SwHide = 0;
    private const int SwShowNoActivate = 4;
    private const int SwpNoActivate = 0x0010;
    private const int SwpShowWindow = 0x0040;
    private const int SwpNoSize = 0x0001;
    private const int HtTransparent = -1;
    private const int HtClient = 1;
    private const int WmDestroy = 0x0002;
    private const int WmPaint = 0x000F;
    private const int WmNcHitTest = 0x0084;
    private const int WmSetCursor = 0x0020;
    private const int WmDpiChanged = 0x02E0;
    private const int WmReloadSettings = 0x8001;
    private const uint TransparentColor = 0x00FF00FF;

    private readonly WndProc _windowProcedure;
    private CompanionSurfaceState _state = new(null, null, 340, 520, 1, true);
    private bool _disposed;
    private readonly PluginRuntimeManager _pluginRuntime;
    private WebViewCompositionHost? _renderer;
    private IReadOnlyDictionary<string, NativeActionEntry> _actions = new Dictionary<string, NativeActionEntry>();
    private string? _settingsPath;
    private DateTime _settingsLastWriteUtc;
    private System.Threading.Timer? _settingsTimer;
    private IReadOnlyDictionary<string, string> _interactionBindings = new Dictionary<string, string>();
    private bool _isChinese;
    private bool _toolsCollapsed;
    private DesktopSettings _currentSettings = new();
    private Forms.ContextMenuStrip? _contextMenu;
    private bool _dragCandidate;
    private Point _dragStartCursor;
    private Rect _dragStartWindow;

    public nint WindowHandle { get; private set; }

    public NativeCompanionWindow(PluginRuntimeManager pluginRuntime)
    {
        _pluginRuntime = pluginRuntime;
        SetProcessDpiAwarenessContext(new nint(-4));
        _windowProcedure = WindowProc;
        var module = GetModuleHandle(null);
        var windowClass = new WndClass
        {
            ClassName = WindowClassName,
            Instance = module,
            WindowProcedure = Marshal.GetFunctionPointerForDelegate(_windowProcedure),
            Cursor = LoadCursor(0, new nint(32512)),
        };
        if (RegisterClass(ref windowClass) == 0 && Marshal.GetLastWin32Error() != 1410)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        WindowHandle = CreateWindowEx(
            WsExLayered | WsExToolWindow | WsExNoActivate,
            WindowClassName,
            "Plana Native Companion",
            WsPopup,
            100,
            100,
            340,
            520,
            0,
            0,
            module,
            0);
        if (WindowHandle == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        SetWindowText(WindowHandle, "Plana Native Companion");
        SetLayeredWindowAttributes(WindowHandle, TransparentColor, 255, LwaColorKey);
    }

    public void Show() => ShowWindow(WindowHandle, SwShowNoActivate);
    public void Hide() => ShowWindow(WindowHandle, SwHide);
    public void Close()
    {
        _state = Snapshot();
        DestroyWindow(WindowHandle);
    }

    public void WatchSettings(string settingsPath)
    {
        _settingsPath = settingsPath;
        var directory = Path.GetDirectoryName(settingsPath)!;
        Directory.CreateDirectory(directory);
        _settingsLastWriteUtc = File.Exists(settingsPath) ? File.GetLastWriteTimeUtc(settingsPath) : DateTime.MinValue;
        _settingsTimer = new System.Threading.Timer(_ => CheckSettingsChanged(), null, 500, 500);
    }

    public void InitializeRenderer(string rendererDirectory, DesktopSettings settings, IReadOnlyList<NativeActionEntry> actions)
    {
        _actions = actions.ToDictionary(action => action.Id, StringComparer.OrdinalIgnoreCase);
        _interactionBindings = new Dictionary<string, string>(settings.InteractionBindings, StringComparer.OrdinalIgnoreCase);
        _isChinese = settings.UiCulture.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
        _currentSettings = settings;
        var renderer = new WebViewCompositionHost(WindowHandle);
        try
        {
            renderer.WebMessageReceived += OnRendererMessageReceived;
            renderer.CursorChanged += (_, _) => SetCursor(LoadCursor(0, new nint(32512)));
            renderer.Initialize(rendererDirectory, settings, actions);
            _renderer = renderer;
        }
        catch
        {
            renderer.Dispose();
            throw;
        }
        GetClientRect(WindowHandle, out var rect);
        _renderer.Resize(rect.Width, rect.Height);
        InvalidateRect(WindowHandle, 0, true);
    }

    public void Apply(CompanionSurfaceState state)
    {
        _state = state with { Scale = Math.Clamp(state.Scale, 0.5, 2.0) };
        var width = Math.Max(160, (int)Math.Round(state.Width * _state.Scale));
        var height = Math.Max(240, (int)Math.Round(state.Height * _state.Scale));
        var x = state.Left is null ? GetSystemMetrics(0) - width - 48 : (int)Math.Round(state.Left.Value);
        var y = state.Top is null ? GetSystemMetrics(1) - height - 72 : (int)Math.Round(state.Top.Value);
        var insertAfter = state.AlwaysOnTop ? new nint(-1) : new nint(-2);
        SetWindowPos(WindowHandle, insertAfter, x, y, width, height, SwpNoActivate | SwpShowWindow);
        InvalidateRect(WindowHandle, 0, true);
    }

    public CompanionSurfaceState Snapshot()
    {
        if (!GetWindowRect(WindowHandle, out var rect)) return _state;
        return _state with { Left = rect.Left, Top = rect.Top };
    }

    public void RunMessageLoop()
    {
        while (true)
        {
            var result = GetMessage(out var message, 0, 0, 0);
            if (result == 0) return;
            if (result < 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            TranslateMessage(ref message);
            DispatchMessage(ref message);
        }
    }

    private nint WindowProc(nint window, uint message, nint wParam, nint lParam)
    {
        switch (message)
        {
            case WmPaint:
                Paint(window);
                return 0;
            case WmNcHitTest:
                return HitTest(lParam);
            case WmSetCursor:
                SetCursor(LoadCursor(0, new nint(32512)));
                return 1;
            case WmDpiChanged:
                var suggested = Marshal.PtrToStructure<Rect>(lParam);
                SetWindowPos(window, 0, suggested.Left, suggested.Top, suggested.Width, suggested.Height, SwpNoActivate);
                return 0;
            case WmReloadSettings:
                ReloadSettings();
                return 0;
            case 0x0005: // WM_SIZE
                _renderer?.Resize(unchecked((ushort)(long)lParam), unchecked((ushort)((long)lParam >> 16)));
                return 0;
            case 0x0200: // WM_MOUSEMOVE
            case 0x0203: // WM_LBUTTONDBLCLK
            case 0x0204: // WM_RBUTTONDOWN
            case 0x0205: // WM_RBUTTONUP
            case 0x020A: // WM_MOUSEWHEEL
                UpdateNativeDrag((nuint)wParam);
                _renderer?.SendMouseInput(message, (nuint)wParam, lParam);
                SetCursor(LoadCursor(0, new nint(32512)));
                return 0;
            case 0x0201: // WM_LBUTTONDOWN
                SetCapture(WindowHandle);
                BeginNativeDragCandidate(lParam);
                _renderer?.SendMouseInput(message, (nuint)wParam, lParam);
                SetCursor(LoadCursor(0, new nint(32512)));
                return 0;
            case 0x0202: // WM_LBUTTONUP
                _renderer?.SendMouseInput(message, (nuint)wParam, lParam);
                ReleaseCapture();
                _dragCandidate = false;
                _state = Snapshot();
                SetCursor(LoadCursor(0, new nint(32512)));
                return 0;
            case WmDestroy:
                PostQuitMessage(0);
                return 0;
            default:
                return DefWindowProc(window, message, wParam, lParam);
        }
    }

    private void BeginNativeDragCandidate(nint lParam)
    {
        var clientY = unchecked((short)((long)lParam >> 16));
        if (clientY <= (_toolsCollapsed ? 43 : 76)) return;
        _dragCandidate = GetCursorPos(out _dragStartCursor) && GetWindowRect(WindowHandle, out _dragStartWindow);
    }

    private void UpdateNativeDrag(nuint mouseKeys)
    {
        if (!_dragCandidate || (mouseKeys & 0x0001) == 0 || !GetCursorPos(out var current)) return;
        var deltaX = current.X - _dragStartCursor.X;
        var deltaY = current.Y - _dragStartCursor.Y;
        if (Math.Abs(deltaX) < 4 && Math.Abs(deltaY) < 4) return;
        var insertAfter = _state.AlwaysOnTop ? new nint(-1) : new nint(-2);
        SetWindowPos(WindowHandle, insertAfter, _dragStartWindow.Left + deltaX, _dragStartWindow.Top + deltaY, 0, 0, SwpNoSize | SwpNoActivate);
    }

    private void CheckSettingsChanged()
    {
        if (_settingsPath is null || !File.Exists(_settingsPath)) return;
        var lastWrite = File.GetLastWriteTimeUtc(_settingsPath);
        if (lastWrite == _settingsLastWriteUtc) return;
        _settingsLastWriteUtc = lastWrite;
        PostMessage(WindowHandle, WmReloadSettings, 0, 0);
    }

    private void ReloadSettings()
    {
        if (_settingsPath is null || !File.Exists(_settingsPath)) return;
        try
        {
            var settings = new DesktopSettingsStore(_settingsPath).LoadAsync().GetAwaiter().GetResult();
            _interactionBindings = new Dictionary<string, string>(settings.InteractionBindings, StringComparer.OrdinalIgnoreCase);
            _isChinese = settings.UiCulture.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
            _currentSettings = settings;
            RefreshUserActions(settings);
            Apply(new CompanionSurfaceState(settings.Left, settings.Top, settings.Width, settings.Height, settings.Scale, settings.AlwaysOnTop));
            if (_renderer is not null) _ = RefreshToolbarAsync(settings);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            // Atomic replacement can briefly race the watcher; the following event retries it.
        }
    }

    private async Task RefreshToolbarAsync(DesktopSettings settings)
    {
        try
        {
            await _renderer!.ConfigureToolbarAsync(settings, _actions.Values.ToArray());
        }
        catch
        {
            // A renderer refresh failure must never block the Win32 message loop.
        }
    }

    private void RefreshUserActions(DesktopSettings settings)
    {
        var actions = _actions.Values.Where(action => !action.Id.StartsWith("user.action.", StringComparison.OrdinalIgnoreCase)
            && !action.Id.StartsWith("user.launcher.", StringComparison.OrdinalIgnoreCase)).ToList();
        actions.AddRange(settings.UserActions.Select(action => new NativeActionEntry(
            $"user.action.{action.Id}", action.Name,
            new ActionDefinition($"user.action.{action.Id}", action.Name, action.Kind, action.Parameters, Capability(action.Kind)), null)));
        actions.AddRange(settings.ProjectLaunchers.Select(project =>
        {
            var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["executable"] = project.Executable };
            for (var index = 0; index < project.Arguments.Count; index++) parameters[$"arg.{index}"] = project.Arguments[index].Replace("{folder}", project.Folder, StringComparison.OrdinalIgnoreCase);
            return new NativeActionEntry($"user.launcher.{project.Id}", project.Name,
                new ActionDefinition($"user.launcher.{project.Id}", project.Name, ActionKinds.LaunchProcess, parameters, Capability(ActionKinds.LaunchProcess)), project.Folder);
        }));
        _actions = actions.ToDictionary(action => action.Id, StringComparer.OrdinalIgnoreCase);
    }

    private static HashSet<string> Capability(string kind) => [kind switch
    {
        ActionKinds.OpenUrl => Capabilities.OpenUrl,
        ActionKinds.OpenFile => Capabilities.OpenFile,
        ActionKinds.OpenFolder => Capabilities.OpenFolder,
        ActionKinds.RunCommand => Capabilities.RunCommand,
        ActionKinds.RunScript => Capabilities.RunScript,
        _ => Capabilities.LaunchProcess,
    }];

    private void Paint(nint window)
    {
        var device = BeginPaint(window, out var paint);
        GetClientRect(window, out var rect);
        var transparentBrush = CreateSolidBrush(TransparentColor);
        FillRect(device, ref rect, transparentBrush);
        DeleteObject(transparentBrush);

        if (_renderer is not null)
        {
            EndPaint(window, ref paint);
            return;
        }

        var bodyBrush = CreateSolidBrush(0x00E6C7A7);
        var oldBrush = SelectObject(device, bodyBrush);
        var margin = Math.Max(12, rect.Width / 14);
        RoundRect(device, margin, margin, rect.Right - margin, rect.Bottom - margin, 48, 48);
        SelectObject(device, oldBrush);
        DeleteObject(bodyBrush);

        SetBkMode(device, 1);
        SetTextColor(device, 0x00604035);
        var label = "Native Companion\ntransparent Win32 surface";
        DrawText(device, label, label.Length, ref rect, 0x00000001 | 0x00000004 | 0x00000020);
        EndPaint(window, ref paint);
    }

    private nint HitTest(nint lParam)
    {
        GetWindowRect(WindowHandle, out var rect);
        var x = unchecked((short)(long)lParam) - rect.Left;
        var y = unchecked((short)((long)lParam >> 16)) - rect.Top;
        var toolbarWidth = Math.Min(Math.Max(0, rect.Width - 12), 340);
        var toolbarLeft = (rect.Width - toolbarWidth) / 2;
        var toolbarBottom = _toolsCollapsed ? 40 : 67;
        if (y >= 5 && y <= toolbarBottom && x >= toolbarLeft && x <= toolbarLeft + toolbarWidth) return HtClient;
        var modelHeight = Math.Max(1, rect.Height - 76);
        var normalizedX = x / (double)Math.Max(1, rect.Width);
        var normalizedY = (y - 76) / (double)modelHeight;
        var inside = PointInPolygon(normalizedX, normalizedY);
        return inside ? HtClient : HtTransparent;
    }

    private static bool PointInPolygon(double x, double y)
    {
        var inside = false;
        for (int current = 0, previous = CharacterHitPolygon.Length - 1; current < CharacterHitPolygon.Length; previous = current++)
        {
            var a = CharacterHitPolygon[current];
            var b = CharacterHitPolygon[previous];
            if ((a.Y > y) != (b.Y > y) && x < (b.X - a.X) * (y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
        }
        return inside;
    }


    private async void OnRendererMessageReceived(object? sender, string json)
    {
        try
        {
            using var message = JsonDocument.Parse(json);
            switch (message.RootElement.GetProperty("type").GetString())
            {
                case "drag":
                    // Native mouse capture owns dragging; renderer drag messages are informational.
                    break;
                case "interaction":
                    var interaction = message.RootElement.TryGetProperty("interaction", out var interactionElement)
                        ? interactionElement.GetString() ?? "click"
                        : "click";
                    await ExecuteInteractionAsync(interaction);
                    break;
                case "context":
                    ShowContextMenu();
                    break;
                case "toolbarSettings":
                    Process.Start(new ProcessStartInfo("plana://settings") { UseShellExecute = true });
                    break;
                case "aiPrompt":
                    var prompt = message.RootElement.TryGetProperty("prompt", out var promptElement) ? promptElement.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(prompt)) await RunAiPromptAsync(prompt);
                    break;
                case "toolbarGroupChanged":
                    var groupId = message.RootElement.TryGetProperty("groupId", out var groupElement) ? groupElement.GetString() : null;
                    await SaveSelectedGroupAsync(groupId);
                    break;
                case "toolbarCollapsed":
                    _toolsCollapsed = message.RootElement.TryGetProperty("collapsed", out var collapsedElement) && collapsedElement.GetBoolean();
                    break;
                case "toolbarRun":
                    var actionId = message.RootElement.TryGetProperty("actionId", out var actionElement) ? actionElement.GetString() : null;
                    if (actionId is not null && _actions.TryGetValue(actionId, out var action)) await ExecuteActionAsync(action);
                    break;
            }
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            // Renderer messages are local and non-fatal; ignore malformed payloads.
        }
    }

    private async Task RunAiPromptAsync(string prompt)
    {
        try
        {
            var response = await AiChatService.SendAsync(_currentSettings, prompt, CancellationToken.None);
            if (response.Changed && _settingsPath is not null)
            {
                await new DesktopSettingsStore(_settingsPath).SaveAsync(_currentSettings);
                RefreshUserActions(_currentSettings);
            }
            await (_renderer?.ExecuteScriptAsync($"window.plana.showAiResponse({JsonSerializer.Serialize(response.Message)}, false)") ?? Task.CompletedTask);
        }
        catch (Exception exception)
        {
            await (_renderer?.ExecuteScriptAsync($"window.plana.showAiResponse({JsonSerializer.Serialize(exception.Message)}, true)") ?? Task.CompletedTask);
        }
    }

    private async Task SaveSelectedGroupAsync(string? groupId)
    {
        if (_settingsPath is null) return;
        var settings = await new DesktopSettingsStore(_settingsPath).LoadAsync();
        settings.SelectedToolGroupId = string.IsNullOrWhiteSpace(groupId) ? null : groupId;
        await new DesktopSettingsStore(_settingsPath).SaveAsync(settings);
    }

    private async Task ExecuteInteractionAsync(string interaction)
    {
        if (!_interactionBindings.TryGetValue(interaction, out var actionId)) return;
        if (actionId.Equals("builtin.companion.interact", StringComparison.OrdinalIgnoreCase))
        {
            await (_renderer?.ExecuteScriptAsync("window.plana.playAnimation('random')") ?? Task.CompletedTask);
            return;
        }
        if (_actions.TryGetValue(actionId, out var action)) await ExecuteActionAsync(action);
    }

    private void ShowContextMenu()
    {
        _contextMenu ??= CreateContextMenu();
        _contextMenu.Show(Forms.Cursor.Position);
    }

    private Forms.ContextMenuStrip CreateContextMenu()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(_isChinese ? "操作" : "Actions", null, (_, _) => OpenActionSearch(null));
        menu.Items.Add(_isChinese ? "设置" : "Settings", null, (_, _) => Process.Start(new ProcessStartInfo("plana://settings") { UseShellExecute = true }));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_isChinese ? "隐藏" : "Hide", null, (_, _) => Hide());
        return menu;
    }

    private async Task<ActionResult> ExecuteActionAsync(NativeActionEntry entry)
    {
        if (entry.Definition.Kind != ActionKinds.PluginInvoke) return NativeActionExecutor.Execute(entry);
        var action = entry.Definition;
        return await _pluginRuntime.InvokeAsync(
            action.Parameters["pluginId"],
            action.Parameters["actionId"],
            action.Capabilities,
            (request, token) => NativePluginBroker.ExecuteAsync(request, entry.WorkingDirectory, token));
    }

    private static void OpenActionSearch(string? query)
    {
        var uri = "plana://actions";
        if (!string.IsNullOrWhiteSpace(query)) uri += $"?query={Uri.EscapeDataString(query)}";
        Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _renderer?.Dispose();
        _contextMenu?.Dispose();
        _settingsTimer?.Dispose();
        if (WindowHandle != 0) DestroyWindow(WindowHandle);
        WindowHandle = 0;
    }

    private delegate nint WndProc(nint window, uint message, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WndClass
    {
        public uint Style;
        public nint WindowProcedure;
        public int ClassExtra;
        public int WindowExtra;
        public nint Instance;
        public nint Icon;
        public nint Cursor;
        public nint Background;
        public string? MenuName;
        public string ClassName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Message { public nint Window; public uint Id; public nuint WParam; public nint LParam; public uint Time; public Point Position; public uint Private; }
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X; public int Y; }
    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left; public int Top; public int Right; public int Bottom;
        public readonly int Width => Right - Left;
        public readonly int Height => Bottom - Top;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct PaintStruct { public nint Device; public bool Erase; public Rect Paint; public bool Restore; public bool Update; [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] Reserved; }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)] private static extern ushort RegisterClass(ref WndClass windowClass);
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)] private static extern nint CreateWindowEx(uint exStyle, string className, string title, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
    [DllImport("user32.dll", EntryPoint = "SetWindowTextW", CharSet = CharSet.Unicode)] private static extern bool SetWindowText(nint window, string title);
    [DllImport("user32.dll")] private static extern nint DefWindowProc(nint window, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(nint window);
    [DllImport("user32.dll")] private static extern bool ShowWindow(nint window, int command);
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern nint SetCapture(nint window);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] private static extern bool PostMessage(nint window, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetLayeredWindowAttributes(nint window, uint colorKey, byte alpha, uint flags);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(nint window, nint insertAfter, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern int GetMessage(out Message message, nint window, uint min, uint max);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref Message message);
    [DllImport("user32.dll")] private static extern nint DispatchMessage(ref Message message);
    [DllImport("user32.dll")] private static extern void PostQuitMessage(int exitCode);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint window, out Rect rect);
    [DllImport("user32.dll")] private static extern bool GetClientRect(nint window, out Rect rect);
    [DllImport("user32.dll")] private static extern bool InvalidateRect(nint window, nint rect, bool erase);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] private static extern bool SetProcessDpiAwarenessContext(nint value);
    [DllImport("user32.dll")] private static extern nint LoadCursor(nint instance, nint cursor);
    [DllImport("user32.dll")] private static extern nint SetCursor(nint cursor);
    [DllImport("user32.dll")] private static extern nint BeginPaint(nint window, out PaintStruct paint);
    [DllImport("user32.dll")] private static extern bool EndPaint(nint window, ref PaintStruct paint);
    [DllImport("user32.dll")] private static extern int FillRect(nint device, ref Rect rect, nint brush);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int DrawText(nint device, string text, int length, ref Rect rect, uint format);
    [DllImport("gdi32.dll")] private static extern uint SetTextColor(nint device, uint color);
    [DllImport("gdi32.dll")] private static extern int SetBkMode(nint device, int mode);
    [DllImport("gdi32.dll")] private static extern nint CreateSolidBrush(uint color);
    [DllImport("gdi32.dll")] private static extern nint SelectObject(nint device, nint value);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint value);
    [DllImport("gdi32.dll")] private static extern bool RoundRect(nint device, int left, int top, int right, int bottom, int width, int height);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? moduleName);
}
