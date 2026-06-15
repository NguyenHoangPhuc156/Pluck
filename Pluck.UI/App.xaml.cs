using System.Windows;

namespace Pluck.UI;

public partial class App : System.Windows.Application
{
    private static Mutex? _singleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        const string mutexName = "Pluck.FloatingClipboard.SingleInstance";
        _singleInstanceMutex = new Mutex(true, mutexName, out var createdNew);
        if (!createdNew)
        {
            System.Windows.MessageBox.Show("Pluck is already running.", "Pluck", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);
        PluckAppHost.Initialize();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        PluckAppHost.Instance?.Dispose();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
