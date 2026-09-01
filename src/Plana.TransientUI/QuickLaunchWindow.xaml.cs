using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Interop;

namespace Plana.TransientUI;

public partial class QuickLaunchWindow : Window
{
    private IReadOnlyList<TransientActionItem> _actions = [];
    private IReadOnlyList<TransientActionGroup> _groups = [];
    private Func<string, Task<(bool Succeeded, string? Message)>>? _execute;
    private IReadOnlyList<TransientActionItem> _visibleResults = [];
    private string? _expandedGroupId;
    private int _selectedIndex = -1;
    private bool _chinese;

    public QuickLaunchWindow()
    {
        InitializeComponent();
        TextCompositionManager.AddPreviewTextInputStartHandler(SearchBox, SearchBox_CompositionChanged);
        TextCompositionManager.AddPreviewTextInputUpdateHandler(SearchBox, SearchBox_CompositionChanged);
        SearchBox.GotKeyboardFocus += (_, _) => SearchPlaceholder.Visibility = Visibility.Collapsed;
        SearchBox.LostKeyboardFocus += (_, _) => UpdateSearchPlaceholder();
        Deactivated += (_, _) => Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.ContextIdle,
            ConfirmOutsideDeactivation);
        PreviewKeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape) { HideLauncher(); args.Handled = true; }
        };
    }

    public void Configure(
        IReadOnlyList<TransientActionItem> actions,
        IReadOnlyList<TransientActionGroup> groups,
        bool chinese,
        Func<string, Task<(bool Succeeded, string? Message)>> execute)
    {
        _actions = actions;
        _groups = groups;
        _chinese = chinese;
        _execute = execute;
        SearchPlaceholder.Text = chinese ? "搜索动作" : "Search actions";
        BuildGroups();
    }

    public bool ShowLauncher(string? query = null)
    {
        SearchBox.Text = query ?? string.Empty;
        CollapseDisclosure();
        PositionOnPrimaryWorkArea();
        if (!IsVisible) Show();
        var handle = new WindowInteropHelper(this).EnsureHandle();
        ActivateForTyping(handle);
        SearchBox.Focus();
        Keyboard.Focus(SearchBox);
        SearchBox.SelectAll();
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
        {
            SearchBox.Focus();
            Keyboard.Focus(SearchBox);
        });
        return GetForegroundWindow() == handle && SearchBox.IsKeyboardFocusWithin;
    }

    private void BuildGroups()
    {
        GroupPanel.Children.Clear();
        foreach (var group in _groups)
        {
            var button = CreatePill(group.Name);
            button.Tag = group.Id;
            button.Click += (_, _) => ToggleGroup(group.Id);
            GroupPanel.Children.Add(button);
        }
    }

    private void ToggleGroup(string groupId)
    {
        if (_expandedGroupId == groupId) { CollapseDisclosure(); return; }
        _expandedGroupId = groupId;
        var group = _groups.First(item => item.Id == groupId);
        var ids = group.ActionIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        ShowDisclosure(_actions.Where(item => ids.Contains(item.Id)).ToArray(),
            _chinese ? "这个动作组是空的" : "This action group is empty");
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateSearchPlaceholder();
        ApplyQuery(SearchBox.Text);
    }

    private void SearchBox_CompositionChanged(object sender, TextCompositionEventArgs e)
    {
        var composition = e.TextComposition.CompositionText;
        if (string.IsNullOrEmpty(composition)) composition = e.TextComposition.Text;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
        {
            if (string.IsNullOrEmpty(composition))
            {
                ApplyQuery(SearchBox.Text);
                return;
            }
            var committed = SearchBox.Text;
            var insertion = Math.Clamp(SearchBox.CaretIndex, 0, committed.Length);
            ApplyQuery(committed.Insert(insertion, composition));
        });
    }

    private void ApplyQuery(string? value)
    {
        var query = value?.Trim() ?? string.Empty;
        if (query.Length == 0) { CollapseDisclosure(); return; }
        var tokens = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        _visibleResults = _actions
            .Where(item => tokens.All(token => SearchText(item).Contains(token, StringComparison.CurrentCultureIgnoreCase)))
            .Take(10)
            .ToArray();
        ShowDisclosure(_visibleResults, _chinese ? "没有匹配的动作" : "No matching actions");
    }

    private void UpdateSearchPlaceholder() => SearchPlaceholder.Visibility =
        string.IsNullOrEmpty(SearchBox.Text) && !SearchBox.IsKeyboardFocusWithin
            ? Visibility.Visible
            : Visibility.Collapsed;

    private async void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Back or Key.Delete)
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, () => ApplyQuery(SearchBox.Text));
        }
        if (_visibleResults.Count == 0) return;
        if (e.Key == Key.Down)
        {
            SelectResult(Math.Min(_visibleResults.Count - 1, _selectedIndex + 1));
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Up)
        {
            SelectResult(Math.Max(0, _selectedIndex - 1));
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await ExecuteAsync(_visibleResults[_selectedIndex >= 0 ? _selectedIndex : 0]);
        }
    }

    private void ShowDisclosure(IReadOnlyList<TransientActionItem> actions, string emptyText)
    {
        DisclosurePanel.Children.Clear();
        _visibleResults = actions;
        _selectedIndex = actions.Count > 0 ? 0 : -1;
        if (actions.Count == 0)
        {
            DisclosurePanel.Children.Add(new TextBlock { Text = emptyText, Foreground = Brushes.DimGray, Margin = new Thickness(8, 6, 8, 0) });
        }
        for (var actionIndex = 0; actionIndex < actions.Count; actionIndex++)
        {
            var action = actions[actionIndex];
            var currentIndex = actionIndex;
            var button = CreateActionRow(action);
            button.Tag = action.Id;
            button.Click += async (_, _) => await ExecuteAsync(action);
            button.MouseEnter += (_, _) => SelectResult(currentIndex);
            DisclosurePanel.Children.Add(button);
        }
        DisclosureSurface.Visibility = Visibility.Visible;
        Height = Math.Min(488, 145 + Math.Max(1, actions.Count) * 56);
        SelectResult(_selectedIndex);
    }

    private async Task ExecuteAsync(TransientActionItem action)
    {
        if (_execute is null) return;
        var result = await _execute(action.Id);
        if (result.Succeeded) HideLauncher();
        else SearchPlaceholder.Text = result.Message ?? (_chinese ? "动作执行失败" : "Action failed");
    }

    private Button CreatePill(string text) => new()
    {
        Content = text,
        MinHeight = 30,
        Padding = new Thickness(13, 3, 13, 3),
        Margin = new Thickness(0, 0, 7, 0),
        Background = new SolidColorBrush(Color.FromRgb(239, 239, 239)),
        BorderBrush = new SolidColorBrush(Color.FromRgb(215, 215, 215)),
        BorderThickness = new Thickness(1),
        Cursor = Cursors.Hand,
    };

    private Button CreateActionRow(TransientActionItem action)
    {
        var title = new TextBlock { Text = action.Name, FontWeight = FontWeights.SemiBold, FontSize = 14, TextTrimming = TextTrimming.CharacterEllipsis };
        var description = new TextBlock { Text = action.Description, Foreground = new SolidColorBrush(Color.FromRgb(92, 92, 92)), FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis };
        var type = new TextBlock { Text = action.Type, HorizontalAlignment = HorizontalAlignment.Right, FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(62, 79, 121)) };
        var source = new TextBlock { Text = action.Source, HorizontalAlignment = HorizontalAlignment.Right, FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(112, 112, 112)), TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 170 };
        var content = new Grid();
        content.ColumnDefinitions.Add(new ColumnDefinition());
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var primary = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        primary.Children.Add(title);
        primary.Children.Add(description);
        var metadata = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
        metadata.Children.Add(type);
        metadata.Children.Add(source);
        Grid.SetColumn(metadata, 1);
        content.Children.Add(primary);
        content.Children.Add(metadata);
        return new Button
        {
            Content = content,
            Style = (Style)FindResource("ActionRowButtonStyle"),
            ToolTip = $"{action.Name}\n{action.Description}\n{action.Type} · {action.Source}",
            Cursor = Cursors.Hand,
        };
    }

    private void SelectResult(int index)
    {
        _selectedIndex = index;
        for (var itemIndex = 0; itemIndex < DisclosurePanel.Children.Count; itemIndex++)
        {
            if (DisclosurePanel.Children[itemIndex] is Button button)
                button.Background = itemIndex == index
                    ? new SolidColorBrush(Color.FromRgb(226, 233, 249))
                    : Brushes.Transparent;
        }
    }

    private void CollapseDisclosure()
    {
        _expandedGroupId = null;
        _visibleResults = [];
        _selectedIndex = -1;
        DisclosurePanel.Children.Clear();
        DisclosureSurface.Visibility = Visibility.Collapsed;
        Height = 132;
    }

    private void HideLauncher()
    {
        if (!IsVisible) return;
        Keyboard.ClearFocus();
        FocusManager.SetFocusedElement(this, null);
        Close();
    }

    private void ConfirmOutsideDeactivation()
    {
        if (!IsVisible) return;
        var handle = new WindowInteropHelper(this).Handle;
        if (handle != 0 && GetForegroundWindow() != handle) HideLauncher();
    }

    private void PositionOnPrimaryWorkArea()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Left + (area.Width - Width) / 2;
        Top = area.Top + Math.Max(72, area.Height / 5);
    }

    private static string SearchText(TransientActionItem item) => $"{item.Name} {item.Description} {item.Source}";

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint window);

    private static void ActivateForTyping(nint window)
    {
        var foreground = GetForegroundWindow();
        var foregroundThread = foreground == 0 ? 0 : GetWindowThreadProcessId(foreground, out _);
        var currentThread = GetCurrentThreadId();
        var attached = foregroundThread != 0 && foregroundThread != currentThread &&
            AttachThreadInput(currentThread, foregroundThread, true);
        try
        {
            BringWindowToTop(window);
            SetForegroundWindow(window);
            SetActiveWindow(window);
            SetFocus(window);
        }
        finally
        {
            if (attached) AttachThreadInput(currentThread, foregroundThread, false);
        }
    }

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint attachThread, uint attachToThread, bool attach);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(nint window);

    [DllImport("user32.dll")]
    private static extern nint SetActiveWindow(nint window);

    [DllImport("user32.dll")]
    private static extern nint SetFocus(nint window);

}
