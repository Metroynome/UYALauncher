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

        bool firstRun = Configuration.IsFirstRun();
        bool configIncomplete = !firstRun && !Configuration.IsConfigComplete();

        if (firstRun || configIncomplete) {
            var setupWindow = new SettingsWindow(hotkeyMode: false);
            var result = setupWindow.ShowDialog();

            if (setupWindow.WasCancelled || result != true) {
                Shutdown();
                return;
            }
        }

        var config = Configuration.Load();
        var launchGame = ResolveLaunchGame(ref config);
        if (launchGame == null) {
            Shutdown();
            return;
        }

        var unsupportedMessage = GameSupport.GetUnsupportedMessage(
            GameDetector.ReadFromIso(config.GetIsoPathForGame(launchGame.Value)));
        if (unsupportedMessage != null) {
            MessageBox.Show(
                unsupportedMessage,
                "Unsupported Game Region",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
            return;
        }

        if (config.ShowConsole) {
            AllocConsole();
            Console.WriteLine("=== UYA Launcher Starting ===");
            Console.WriteLine($"Version: {config.Version}");
            Console.WriteLine($"Config Path: {Configuration.GetConfigPath()}");
        }

        if (config.AutoUpdate) {
            _ = Updater.CheckAndUpdateAsync(true);
        }

        var launcherWindow = new LauncherWindow(config, launchGame.Value);
        MainWindow = launcherWindow;

        if (config.EmbedWindow) {
            launcherWindow.Show();
        } else {
            launcherWindow.Visibility = Visibility.Hidden;
            launcherWindow.Show();
        }
    }

    private static SupportedGame? ResolveLaunchGame(ref ConfigurationData config) {
        while (true) {
            var defaultGame = config.GetDefaultLaunchGame();
            if (defaultGame != null && !string.IsNullOrWhiteSpace(config.GetIsoPathForGame(defaultGame.Value)))
                return defaultGame;

            var selectionWindow = new GameSelectionWindow(config);
            var result = selectionWindow.ShowDialog();
            if (result != true)
                return null;

            if (selectionWindow.OpenSettingsRequested) {
                var settingsWindow = new SettingsWindow(hotkeyMode: true);
                settingsWindow.ShowDialog();
                config = Configuration.Load();
                continue;
            }

            if (selectionWindow.SelectedGame != null)
                return selectionWindow.SelectedGame;
        }
    }

    protected override void OnExit(ExitEventArgs e) {
        base.OnExit(e);

        try {
            FreeConsole();
        } catch {
        }
    }
}
