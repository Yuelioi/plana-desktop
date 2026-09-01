using System.Windows;
using System.Windows.Threading;

namespace Plana.TransientUI;

public sealed class TransientUiHost : IDisposable
{
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _ready = new();
    private CompanionDockWindow? _dock;
    private QuickLaunchWindow? _quickLaunch;
    private IReadOnlyList<TransientActionItem> _quickActions = [];
    private IReadOnlyList<TransientActionGroup> _quickGroups = [];
    private bool _quickChinese;
    private Func<string, Task<(bool Succeeded, string? Message)>>? _quickExecute;
    private Exception? _startupError;
    private bool _disposed;

    public TransientUiHost()
    {
        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "Plana.TransientUI",
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        if (!_ready.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Transient UI Dispatcher did not start.");
        if (_startupError is not null) throw new InvalidOperationException("Transient UI failed to start.", _startupError);
    }

    public bool DockShouldRemainVisible => Invoke(() => _dock!.ShouldRemainVisible);
    public int DockPixelHeight => Invoke(() => _dock!.PixelHeight);
    public Rect DockPixelBounds => Invoke(() => _dock!.PixelBounds);
    public bool DockIsVisible => Invoke(() => _dock!.IsVisible);

    public void ConfigureDock(bool chinese, IEnumerable<(string Id, string Label)> actions, Func<string, Task> submit, Action<string> execute)
    {
        var configuredActions = actions.Take(4).ToArray();
        Invoke(() => _dock!.Configure(chinese, configuredActions, submit, execute));
    }

    public void ShowDock() => Invoke(() => _dock!.ShowDock());
    public void HideDock() => Invoke(() => _dock!.HideDock());
    public void PositionDockPixels(int left, int top, int width) => Invoke(() => _dock!.PositionPixels(left, top, width));
    public void SetDockTopMost(bool value) => Invoke(() => _dock!.SetTopMost(value));

    public void ConfigureQuickLaunch(
        IReadOnlyList<TransientActionItem> actions,
        IReadOnlyList<TransientActionGroup> groups,
        bool chinese,
        Func<string, Task<(bool Succeeded, string? Message)>> execute) =>
        Invoke(() =>
        {
            _quickActions = actions;
            _quickGroups = groups;
            _quickChinese = chinese;
            _quickExecute = execute;
        });

    public bool ShowQuickLaunch(string? query = null) => Invoke(() =>
    {
        if (_quickExecute is null) throw new InvalidOperationException("Quick Launch is not configured.");
        if (_quickLaunch is not null)
        {
            _quickLaunch.Close();
            _quickLaunch = null;
        }
        _quickLaunch = new QuickLaunchWindow();
        _quickLaunch.Configure(_quickActions, _quickGroups, _quickChinese, _quickExecute);
        return _quickLaunch.ShowLauncher(query);
    });

    public void HideQuickLaunch() => Invoke(() =>
    {
        if (_quickLaunch is null) return;
        _quickLaunch.Close();
        _quickLaunch = null;
    });

    private void Run()
    {
        try
        {
            _dock = new CompanionDockWindow();
        }
        catch (Exception exception) { _startupError = exception; }
        finally { _ready.Set(); }
        if (_startupError is null) Dispatcher.Run();
    }

    private void Invoke(Action action)
    {
        ThrowIfDisposed();
        _dock!.Dispatcher.Invoke(action);
    }

    private T Invoke<T>(Func<T> action)
    {
        ThrowIfDisposed();
        return _dock!.Dispatcher.Invoke(action);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(TransientUiHost));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _dock!.Dispatcher.Invoke(() =>
        {
            _quickLaunch?.Close();
            _dock.Close();
            Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Normal);
        });
        _thread.Join(TimeSpan.FromSeconds(5));
        _ready.Dispose();
        _disposed = true;
    }
}
