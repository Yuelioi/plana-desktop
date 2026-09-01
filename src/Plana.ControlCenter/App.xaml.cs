using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Microsoft.UI.Dispatching;
using Plana.Core.Settings;
using Windows.ApplicationModel.Activation;

namespace Plana_ControlCenter;

public partial class App : Application
{
    private Window? _window;
    private QuickLaunchWindow? _quickLaunchWindow;
    private AppInstance? _appInstance;
    private DispatcherQueue? _dispatcherQueue;

    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PlanaDesktop");

    public static DesktopSettingsStore SettingsStore { get; } = new(Path.Combine(DataDirectory, "settings.json"));
    public static DesktopSettings Settings { get; private set; } = new();
    public static bool IsChinese => Settings.UiCulture.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
    public static nint MainWindowHandle { get; private set; }

    public App()
    {
        InitializeComponent();
    }

    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        _appInstance = AppInstance.FindOrRegisterForKey("Plana.ControlCenter");
        if (!_appInstance.IsCurrent)
        {
            await _appInstance.RedirectActivationToAsync(activation);
            Exit();
            return;
        }
        _appInstance.Activated += OnAppActivated;
        await ActivateAsync(GetProtocolUri(activation));
    }

    private void OnAppActivated(object? sender, AppActivationArguments activation)
    {
        _dispatcherQueue?.TryEnqueue(async () => await ActivateAsync(GetProtocolUri(activation)));
    }

    private static Uri? GetProtocolUri(AppActivationArguments activation)
    {
        var uri = activation.Kind == ExtendedActivationKind.Protocol
            ? (activation.Data as ProtocolActivatedEventArgs)?.Uri
            : null;
        return uri;
    }

    private async Task ActivateAsync(Uri? uri)
    {
        Settings = await SettingsStore.LoadAsync();
        if (uri?.Host.Equals("commands", StringComparison.OrdinalIgnoreCase) == true)
        {
            _quickLaunchWindow ??= new QuickLaunchWindow();
            await _quickLaunchWindow.ShowAsync(ParseQuery(uri.Query, "query"));
            return;
        }
        _window ??= new MainWindow();
        MainWindowHandle = WinRT.Interop.WindowNative.GetWindowHandle(_window);
        if (_window is MainWindow mainWindow) mainWindow.Navigate(uri);
        _window.Activate();
    }

    private static string? ParseQuery(string queryString, string name)
    {
        foreach (var pair in queryString.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && Uri.UnescapeDataString(parts[0]).Equals(name, StringComparison.OrdinalIgnoreCase)) return Uri.UnescapeDataString(parts[1].Replace('+', ' '));
        }
        return null;
    }
}
