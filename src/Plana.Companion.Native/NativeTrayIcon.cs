using System.Diagnostics;
using Plana.Core.Companion;
using Plana.Core.Settings;
using Forms = System.Windows.Forms;

namespace Plana.Companion.Native;

internal sealed class NativeTrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ContextMenuStrip _menu;
    private readonly GodotCompanionWindow? _godot;
    private readonly SynchronizationContext _uiContext;

    public NativeTrayIcon(ICompanionController companion, DesktopSettings settings)
    {
        var chinese = settings.UiCulture.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
        _uiContext = SynchronizationContext.Current ?? new Forms.WindowsFormsSynchronizationContext();
        _menu = new Forms.ContextMenuStrip();
        _menu.Items.Add(chinese ? "显示桌宠" : "Show companion", null, (_, _) => companion.Show());
        _menu.Items.Add(chinese ? "隐藏桌宠" : "Hide companion", null, (_, _) => companion.Hide());
        if (companion is GodotCompanionWindow godot)
        {
            _godot = godot;
            _godot.ContextRequested += OnContextRequested;
            _menu.Items.Add(new Forms.ToolStripSeparator());
            _menu.Items.Add(chinese ? "摸头" : "Head pat", null, (_, _) => godot.Perform(new CharacterPerformanceIntent(CharacterEmotion.Happy, CharacterGesture.HeadPat)));
            _menu.Items.Add(chinese ? "爱心表情" : "Affection", null, (_, _) => godot.Perform(new CharacterPerformanceIntent(CharacterEmotion.Affectionate)));
            var passThrough = new Forms.ToolStripMenuItem(chinese ? "鼠标穿透" : "Mouse pass-through") { CheckOnClick = true };
            passThrough.CheckedChanged += (_, _) => godot.SetPassThrough(passThrough.Checked);
            _menu.Items.Add(passThrough);
        }
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add(chinese ? "操作" : "Actions", null, (_, _) => Open("plana://actions"));
        _menu.Items.Add(chinese ? "设置" : "Settings", null, (_, _) => Open("plana://settings"));
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add(chinese ? "退出" : "Exit", null, (_, _) => companion.Close());

        _icon = new Forms.NotifyIcon
        {
            Text = "Plana Desktop",
            Icon = System.Drawing.SystemIcons.Application,
            ContextMenuStrip = _menu,
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => companion.Show();
    }

    private static void Open(string uri) => Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });

    private void OnContextRequested(object? sender, EventArgs args) =>
        _uiContext.Post(_ => _menu.Show(Forms.Cursor.Position), null);

    public void Dispose()
    {
        _icon.Visible = false;
        if (_godot is not null) _godot.ContextRequested -= OnContextRequested;
        _menu.Dispose();
        _icon.Dispose();
    }
}
