using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Plana_ControlCenter.Controls;

public sealed partial class SearchField : UserControl
{
    public static readonly DependencyProperty PlaceholderTextProperty = DependencyProperty.Register(
        nameof(PlaceholderText), typeof(string), typeof(SearchField), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(SearchField), new PropertyMetadata(string.Empty));

    public string PlaceholderText { get => (string)GetValue(PlaceholderTextProperty); set => SetValue(PlaceholderTextProperty, value); }
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public event EventHandler? SearchTextChanged;
    public event EventHandler? EnterPressed;

    public SearchField() => InitializeComponent();

    public void FocusInput() => Input.Focus(FocusState.Programmatic);

    private void Input_TextChanged(object sender, TextChangedEventArgs e)
    {
        // TextBox.TextChanged can run before the TwoWay x:Bind source is updated.
        // Publish the current editor value before notifying page-level filters.
        if (!string.Equals(Text, Input.Text, StringComparison.Ordinal)) Text = Input.Text;
        if (SearchGlyph is not null) SearchGlyph.Visibility = Input.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        SearchTextChanged?.Invoke(this, EventArgs.Empty);
    }
    private void Input_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Enter) return;
        e.Handled = true;
        EnterPressed?.Invoke(this, EventArgs.Empty);
    }
}
