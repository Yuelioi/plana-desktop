using System.Windows.Threading;
using Plana.TransientUI;

namespace Plana.TransientUI.Tests;

public sealed class CompanionDockWindowTests
{
    [Fact]
    public void Configure_Marshals_From_Background_Thread()
    {
        CompanionDockWindow? dock = null;
        var ready = new ManualResetEventSlim();
        var uiThread = new Thread(() =>
        {
            dock = new CompanionDockWindow();
            ready.Set();
            Dispatcher.Run();
        });
        uiThread.SetApartmentState(ApartmentState.STA);
        uiThread.Start();
        Assert.True(ready.Wait(TimeSpan.FromSeconds(5)));

        try
        {
            var exception = Record.Exception(() => dock!.Configure(
                chinese: true,
                actions: [("test", "测试")],
                submit: _ => Task.CompletedTask,
                execute: _ => { }));
            Assert.Null(exception);
        }
        finally
        {
            dock!.Dispatcher.Invoke(() =>
            {
                dock.Close();
                Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Normal);
            });
            Assert.True(uiThread.Join(TimeSpan.FromSeconds(5)));
        }
    }
}
