using System;
using System.Windows;

namespace UYALauncherSetup;

public partial class App : Application {
    protected override void OnStartup(StartupEventArgs e) {
        base.OnStartup(e);
        
        // Catch any unhandled exceptions
        AppDomain.CurrentDomain.UnhandledException += (s, args) => {
            var ex = args.ExceptionObject as Exception;
            MessageBox.Show($"Fatal error:\n\n{ex?.Message}\n\n{ex?.StackTrace}", 
                "Installer Error", MessageBoxButton.OK, MessageBoxImage.Error);
        };
        
        DispatcherUnhandledException += (s, args) => {
            MessageBox.Show($"Error:\n\n{args.Exception.Message}\n\n{args.Exception.StackTrace}", 
                "Installer Error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
    }
}
