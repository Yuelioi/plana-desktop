using System.Drawing.Drawing2D;
using Forms = System.Windows.Forms;

namespace Plana.Companion.Native;

internal sealed class CompanionChatInput : Forms.Form
{
    private readonly Forms.TextBox _input;
    private readonly Forms.Button _send;
    private readonly Func<string, Task> _submit;

    public CompanionChatInput(bool chinese, Func<string, Task> submit)
    {
        _submit = submit;
        FormBorderStyle = Forms.FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = Forms.FormStartPosition.Manual;
        BackColor = Color.FromArgb(246, 248, 253);
        Padding = new Padding(12, 8, 8, 8);
        Height = 52;
        MinimumSize = new Size(260, 52);

        _input = new Forms.TextBox
        {
            BorderStyle = Forms.BorderStyle.None,
            Dock = Forms.DockStyle.Fill,
            Font = new Font("Segoe UI", 10.5f),
            BackColor = BackColor,
            Margin = new Padding(0, 7, 8, 0),
        };
        SetCueBanner(_input.Handle, chinese ? "和普拉娜说点什么…" : "Ask Plana…");
        _input.KeyDown += async (_, args) =>
        {
            if (args.KeyCode != Keys.Enter || args.Shift) return;
            args.SuppressKeyPress = true;
            await SubmitAsync();
        };

        _send = new Forms.Button
        {
            Dock = Forms.DockStyle.Right,
            Width = 40,
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
        Controls.Add(_send);
        Resize += (_, _) => ApplyRoundedRegion();
        Shown += (_, _) => ApplyRoundedRegion();
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

    private void ApplyRoundedRegion()
    {
        using var path = new GraphicsPath();
        const int radius = 12;
        var bounds = new Rectangle(0, 0, Width, Height);
        path.AddArc(bounds.Left, bounds.Top, radius * 2, radius * 2, 180, 90);
        path.AddArc(bounds.Right - radius * 2, bounds.Top, radius * 2, radius * 2, 270, 90);
        path.AddArc(bounds.Right - radius * 2, bounds.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
        path.CloseFigure();
        Region?.Dispose();
        Region = new Region(path);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern nint SendMessage(nint window, uint message, nint wParam, string lParam);

    private static void SetCueBanner(nint handle, string text) => SendMessage(handle, 0x1501, 1, text);
}
