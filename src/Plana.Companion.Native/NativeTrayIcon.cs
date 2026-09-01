using System.Diagnostics;
using Plana.Core.Companion;
using Plana.Core.Settings;
using Forms = System.Windows.Forms;

namespace Plana.Companion.Native;

internal sealed class NativeTrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _icon;

    public NativeTrayIcon(ICompanionController companion, DesktopSettings settings)
    {
        var chinese = settings.UiCulture.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(chinese ? "显示桌宠" : "Show companion", null, (_, _) => companion.Show());
        menu.Items.Add(chinese ? "隐藏桌宠" : "Hide companion", null, (_, _) => companion.Hide());
        if (companion is GodotCompanionWindow godot)
        {
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add(chinese ? "摸头" : "Head pat", null, (_, _) => godot.Perform(new CharacterPerformanceIntent(CharacterEmotion.Happy, CharacterGesture.HeadPat)));
            menu.Items.Add(chinese ? "爱心表情" : "Affection", null, (_, _) => godot.Perform(new CharacterPerformanceIntent(CharacterEmotion.Affectionate)));
            var passThrough = new Forms.ToolStripMenuItem(chinese ? "鼠标穿透" : "Mouse pass-through") { CheckOnClick = true };
            passThrough.CheckedChanged += (_, _) => godot.SetPassThrough(passThrough.Checked);
            menu.Items.Add(passThrough);
        }
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(chinese ? "操作" : "Actions", null, (_, _) => Open("plana://actions"));
        menu.Items.Add(chinese ? "设置" : "Settings", null, (_, _) => Open("plana://settings"));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(chinese ? "退出" : "Exit", null, (_, _) => companion.Close());

        _icon = new Forms.NotifyIcon
        {
            Text = "Plana Desktop",
            Icon = System.Drawing.SystemIcons.Application,
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => companion.Show();
    }

    private static void Open(string uri) => Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
