using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Plana_ControlCenter.Pages;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Plana_ControlCenter;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.SetIcon("Assets/AppIcon.ico");
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1040, 720));
        if (App.IsChinese)
        {
            CommandPaletteNavigationItem.Content = "快速启动";
            SettingsNavigationItem.Content = "设置";
            ChatNavigationItem.Content = "对话";
            ActionsNavigationItem.Content = "操作";
            ToolGroupsNavigationItem.Content = "工具组";
            MigrationNavigationItem.Content = "扩展";
        }
        NavFrame.Navigate(typeof(CommandPalettePage));
    }

    public void Navigate(Uri? uri)
    {
        if (uri is null)
        {
            NavView.SelectedItem = CommandPaletteNavigationItem;
            NavFrame.Navigate(typeof(CommandPalettePage));
            return;
        }
        if (uri?.Host.Equals("settings", StringComparison.OrdinalIgnoreCase) == true)
        {
            NavView.SelectedItem = SettingsNavigationItem;
            NavFrame.Navigate(typeof(SettingsPage));
            return;
        }
        if (uri?.Host.Equals("commands", StringComparison.OrdinalIgnoreCase) == true)
        {
            NavView.SelectedItem = CommandPaletteNavigationItem;
            NavFrame.Navigate(typeof(CommandPalettePage), uri.Query);
            return;
        }
        if (uri?.Host.Equals("groups", StringComparison.OrdinalIgnoreCase) == true)
        {
            NavView.SelectedItem = ToolGroupsNavigationItem;
            NavFrame.Navigate(typeof(ToolGroupsPage));
            return;
        }
        if (uri?.Host.Equals("chat", StringComparison.OrdinalIgnoreCase) == true)
        {
            NavView.SelectedItem = ChatNavigationItem;
            NavFrame.Navigate(typeof(ChatPage));
            return;
        }
        if (uri?.Host.Equals("extensions", StringComparison.OrdinalIgnoreCase) == true)
        {
            NavView.SelectedItem = MigrationNavigationItem;
            NavFrame.Navigate(typeof(AboutPage));
            return;
        }
        if (uri?.Host.Equals("actions", StringComparison.OrdinalIgnoreCase) == true)
        {
            NavView.SelectedItem = ActionsNavigationItem;
            NavFrame.Navigate(typeof(HomePage), ParseQuery(uri.Query, "query"));
            return;
        }

        ActionsNavigationItem.IsSelected = true;
        NavFrame.Navigate(typeof(HomePage), uri is null ? null : ParseQuery(uri.Query, "query"));
    }

    private static string? ParseQuery(string queryString, string name)
    {
        foreach (var pair in queryString.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && Uri.UnescapeDataString(parts[0]).Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return Uri.UnescapeDataString(parts[1].Replace('+', ' '));
            }
        }
        return null;
    }

    private void TitleBar_PaneToggleRequested(TitleBar sender, object args)
    {
        NavView.IsPaneOpen = !NavView.IsPaneOpen;
    }

    private void TitleBar_BackRequested(TitleBar sender, object args)
    {
        NavFrame.GoBack();
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item)
        {
            switch (item.Tag)
            {
                case "commands":
                    NavFrame.Navigate(typeof(CommandPalettePage));
                    break;
                case "settings":
                    NavFrame.Navigate(typeof(SettingsPage));
                    break;
                case "home":
                    NavFrame.Navigate(typeof(HomePage));
                    break;
                case "chat":
                    NavFrame.Navigate(typeof(ChatPage));
                    break;
                case "groups":
                    NavFrame.Navigate(typeof(ToolGroupsPage));
                    break;
                case "about":
                    NavFrame.Navigate(typeof(AboutPage));
                    break;
                default:
                    throw new InvalidOperationException($"Unknown navigation item tag: {item.Tag}");
            }
        }
    }
}
