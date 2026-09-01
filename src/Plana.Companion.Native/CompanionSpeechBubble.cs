using System.Diagnostics;
using System.Drawing.Drawing2D;
using Forms = System.Windows.Forms;

namespace Plana.Companion.Native;

internal sealed class CompanionSpeechBubble : Forms.Form
{
    private readonly Forms.Label _text;
    private readonly Forms.Button _close;
    private readonly Forms.Timer _thinkingTimer;
    private readonly Stopwatch _thinkingWatch = new();
    private string _thinkingPrefix = string.Empty;
    private bool _isError;
    private const int TailHeight = 10;

    public CompanionSpeechBubble()
    {
        FormBorderStyle = Forms.FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = Forms.FormStartPosition.Manual;
        BackColor = Color.FromArgb(14, 20, 34);
        Padding = new Padding(16, 12, 44, 12 + TailHeight);
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

        _close = new Forms.Button
        {
            Width = 28,
            Height = 28,
            FlatStyle = Forms.FlatStyle.Flat,
            BackColor = BackColor,
            ForeColor = Color.FromArgb(180, 190, 214),
            Font = new Font("Segoe UI Symbol", 10f),
            Text = "×",
            AccessibleName = "Close response",
            TabStop = false,
        };
        _close.FlatAppearance.BorderSize = 0;
        _close.Click += (_, _) => ClearMessage();
        Controls.Add(_close);

        _thinkingTimer = new Forms.Timer { Interval = 1000 };
        _thinkingTimer.Tick += (_, _) => RenderThinking();
        Shown += (_, _) => ApplyWindowShape();
        Resize += (_, _) => { LayoutCloseButton(); ApplyWindowShape(); };
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            const int WsExToolWindow = 0x00000080;
            const int WsExNoActivate = 0x08000000;
            var parameters = base.CreateParams;
            parameters.ExStyle |= WsExToolWindow | WsExNoActivate;
            return parameters;
        }
    }

    public void ShowThinking(bool chinese)
    {
        _isError = false;
        _close.Visible = false;
        _thinkingPrefix = chinese ? "正在思考" : "Thinking";
        _thinkingWatch.Restart();
        _thinkingTimer.Start();
        RenderThinking();
    }

    public void ShowMessage(string text, bool isError)
    {
        _isError = isError;
        _thinkingTimer.Stop();
        _thinkingWatch.Stop();
        _close.Visible = true;
        _text.Text = text;
        var proposed = new Size(Math.Max(220, ClientSize.Width - Padding.Horizontal), 500);
        var measured = TextRenderer.MeasureText(text, _text.Font, proposed, TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
        Height = Math.Clamp(measured.Height + Padding.Vertical + 8, 72, 180);
        Invalidate();
        if (!Visible) Show();
        LayoutCloseButton();
    }

    public void ClearMessage()
    {
        _thinkingTimer.Stop();
        _thinkingWatch.Stop();
        Hide();
    }

    private void RenderThinking()
    {
        _text.Text = $"{_thinkingPrefix}… {Math.Max(0, (int)_thinkingWatch.Elapsed.TotalSeconds)}s";
        var measured = TextRenderer.MeasureText(_text.Text, _text.Font, new Size(260, 200), TextFormatFlags.NoPadding);
        Height = Math.Clamp(measured.Height + Padding.Vertical + 8, 72, 100);
        if (!Visible) Show();
    }

    private void LayoutCloseButton() => _close.Location = new Point(Math.Max(0, ClientSize.Width - 36), 6);

    protected override void OnPaint(Forms.PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(_isError ? Color.FromArgb(210, 74, 86) : Color.FromArgb(92, 119, 198));
        using var path = BubblePath(new Rectangle(0, 0, Width - 1, Height - 1), 12, TailHeight);
        e.Graphics.DrawPath(pen, path);
    }

    private void ApplyWindowShape()
    {
        using var path = BubblePath(ClientRectangle, 12, TailHeight);
        Region?.Dispose();
        Region = new Region(path);
    }

    private static GraphicsPath BubblePath(Rectangle bounds, int radius, int tailHeight)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        var cardBottom = bounds.Bottom - tailHeight;
        var center = bounds.Left + bounds.Width / 2;
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, cardBottom - diameter, diameter, diameter, 0, 90);
        path.AddLine(bounds.Right - radius, cardBottom, center + 10, cardBottom);
        path.AddLine(center + 10, cardBottom, center, bounds.Bottom);
        path.AddLine(center, bounds.Bottom, center - 10, cardBottom);
        path.AddLine(center - 10, cardBottom, bounds.Left + radius, cardBottom);
        path.AddArc(bounds.Left, cardBottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _thinkingTimer.Dispose();
        base.Dispose(disposing);
    }
}
