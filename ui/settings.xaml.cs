using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace UYALauncher;

public partial class SettingsWindow : Window {
    private readonly bool _isHotkeyMode;
    private bool _cancelled = false;

    // Default constructor for XAML
    public SettingsWindow() : this(false) {
        Console.WriteLine("SettingsWindow created via default constructor (from XAML?)");
    }

    public SettingsWindow(bool hotkeyMode = false) {
        Console.WriteLine($"SettingsWindow created with hotkeyMode={hotkeyMode}");
        
        try {
            InitializeComponent();            
            ThemeHelper.ApplyTheme(this);
        } catch (Exception ex) {
            Console.WriteLine($"ERROR in InitializeComponent: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            throw;
        }
        _isHotkeyMode = hotkeyMode;

        // Configure buttons based on mode
        if (_isHotkeyMode) {
            Title = "UYA Launcher - Settings";
            CheckUpdatesButton.Visibility = Visibility.Visible;
            SaveButton.Visibility = Visibility.Visible;
            SaveAndRelaunchButton.Visibility = Visibility.Visible;
            LaunchButton.Visibility = Visibility.Collapsed;
        } else {
            Title = "UYA Launcher - First Run Setup";
        }
        
        // Set version from assembly
        var version = System.Reflection.Assembly.GetExecutingAssembly()
            .GetName().Version?.ToString(3) ?? "3.0.0";
        VersionTextBlock.Text = version;
        
        LoadConfiguration();
        ValidateLaunchButton();

        // Subscribe to text changed events
        IsoPathTextBox.TextChanged += (s, e) => ValidateLaunchButton();
        BiosPathTextBox.TextChanged += (s, e) => ValidateLaunchButton();
    }

    public bool WasCancelled => _cancelled;

    private void LoadConfiguration() {
        var config = Configuration.Load();
        IsoPathTextBox.Text = config.IsoPath;
        BiosPathTextBox.Text = config.BiosPath;
        
        // Set region
        foreach (ComboBoxItem item in RegionComboBox.Items) {
            if (item.Content.ToString() == config.Region) {
                RegionComboBox.SelectedItem = item;
                break;
            }
        }

        // Set checkboxes
        AutoUpdateCheckBox.IsChecked = config.AutoUpdate;
        EmbedWindowCheckBox.IsChecked = config.EmbedWindow;
        FullscreenCheckBox.IsChecked = config.Fullscreen;
        BootToMultiplayerCheckBox.IsChecked = config.Patches.BootToMultiplayer;
        WidescreenCheckBox.IsChecked = config.Patches.Widescreen;
        ShowConsoleCheckBox.IsChecked = config.ShowConsole;
    }

    private void SaveConfiguration() {
        var config = new ConfigurationData {
            IsoPath = IsoPathTextBox.Text,
            BiosPath = BiosPathTextBox.Text,
            Region = ((ComboBoxItem)RegionComboBox.SelectedItem)?.Content.ToString() ?? "NTSC",
            AutoUpdate = AutoUpdateCheckBox.IsChecked ?? true,
            EmbedWindow = EmbedWindowCheckBox.IsChecked ?? true,
            Fullscreen = FullscreenCheckBox.IsChecked ?? true,
            ShowConsole = ShowConsoleCheckBox.IsChecked ?? false,
            Patches = new PatchFlags {
                BootToMultiplayer = BootToMultiplayerCheckBox.IsChecked ?? true,
                Widescreen = WidescreenCheckBox.IsChecked ?? true
            }
        };

        Configuration.Save(config);
    }

    private void ValidateLaunchButton() {
        bool hasIso = !string.IsNullOrWhiteSpace(IsoPathTextBox.Text);
        bool hasBios = !string.IsNullOrWhiteSpace(BiosPathTextBox.Text);
        
        LaunchButton.IsEnabled = hasIso && hasBios;
    }

    private void BrowseIso_Click(object sender, RoutedEventArgs e) {
        var dialog = new OpenFileDialog {
            Filter = "ISO Files (*.iso)|*.iso|All Files (*.*)|*.*",
            Title = "Select UYA ISO File"
        };

        if (dialog.ShowDialog() == true) {
            IsoPathTextBox.Text = dialog.FileName;
        }
    }

    private void BrowseBios_Click(object sender, RoutedEventArgs e) {
        var dialog = new OpenFileDialog {
            Filter = "BIOS Files (*.bin)|*.bin|All Files (*.*)|*.*",
            Title = "Select PS2 BIOS File"
        };

        if (dialog.ShowDialog() == true) {
            BiosPathTextBox.Text = dialog.FileName;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e) {
        SaveConfiguration();
        
        if (_isHotkeyMode) {
            // In hotkey mode, apply patches after saving
            try {
                var config = Configuration.Load();
                PatchManager.ApplyPatches(config);
            } catch (Exception ex) {
                MessageBox.Show(
                    $"Error applying patches:\n\n{ex.Message}",
                    "Patch Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        
        Close();
    }

    private void SaveAndRelaunch_Click(object sender, RoutedEventArgs e) {
        SaveConfiguration();
        
        // Get path to current executable
        var exePath = Environment.ProcessPath ?? AppContext.BaseDirectory;

        // Launch new instance
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
            FileName = exePath,
            UseShellExecute = true
        });

        // Exit current instance
        Application.Current.Shutdown();
    }

    private void Launch_Click(object sender, RoutedEventArgs e) {
        // Save and relaunch for first run
        SaveAndRelaunch_Click(sender, e);
    }

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e) {
        await Updater.CheckAndUpdateAsync(false);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) {
        _cancelled = true;
        DialogResult = false;
        Close();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e) {
        base.OnClosing(e);
        
        // If in first-run mode and window is closing without saving, mark as cancelled
        if (!_isHotkeyMode && DialogResult != true && !_cancelled) {
            _cancelled = true;
        }
    }
}