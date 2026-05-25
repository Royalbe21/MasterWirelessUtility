using System;
using System.Threading.Tasks;
using System.Windows;
using MasterWirelessUtility.Services;
using MessageBox = System.Windows.MessageBox;

namespace MasterWirelessUtility;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        CrashLogger.Info("Application starting.");

        DispatcherUnhandledException += (_, args) =>
        {
            CrashLogger.Error(args.Exception, "DispatcherUnhandledException");
            MessageBox.Show(
                $"The app hit an error and wrote a crash log here:\n\n{CrashLogger.CurrentLogFile}\n\nError:\n{args.Exception.Message}",
                "Master Wireless Utility crash log created",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                CrashLogger.Error(ex, "AppDomain.UnhandledException");
            else
                CrashLogger.Write("ERROR", $"Unhandled non-Exception object: {args.ExceptionObject}", null);
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            CrashLogger.Error(args.Exception, "TaskScheduler.UnobservedTaskException");
            args.SetObserved();
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        CrashLogger.Info($"Application exiting. ExitCode={e.ApplicationExitCode}");
        base.OnExit(e);
    }
}
