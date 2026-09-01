using System.Runtime.InteropServices;
using System.Drawing.Drawing2D;
using Forms = System.Windows.Forms;

namespace Plana.Companion.Native;

internal sealed class CompanionSpeechBubble : Forms.Form
{
    private readonly Forms.Label _text;
    private readonly Forms.Timer _dismissTimer;
    private bool _isError;

    public CompanionSpeechBubble()
    {
        FormBorderStyle = Forms.FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = Forms.FormStartPosition.Manual;
        BackColor = Color.FromArgb(14, 20, 34);
        Padding = new Padding(16, 12, 16, 12);
        MinimumSize = new Size(260, 72);

        _text = new Forms.Label
        {
            Dock = Forms.DockStyle.Fill,
            BackColor = BackColor,
            ForeColor = Color.FromArgb(246, 248, 255),
            Font = new Font("Segoe UI", 10.5f, FontStyle.Regular),
            TextAlign = ContentAlignment.MiddleLeft,
            UseCompatibleTextRendering = false,
        };
        Controls.Add(_text);

        _dismissTimer = new Forms.Timer();
        _dismissTimer.Tick += (_, _) => { _dismissTimer.Stop(); Hide(); };
        Shown += (_, _) => ApplyWindowShape();
        Resize += (_, _) => ApplyWindowShape();
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            const int WsExTransparent = 0x00000020;
            const int WsExToolWindow = 0x00000080;
            const int WsExNoActivate = 0x08000000;
            var parameters = base.CreateParams;
            parameters.ExStyle |= WsExTransparent | WsExToolWindow | WsExNoActivate;
            return parameters;
        }
    }

    public void ShowMessage(string text, bool isError)
    {
        _isError = isError;
        _text.Text = text;
        var proposed = new Size(Math.Max(220, ClientSize.Width - Padding.Horizontal), 500);
        var measured = TextRenderer.MeasureText(text, _text.Font, proposed, TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
        Height = Math.Clamp(measured.Height + Padding.Vertical + 8, 72, 180);
        Invalidate();
        if (!Visible) Show();
        _dismissTimer.Stop();
        _dismissTimer.Interval = isError ? 14000 : 22000;
        _dismissTimer.Start();
    }

    public void ClearMessage()
    {
        _dismissTimer.Stop();
        Hide();
    }

    protected override void OnPaint(Forms.PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(_isError ? Color.FromArgb(210, 74, 86) : Color.FromArgb(92, 119, 198));
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }

    private void ApplyWindowShape()
    {
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            var preference = 2;
            DwmSetWindowAttribute(Handle, 33, ref preference, sizeof(int));
            return;
        }
        using var path = RoundedPath(ClientRectangle, 12);
        Region?.Dispose();
        Region = new Region(path);
    }

    private static GraphicsPath RoundedPath(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _dismissTimer.Dispose();
        base.Dispose(disposing);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);
}
