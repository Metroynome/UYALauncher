using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace UYALauncher;

public partial class App : Application {
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeConsole();

    protected override void OnStartup(StartupEventArgs e) {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Handle self-update command line argument
        if (e.Args.Length > 0 && e.Args[0] == "--self-update") {
            if (e.Args.Length >= 2) {
                var args = e.Args[1].Split('|');
                var newExePath = args[0];
                var remoteVersion = args.Length > 1 ? args[1] : "0.0.0";
                Updater.RunSelfUpdate(newExePath, remoteVersion);
            }
            Shutdown();
            return;
        }

        // Check if this is first run or config is incomplete
        bool firstRun = Configuration.IsFirstRun();
        bool configIncomplete = !firstRun && !Configuration.IsConfigComplete();

        // Show setup dialog if needed
        if (firstRun || configIncomplete) {
            var setupWindow = new MainWindow(hotkeyMode: false);
            var result = setupWindow.ShowDialog();

            if (setupWindow.WasCancelled || result != true) {
                Shutdown();
                return;
            }
        }

        // Load configuration
        var config = Configuration.Load();

        // Setup console ONLY if enabled in config
        if (config.ShowConsole) {
            AllocConsole();
            Console.WriteLine("=== UYA Launcher Starting ===");
            Console.WriteLine($"Version: 3.0.0");
            Console.WriteLine($"Config Path: {Configuration.GetConfigPath()}");
        }

        // Check for updates if auto-update is enabled
        if (config.AutoUpdate) {
            _ = Updater.CheckAndUpdateAsync(true);
        }

        // Apply patches
        PatchManager.ApplyPatches(config);

        // Create and show launcher window
        var launcherWindow = new LauncherWindow(config);
        MainWindow = launcherWindow;

        if (config.EmbedWindow) {
            launcherWindow.Show();
        } else {
            // launcherWindow.WindowState = WindowState.Minimized;
            launcherWindow.Visibility = Visibility.Hidden;
            launcherWindow.Show();
        }
    }

    protected override void OnExit(ExitEventArgs e) {
        base.OnExit(e);
        
        // Cleanup console if it was allocated
        try {
            FreeConsole();
        } catch {
            // Ignore errors during cleanup
        }
    }
}
