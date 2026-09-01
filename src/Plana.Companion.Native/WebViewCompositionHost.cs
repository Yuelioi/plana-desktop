using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Plana.Core.Settings;
using Windows.UI.Composition;
using Windows.UI.Composition.Desktop;

namespace Plana.Companion.Native;

internal sealed class WebViewCompositionHost : IDisposable
{
    public event EventHandler<string>? WebMessageReceived;
    public event EventHandler? CursorChanged;
    private readonly nint _parentWindow;
    private Compositor? _compositor;
    private DesktopWindowTarget? _desktopTarget;
    private ContainerVisual? _rootVisual;
    private ContainerVisual? _webViewVisual;
    private CoreWebView2CompositionController? _controller;
    private nint _dispatcherQueueController;

    public WebViewCompositionHost(nint parentWindow)
    {
        _parentWindow = parentWindow;
    }

    public void Initialize(string rendererDirectory, DesktopSettings settings, IReadOnlyList<NativeActionEntry> actions)
    {
        EnsureDispatcherQueue();
        _compositor = new Compositor();
        if (!WinRT.ComWrappersSupport.TryUnwrapObject(_compositor, out var compositorReference))
        {
            throw new InvalidOperationException("Could not unwrap the WinRT compositor.");
        }
        var interop = compositorReference.AsInterface<ICompositorDesktopInterop>();
        interop.CreateDesktopWindowTarget(_parentWindow, true, out var target);
        _desktopTarget = WinRT.MarshalInspectable<DesktopWindowTarget>.FromAbi(target);
        Marshal.Release(target);

        _rootVisual = _compositor.CreateContainerVisual();
        _webViewVisual = _compositor.CreateContainerVisual();
        _rootVisual.Children.InsertAtTop(_webViewVisual);
        _desktopTarget.Root = _rootVisual;

        var environment = WaitWithMessagePump(CoreWebView2Environment.CreateAsync().AsTask());
        _controller = WaitWithMessagePump(environment.CreateCoreWebView2CompositionControllerAsync(
            CoreWebView2ControllerWindowReference.CreateFromWindowHandle((ulong)_parentWindow)).AsTask());
        _controller.RootVisualTarget = _webViewVisual;
        _controller.CursorChanged += (_, _) => CursorChanged?.Invoke(this, EventArgs.Empty);
        _controller.DefaultBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
        _controller.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "plana.local",
            rendererDirectory,
            CoreWebView2HostResourceAccessKind.DenyCors);
        _controller.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        _controller.CoreWebView2.Settings.AreDevToolsEnabled = false;
        _controller.CoreWebView2.WebMessageReceived += (_, args) => WebMessageReceived?.Invoke(this, args.WebMessageAsJson);
        _controller.CoreWebView2.NavigationCompleted += async (_, _) =>
        {
            await ConfigureToolbarAsync(settings, actions);
        };
        _controller.CoreWebView2.Navigate("https://plana.local/index.html?native=1");
    }

    public void Resize(int width, int height)
    {
        if (_controller is null || _webViewVisual is null) return;
        _controller.Bounds = new Windows.Foundation.Rect(0, 0, Math.Max(1, width), Math.Max(1, height));
        _webViewVisual.Size = new System.Numerics.Vector2(Math.Max(1, width), Math.Max(1, height));
    }

    public void SendMouseInput(uint message, nuint wParam, nint lParam)
    {
        if (_controller is null) return;
        if (message == 0x0201) _controller.MoveFocus(CoreWebView2MoveFocusReason.Programmatic);
        var kind = message switch
        {
            0x0200 => CoreWebView2MouseEventKind.Move,
            0x0201 => CoreWebView2MouseEventKind.LeftButtonDown,
            0x0202 => CoreWebView2MouseEventKind.LeftButtonUp,
            0x0203 => CoreWebView2MouseEventKind.LeftButtonDoubleClick,
            0x0204 => CoreWebView2MouseEventKind.RightButtonDown,
            0x0205 => CoreWebView2MouseEventKind.RightButtonUp,
            0x020A => CoreWebView2MouseEventKind.Wheel,
            _ => CoreWebView2MouseEventKind.Move,
        };
        var x = unchecked((short)(long)lParam);
        var y = unchecked((short)((long)lParam >> 16));
        var mouseData = message == 0x020A ? unchecked((uint)((ulong)wParam >> 16)) : 0;
        _controller.SendMouseInput(
            kind,
            (CoreWebView2MouseEventVirtualKeys)((uint)wParam & 0xFFFF),
            mouseData,
            new Windows.Foundation.Point(x, y));
    }

    public Task ExecuteScriptAsync(string script) =>
        _controller?.CoreWebView2.ExecuteScriptAsync(script).AsTask() ?? Task.CompletedTask;

    public Task ConfigureToolbarAsync(DesktopSettings settings, IReadOnlyList<NativeActionEntry> actions)
    {
        var config = BuildToolbarConfig(settings, actions);
        return ExecuteScriptAsync($"window.plana.configureToolbar({JsonSerializer.Serialize(config)})");
    }

    private static object BuildToolbarConfig(DesktopSettings settings, IReadOnlyList<NativeActionEntry> entries)
    {
        var actions = entries.Select(action => new { id = action.Id, name = action.Name }).ToArray();
        return new
        {
            groupPlaceholder = AppLanguage(settings, "快捷工具", "Quick tools"),
            actionPlaceholder = AppLanguage(settings, "选择操作", "Choose an action"),
            chatPlaceholder = AppLanguage(settings, "说些什么吧…", "Say something…"),
            settingsLabel = AppLanguage(settings, "设置", "Settings"),
            collapseLabel = AppLanguage(settings, "折叠工具组", "Collapse tools"),
            runLabel = AppLanguage(settings, "运行", "Run"),
            isChinese = settings.UiCulture.StartsWith("zh", StringComparison.OrdinalIgnoreCase),
            selectedGroupId = settings.SelectedToolGroupId,
            groups = settings.ToolGroups.Select(group => new { id = group.Id, name = group.Name, actionIds = group.ActionIds }).ToArray(),
            actions,
        };
    }

    private static string AppLanguage(DesktopSettings settings, string chinese, string english) =>
        settings.UiCulture.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? chinese : english;

    public void Dispose()
    {
        if (_controller is not null)
        {
            _controller.RootVisualTarget = null;
            _controller.Close();
        }
        _webViewVisual?.Dispose();
        _rootVisual?.Dispose();
        _desktopTarget?.Dispose();
        _compositor?.Dispose();
        if (_dispatcherQueueController != 0) Marshal.Release(_dispatcherQueueController);
    }

    private void EnsureDispatcherQueue()
    {
        var options = new DispatcherQueueOptions
        {
            Size = Marshal.SizeOf<DispatcherQueueOptions>(),
            ThreadType = 2,
            ApartmentType = 2,
        };
        var result = CreateDispatcherQueueController(options, out _dispatcherQueueController);
        if (result < 0) Marshal.ThrowExceptionForHR(result);
    }

    private static T WaitWithMessagePump<T>(Task<T> task)
    {
        while (!task.IsCompleted)
        {
            while (PeekMessage(out var message, 0, 0, 0, 1))
            {
                TranslateMessage(ref message);
                DispatchMessage(ref message);
            }
            MsgWaitForMultipleObjectsEx(0, 0, 16, 0x04FF, 0x0004);
        }
        return task.GetAwaiter().GetResult();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DispatcherQueueOptions
    {
        public int Size;
        public int ThreadType;
        public int ApartmentType;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        public nint Window;
        public uint Message;
        public nuint WParam;
        public nint LParam;
        public uint Time;
        public int X;
        public int Y;
        public uint Private;
    }

    [DllImport("CoreMessaging.dll")]
    private static extern int CreateDispatcherQueueController(
        DispatcherQueueOptions options,
        out nint dispatcherQueueController);
    [DllImport("user32.dll")] private static extern bool PeekMessage(out NativeMessage message, nint window, uint min, uint max, uint remove);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref NativeMessage message);
    [DllImport("user32.dll")] private static extern nint DispatchMessage(ref NativeMessage message);
    [DllImport("user32.dll")] private static extern uint MsgWaitForMultipleObjectsEx(uint count, nint handles, uint milliseconds, uint wakeMask, uint flags);

    [ComImport]
    [Guid("29E691FA-4567-4DCA-B319-D0F207EB6807")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ICompositorDesktopInterop
    {
        void CreateDesktopWindowTarget(nint window, [MarshalAs(UnmanagedType.Bool)] bool isTopmost, out nint target);
        void EnsureOnThread(uint threadId);
    }
}
