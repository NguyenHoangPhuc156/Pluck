using System.Windows;

namespace Pluck.UI;

/// <summary>
/// WPF application entry point for Pluck; enforces single-instance execution and hosts the app lifecycle.
/// </summary>
public partial class App : System.Windows.Application
{
    private static Mutex? _singleInstanceMutex;

    /// <summary>
    /// Ensures only one Pluck instance runs, then initializes the application host.
    /// </summary>
    /// <param name="e">Startup event arguments supplied by the WPF runtime.</param>
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

    /// <summary>
    /// Disposes the application host and single-instance mutex before shutdown.
    /// </summary>
    /// <param name="e">Exit event arguments supplied by the WPF runtime.</param>
    protected override void OnExit(ExitEventArgs e)
    {
        PluckAppHost.Instance?.Dispose();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
