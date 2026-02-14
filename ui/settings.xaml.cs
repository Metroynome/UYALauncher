using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace UYALauncher;

public partial class MainWindow : Window
{
    private readonly bool _isHotkeyMode;
    private bool _cancelled = false;

    // Default constructor for XAML
    public MainWindow() : this(false)
    {
        Console.WriteLine("MainWindow created via default constructor (from XAML?)");
    }

    public MainWindow(bool hotkeyMode = false)
    {
        Console.WriteLine($"MainWindow created with hotkeyMode={hotkeyMode}");
        
        try
        {
            InitializeComponent();
            
            // Apply theme (high contrast or dark)
            ThemeHelper.ApplyTheme(this);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR in InitializeComponent: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            throw;
        }
        
        _isHotkeyMode = hotkeyMode;

        // Configure buttons based on mode
        if (_isHotkeyMode)
        {
            Title = "UYA Launcher - Settings";
            CheckUpdatesButton.Visibility = Visibility.Visible;
            SaveButton.Visibility = Visibility.Visible;
            SaveAndRelaunchButton.Visibility = Visibility.Visible;
            LaunchButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            Title = "UYA Launcher - First Run Setup";
        }

        LoadConfiguration();
        ValidateLaunchButton();

        // Subscribe to text changed events
        IsoPathTextBox.TextChanged += (s, e) => ValidateLaunchButton();
        Pcsx2PathTextBox.TextChanged += (s, e) => ValidateLaunchButton();
    }

    private void LoadConfiguration()
    {
        if (!Configuration.IsFirstRun())
        {
            var config = Configuration.Load();
            IsoPathTextBox.Text = config.IsoPath;
            Pcsx2PathTextBox.Text = config.Pcsx2Path;
            RegionComboBox.SelectedIndex = config.Region == "PAL" ? 1 : 0;
            AutoUpdateCheckBox.IsChecked = config.AutoUpdate;
            FullscreenCheckBox.IsChecked = config.Fullscreen;
            BootToMultiplayerCheckBox.IsChecked = config.Patches.BootToMultiplayer;
            WidescreenCheckBox.IsChecked = config.Patches.Widescreen;
            ProgressiveScanCheckBox.IsChecked = config.Patches.ProgressiveScan;
            EmbedWindowCheckBox.IsChecked = config.EmbedWindow;
            ShowConsoleCheckBox.IsChecked = config.ShowConsole;
        }
    }

    private void ValidateLaunchButton()
    {
        bool hasIso = !string.IsNullOrWhiteSpace(IsoPathTextBox.Text);
        bool hasPcsx2 = !string.IsNullOrWhiteSpace(Pcsx2PathTextBox.Text);
        LaunchButton.IsEnabled = hasIso && hasPcsx2;
    }

    private void BrowseIso_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select UYA ISO File",
            Filter = "PS2 ISO Files (*.iso;*.bin;*.img)|*.iso;*.bin;*.img|All Files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            IsoPathTextBox.Text = dialog.FileName;
        }
    }

    private void BrowsePcsx2_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select PCSX2 Executable",
            Filter = "PCSX2 Executable (pcsx2.exe;pcsx2-qt.exe)|pcsx2.exe;pcsx2-qt.exe|All Executables (*.exe)|*.exe",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            Pcsx2PathTextBox.Text = dialog.FileName;
        }
    }

    private void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        _ = Updater.CheckAndUpdateAsync(false);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        SaveConfiguration();
        _cancelled = false;
        Close();
    }

    private void SaveAndRelaunch_Click(object sender, RoutedEventArgs e)
    {
        SaveConfiguration();
        _cancelled = false;

        // Relaunch the application
        var exePath = Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
        Process.Start(new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = true
        });

        // Close current application
        Application.Current.Shutdown();
    }

    private void Launch_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateAndSave())
            return;

        _cancelled = false;
        DialogResult = true;
        Close();
    }

    private bool ValidateAndSave()
    {
        if (string.IsNullOrWhiteSpace(IsoPathTextBox.Text) || 
            string.IsNullOrWhiteSpace(Pcsx2PathTextBox.Text))
        {
            MessageBox.Show(
                "Please select both ISO and PCSX2 paths.",
                "Validation Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        if (!File.Exists(IsoPathTextBox.Text))
        {
            MessageBox.Show(
                "The selected ISO file does not exist.",
                "File Not Found",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        if (!File.Exists(Pcsx2PathTextBox.Text))
        {
            MessageBox.Show(
                "The selected PCSX2 executable does not exist.",
                "File Not Found",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        SaveConfiguration();
        return true;
    }

    private void SaveConfiguration()
    {
        var config = new ConfigurationData
        {
            IsoPath = IsoPathTextBox.Text,
            Pcsx2Path = Pcsx2PathTextBox.Text,
            Region = (RegionComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "NTSC",
            AutoUpdate = AutoUpdateCheckBox.IsChecked ?? true,
            Fullscreen = FullscreenCheckBox.IsChecked ?? true,
            EmbedWindow = EmbedWindowCheckBox.IsChecked ?? true,
            ShowConsole = ShowConsoleCheckBox.IsChecked ?? false,
            Patches = new PatchFlags
            {
                BootToMultiplayer = BootToMultiplayerCheckBox.IsChecked ?? true,
                Widescreen = WidescreenCheckBox.IsChecked ?? true,
                ProgressiveScan = ProgressiveScanCheckBox.IsChecked ?? true
            }
        };

        Configuration.Save(config);
        PatchManager.ApplyPatches(config);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        
        if (!_cancelled && !_isHotkeyMode)
        {
            // Check if configuration is complete on close
            if (string.IsNullOrWhiteSpace(IsoPathTextBox.Text) || 
                string.IsNullOrWhiteSpace(Pcsx2PathTextBox.Text))
            {
                var result = MessageBox.Show(
                    "Configuration is incomplete. Are you sure you want to exit?",
                    "Exit Confirmation",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }
                
                _cancelled = true;
            }
        }
    }

    public bool WasCancelled => _cancelled;
}
