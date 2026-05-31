using System.Windows;
using System.Windows.Threading;

namespace AiTrader.Wiz;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        VerboseLogger.Initialize();
        VerboseLogger.Info("Application startup beginning.");

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        try
        {
            var window = new MainWindow();
            MainWindow = window;
            window.Show();
            VerboseLogger.Info("Main window created and shown.");
            base.OnStartup(e);
        }
        catch (Exception ex)
        {
            VerboseLogger.Error("Fatal exception during startup.", ex);
            MessageBox.Show(
                $"AlTrader Config Wizard failed during startup.{Environment.NewLine}{Environment.NewLine}Log file:{Environment.NewLine}{VerboseLogger.CurrentLogDisplayPath}",
                "AlTrader Config Wizard Startup Failure",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        VerboseLogger.Info($"Application exiting with code {e.ApplicationExitCode}.");
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        VerboseLogger.Error("Unhandled dispatcher exception.", e.Exception);
        MessageBox.Show(
            $"AlTrader Config Wizard encountered an unexpected error.{Environment.NewLine}{Environment.NewLine}Log file:{Environment.NewLine}{VerboseLogger.CurrentLogDisplayPath}",
            "AlTrader Config Wizard Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
        Shutdown(-2);
    }

    private void OnCurrentDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        VerboseLogger.Error($"Unhandled AppDomain exception. IsTerminating={e.IsTerminating}.", exception);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        VerboseLogger.Error("Unobserved task exception.", e.Exception);
        e.SetObserved();
    }
}
