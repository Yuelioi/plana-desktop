using System.Runtime.InteropServices;
using System.Drawing.Drawing2D;
using Forms = System.Windows.Forms;

namespace Plana.Companion.Native;

internal sealed class CompanionChatInput : Forms.Form
{
    private readonly Forms.TextBox _input;
    private readonly Forms.Button _send;
    private readonly Forms.Label _placeholder;
    private readonly Func<string, Task> _submit;
    private readonly string _cueText;
    private readonly List<Forms.Button> _toolButtons = [];
    private bool _userEngaged;

    public bool ShouldRemainVisible => _userEngaged && ContainsFocus;

    public CompanionChatInput(
        bool chinese,
        Func<string, Task> submit)
    {
        _submit = submit;
        _cueText = chinese ? "和普拉娜说点什么…" : "Ask Plana…";
        FormBorderStyle = Forms.FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = Forms.FormStartPosition.Manual;
        BackColor = Color.FromArgb(14, 20, 34);
        Padding = new Padding(12, 8, 8, 8);
        Height = 56;
        MinimumSize = new Size(280, 56);

        _input = new Forms.TextBox
        {
            BorderStyle = Forms.BorderStyle.None,
            Location = new Point(14, 19),
            Size = new Size(220, 24),
            Font = new Font("Segoe UI", 10.5f),
            BackColor = BackColor,
            ForeColor = Color.FromArgb(246, 248, 255),
        };
        SetCueBanner(_input.Handle, _cueText);
        _input.HandleCreated += (_, _) => SetCueBanner(_input.Handle, _cueText);
        _input.KeyDown += async (_, args) =>
        {
            _userEngaged = true;
            if (args.KeyCode != Keys.Enter || args.Shift) return;
            args.SuppressKeyPress = true;
            await SubmitAsync();
        };
        _input.MouseDown += (_, _) => _userEngaged = true;

        _send = new Forms.Button
        {
            Size = new Size(36, 36),
            FlatStyle = Forms.FlatStyle.Flat,
            BackColor = Color.FromArgb(78, 111, 216),
            ForeColor = Color.White,
            Font = new Font("Segoe UI Symbol", 11f, FontStyle.Bold),
            Text = "➤",
            AccessibleName = chinese ? "发送消息" : "Send message",
            Cursor = Cursors.Hand,
        };
        _send.FlatAppearance.BorderSize = 0;
        _send.Click += async (_, _) => await SubmitAsync();

        Controls.Add(_input);
        _placeholder = new Forms.Label
        {
            AutoSize = true,
            BackColor = BackColor,
            ForeColor = Color.FromArgb(151, 162, 190),
            Font = new Font("Segoe UI", 10f),
            Location = new Point(14, 20),
            Text = _cueText,
            Cursor = Cursors.IBeam,
        };
        _placeholder.Click += (_, _) => { _userEngaged = true; _input.Focus(); };
        _input.TextChanged += (_, _) => _placeholder.Visible = _input.TextLength == 0;
        Controls.Add(_placeholder);
        Controls.Add(_send);
        Shown += (_, _) => { LayoutControls(); ApplyWindowShape(); SetCueBanner(_input.Handle, _cueText); };
        Resize += (_, _) => { LayoutControls(); ApplyWindowShape(); };
        VisibleChanged += (_, _) => { if (!Visible) _userEngaged = false; };
    }

    public void ConfigureQuickActions(IEnumerable<(string Label, Action Execute)> actions)
    {
        if (InvokeRequired) { BeginInvoke(() => ConfigureQuickActions(actions.ToArray())); return; }
        foreach (var button in _toolButtons) { Controls.Remove(button); button.Dispose(); }
        _toolButtons.Clear();
        foreach (var action in actions.Take(4))
        {
            var button = CreateToolButton(action.Label, action.Execute, Color.FromArgb(38, 55, 96));
            _toolButtons.Add(button);
            Controls.Add(button);
        }
        Height = _toolButtons.Count == 0 ? 56 : 94;
        MinimumSize = new Size(280, Height);
        LayoutControls();
        Invalidate();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            const int CsDropShadow = 0x00020000;
            var parameters = base.CreateParams;
            parameters.ClassStyle |= CsDropShadow;
            return parameters;
        }
    }

    protected override bool ShowWithoutActivation => true;

    private Forms.Button CreateToolButton(string text, Action action, Color color)
    {
        var button = new Forms.Button
        {
            Height = 30,
            FlatStyle = Forms.FlatStyle.Flat,
            BackColor = color,
            ForeColor = Color.FromArgb(218, 225, 244),
            Font = new Font("Segoe UI", 8.5f),
            Text = text,
            AccessibleName = text,
            Cursor = Cursors.Hand,
            TabStop = false,
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(58, 75, 122);
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(38, 50, 82);
        button.Click += (_, _) => action();
        return button;
    }

    private async Task SubmitAsync()
    {
        var text = _input.Text.Trim();
        if (text.Length == 0 || !_send.Enabled) return;
        _send.Enabled = false;
        _input.Enabled = false;
        try
        {
            await _submit(text);
            _input.Clear();
        }
        finally
        {
            _input.Enabled = true;
            _send.Enabled = true;
            _input.Focus();
        }
    }

    protected override void OnPaint(Forms.PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(Color.FromArgb(92, 119, 198));
        using var path = RoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), 12);
        e.Graphics.DrawPath(pen, path);
    }

    private void LayoutControls()
    {
        const int gap = 6;
        var availableWidth = ClientSize.Width - 16 - gap * Math.Max(0, _toolButtons.Count - 1);
        var toolWidth = Math.Clamp(availableWidth / Math.Max(1, _toolButtons.Count), 54, 120);
        var toolsWidth = toolWidth * _toolButtons.Count + gap * Math.Max(0, _toolButtons.Count - 1);
        var toolsLeft = Math.Max(8, (ClientSize.Width - toolsWidth) / 2);
        for (var index = 0; index < _toolButtons.Count; index++)
        {
            var button = _toolButtons[index];
            button.SetBounds(toolsLeft + index * (toolWidth + gap), 8, toolWidth, 30);
            using var buttonPath = RoundedPath(button.ClientRectangle, 7);
            button.Region?.Dispose();
            button.Region = new Region(buttonPath);
        }
        var composerTop = _toolButtons.Count == 0 ? 12 : 50;
        _send.Location = new Point(Math.Max(8, ClientSize.Width - 44), composerTop);
        _input.Location = new Point(14, composerTop + 7);
        _placeholder.Location = new Point(14, composerTop + 8);
        _input.Size = new Size(Math.Max(120, ClientSize.Width - 74), 24);
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

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern nint SendMessage(nint window, uint message, nint wParam, string lParam);

    private static void SetCueBanner(nint handle, string text) => SendMessage(handle, 0x1501, 1, text);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);
}
