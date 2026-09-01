using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace Plana.TransientUI;

public partial class CompanionDockWindow : Window, IDisposable
{
    private Func<string, Task>? _submit;
    private Action<string>? _execute;
    private bool _userEngaged;

    public CompanionDockWindow()
    {
        InitializeComponent();
        InputBox.TextChanged += (_, _) => UpdateInputPlaceholder();
        InputBox.GotKeyboardFocus += (_, _) => InputPlaceholder.Visibility = Visibility.Collapsed;
        InputBox.LostKeyboardFocus += (_, _) => UpdateInputPlaceholder();
        InputBox.PreviewMouseDown += (_, _) => _userEngaged = true;
        IsVisibleChanged += (_, _) => { if (!IsVisible) _userEngaged = false; };
    }

    public nint Handle => new WindowInteropHelper(this).EnsureHandle();
    public bool ShouldRemainVisible => Dispatcher.CheckAccess()
        ? _userEngaged && IsKeyboardFocusWithin
        : Dispatcher.Invoke(() => _userEngaged && IsKeyboardFocusWithin);
    public int PixelHeight => Dispatcher.CheckAccess()
        ? ToPixels(ActualHeight > 0 ? ActualHeight : Height)
        : Dispatcher.Invoke(() => ToPixels(ActualHeight > 0 ? ActualHeight : Height));
    public Rect PixelBounds => Dispatcher.CheckAccess()
        ? GetPixelBounds()
        : Dispatcher.Invoke(GetPixelBounds);
    private double DpiScale => VisualTreeHelper.GetDpi(this).DpiScaleX;

    public void Configure(bool chinese, IEnumerable<(string Id, string Label)> actions, Func<string, Task> submit, Action<string> execute)
    {
        var configuredActions = actions.Take(4).ToArray();
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => Configure(chinese, configuredActions, submit, execute));
            return;
        }
        _submit = submit;
        _execute = execute;
        InputPlaceholder.Text = chinese ? "说些什么吧…" : "Say something…";
        SendButton.ToolTip = chinese ? "发送消息" : "Send message";
        ActionPanel.Children.Clear();
        foreach (var action in configuredActions)
        {
            var button = new Button
            {
                Content = action.Label,
                Height = 26,
                Margin = new Thickness(0, 0, 6, 0),
                Padding = new Thickness(8, 1, 8, 1),
                Background = new SolidColorBrush(Color.FromRgb(43, 59, 101)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(67, 88, 143)),
                Foreground = new SolidColorBrush(Color.FromRgb(218, 225, 244)),
                Tag = action.Id,
                Cursor = Cursors.Hand,
            };
            button.Click += (_, _) => _execute?.Invoke((string)button.Tag);
            ActionPanel.Children.Add(button);
        }
        var hasActions = ActionPanel.Children.Count > 0;
        ActionsRow.Height = hasActions ? new GridLength(26) : new GridLength(0);
        ActionsGap.Height = hasActions ? new GridLength(4) : new GridLength(0);
        Height = hasActions ? 82 : 52;
    }

    public void ShowDock()
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.Invoke(ShowDock); return; }
        if (!IsVisible) Show();
    }

    public void HideDock()
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.Invoke(HideDock); return; }
        if (IsVisible) Hide();
    }

    public void PositionPixels(int left, int top, int width)
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.Invoke(() => PositionPixels(left, top, width)); return; }
        var scale = DpiScale;
        Left = left / scale;
        Top = top / scale;
        Width = width / scale;
    }

    public void SetTopMost(bool value)
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.Invoke(() => SetTopMost(value)); return; }
        Topmost = value;
    }

    private async void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        _userEngaged = true;
        if (e.Key != Key.Enter || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) return;
        e.Handled = true;
        await SubmitAsync();
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e) => await SubmitAsync();

    private async Task SubmitAsync()
    {
        var text = InputBox.Text.Trim();
        if (text.Length == 0 || _submit is null || !SendButton.IsEnabled) return;
        SendButton.IsEnabled = false;
        InputBox.IsEnabled = false;
        try
        {
            await _submit(text);
            InputBox.Clear();
        }
        finally
        {
            InputBox.IsEnabled = true;
            SendButton.IsEnabled = true;
            InputBox.Focus();
        }
    }

    private void UpdateInputPlaceholder() => InputPlaceholder.Visibility =
        InputBox.Text.Length == 0 && !InputBox.IsKeyboardFocusWithin
            ? Visibility.Visible
            : Visibility.Collapsed;

    private int ToPixels(double value) => (int)Math.Round(value * DpiScale);

    private Rect GetPixelBounds() => new(
        (int)Math.Round(Left * DpiScale),
        (int)Math.Round(Top * DpiScale),
        ToPixels(ActualWidth),
        ToPixels(ActualHeight > 0 ? ActualHeight : Height));

    public void Dispose()
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.Invoke(Close); return; }
        Close();
    }
}
