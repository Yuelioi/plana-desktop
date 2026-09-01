using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;

namespace Plana.Companion.Native;

internal sealed class GlobalHotkey : Forms.NativeWindow, IDisposable
{
    private const int HotkeyId = 0x504C;
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private readonly Action _callback;
    private bool _registered;

    public GlobalHotkey(Action callback)
    {
        _callback = callback;
        CreateHandle(new Forms.CreateParams { Caption = "Plana.GlobalHotkey" });
        _registered = RegisterHotKey(Handle, HotkeyId, ModControl | ModAlt, (uint)Keys.Space);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotkey && m.WParam == HotkeyId) _callback();
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        if (_registered) UnregisterHotKey(Handle, HotkeyId);
        _registered = false;
        DestroyHandle();
    }

    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(nint window, int id);
}
