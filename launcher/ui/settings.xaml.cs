using System;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using System.Windows.Navigation;
using Microsoft.Win32;

namespace UYALauncher;

public partial class SettingsWindow : Window {
    private readonly bool _isHotkeyMode;
    private bool _cancelled = false;

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

        if (_isHotkeyMode) {
            Title = "UYA Launcher - Settings";
            CheckUpdatesButton.Visibility = Visibility.Visible;
            SaveButton.Visibility = Visibility.Visible;
            SaveAndRelaunchButton.Visibility = Visibility.Visible;
            LaunchButton.Visibility = Visibility.Collapsed;
        } else {
            Title = "UYA Launcher - First Run Setup";
        }
        
        var version = System.Reflection.Assembly.GetExecutingAssembly()
            .GetName().Version?.ToString(3) ?? "3.0.0";
        VersionTextBlock.Text = version;
        
        LoadConfiguration();
        ValidateLaunchButton();

        Rac3IsoPathTextBox.TextChanged += IsoPathTextBox_TextChanged;
        Rac4IsoPathTextBox.TextChanged += IsoPathTextBox_TextChanged;
        BiosPathTextBox.TextChanged += (s, e) => ValidateLaunchButton();
    }

    public bool WasCancelled => _cancelled;

    private void LoadConfiguration() {
        var config = Configuration.Load();

        Rac3IsoPathTextBox.Text = config.Rac3IsoPath;
        Rac4IsoPathTextBox.Text = config.Rac4IsoPath;
        BiosPathTextBox.Text = config.BiosPath;

        RefreshDefaultGameOptions(config.DefaultGame);
        AutoUpdateCheckBox.IsChecked = config.AutoUpdate;
        EmbedWindowCheckBox.IsChecked = config.EmbedWindow;
        FullscreenCheckBox.IsChecked = config.Fullscreen;
        BootToMultiplayerCheckBox.IsChecked = config.Patches.BootToMultiplayer;
        WidescreenCheckBox.IsChecked = config.Patches.Widescreen;
        ShowConsoleCheckBox.IsChecked = config.ShowConsole;
    }

    private void SaveConfiguration() {
        var existingConfig = Configuration.Load();
        var selectedDefaultGame = DefaultGameComboBox.SelectedItem as ComboBoxItem;

        var config = new ConfigurationData {
            Rac3IsoPath = Rac3IsoPathTextBox.Text,
            Rac4IsoPath = Rac4IsoPathTextBox.Text,
            BiosPath = BiosPathTextBox.Text,
            Region = existingConfig.Region,
            DefaultGame = selectedDefaultGame?.Tag?.ToString() ?? "None",
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

    private void IsoPathTextBox_TextChanged(object sender, TextChangedEventArgs e) {
        RefreshDefaultGameOptions();
        ValidateLaunchButton();
    }

    private void RefreshDefaultGameOptions(string? preferredDefaultGame = null) {
        var selectedDefaultGame = preferredDefaultGame
            ?? (DefaultGameComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString()
            ?? "None";
        selectedDefaultGame = ConfigurationData.NormalizeDefaultGame(selectedDefaultGame);

        DefaultGameComboBox.Items.Clear();
        DefaultGameComboBox.Items.Add(new ComboBoxItem {
            Content = "None (Show Game Select)",
            Tag = "None"
        });

        AddDefaultGameOption(Rac3IsoPathTextBox.Text, SupportedGame.Rac3, "Rac3");
        AddDefaultGameOption(Rac4IsoPathTextBox.Text, SupportedGame.Rac4, "Rac4");

        foreach (ComboBoxItem item in DefaultGameComboBox.Items) {
            if ((item.Tag?.ToString() ?? "") == selectedDefaultGame) {
                DefaultGameComboBox.SelectedItem = item;
                return;
            }
        }

        DefaultGameComboBox.SelectedIndex = 0;
    }

    private void AddDefaultGameOption(string isoPath, SupportedGame game, string tag) {
        if (string.IsNullOrWhiteSpace(isoPath))
            return;

        DefaultGameComboBox.Items.Add(new ComboBoxItem {
            Content = GetGameDisplayName(isoPath, game),
            Tag = tag
        });
    }

    private static string GetGameDisplayName(string isoPath, SupportedGame game) {
        return GameDisplayNames.GetDisplayName(GameDetector.ReadFromIso(isoPath), game);
    }

    private void ValidateLaunchButton() {
        bool hasAnyIso = HasAnyIsoPath();
        bool hasBios = !string.IsNullOrWhiteSpace(BiosPathTextBox.Text);
        bool isosValid = IsIsoSlotSupported(Rac3IsoPathTextBox.Text, SupportedGame.Rac3) &&
                         IsIsoSlotSupported(Rac4IsoPathTextBox.Text, SupportedGame.Rac4);

        LaunchButton.IsEnabled = hasAnyIso && hasBios && isosValid;
        SaveButton.IsEnabled = hasAnyIso && isosValid;
        SaveAndRelaunchButton.IsEnabled = hasAnyIso && hasBios && isosValid;
    }

    private bool HasAnyIsoPath() {
        return !string.IsNullOrWhiteSpace(Rac3IsoPathTextBox.Text) ||
               !string.IsNullOrWhiteSpace(Rac4IsoPathTextBox.Text);
    }

    private static bool IsIsoSlotSupported(string isoPath, SupportedGame expectedGame) {
        if (string.IsNullOrWhiteSpace(isoPath))
            return true;

        var info = GameDetector.ReadFromIso(isoPath);
        return info?.Game == expectedGame && GameSupport.GetUnsupportedMessage(info) == null;
    }

    private void GetRac3GameInfo_Click(object sender, RoutedEventArgs e) {
        ShowGameInfo(Rac3IsoPathTextBox.Text, SupportedGame.Rac3, "Up Your Arsenal");
    }

    private void GetRac4GameInfo_Click(object sender, RoutedEventArgs e) {
        ShowGameInfo(Rac4IsoPathTextBox.Text, SupportedGame.Rac4, "Deadlocked");
    }

    private void ClearRac3IsoPath_Click(object sender, RoutedEventArgs e) {
        Rac3IsoPathTextBox.Clear();
        ValidateLaunchButton();
    }

    private void ClearRac4IsoPath_Click(object sender, RoutedEventArgs e) {
        Rac4IsoPathTextBox.Clear();
        ValidateLaunchButton();
    }

    private void BrowseRac3Iso_Click(object sender, RoutedEventArgs e) {
        BrowseIso(Rac3IsoPathTextBox, SupportedGame.Rac3, "Select Up Your Arsenal ISO File");
    }

    private void BrowseRac4Iso_Click(object sender, RoutedEventArgs e) {
        BrowseIso(Rac4IsoPathTextBox, SupportedGame.Rac4, "Select Deadlocked ISO File");
    }

    private void BrowseIso(TextBox targetTextBox, SupportedGame expectedGame, string title) {
        var dialog = new OpenFileDialog {
            Filter = "ISO Files (*.iso)|*.iso|All Files (*.*)|*.*",
            Title = title
        };

        if (dialog.ShowDialog() == true) {
            targetTextBox.Text = dialog.FileName;
            var info = GameDetector.ReadFromIso(dialog.FileName);
            ShowIsoValidationMessage(info, expectedGame);
            ValidateLaunchButton();
        }
    }

    private void ShowGameInfo(string isoPath, SupportedGame expectedGame, string slotName) {
        if (string.IsNullOrWhiteSpace(isoPath)) {
            MessageBox.Show(
                $"No {slotName} ISO path set. Please browse for an ISO first.",
                "Get Game Info",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var info = GameDetector.ReadFromIso(isoPath);
        if (info == null) {
            MessageBox.Show(
                "Could not read game info from the ISO.\n\n" +
                "The file may not be a valid PS2 disc image, or SYSTEM.CNF could not be found.",
                "Get Game Info",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        ShowIsoValidationMessage(info, expectedGame);
        var message =
            $"Game:      {GameDisplayNames.GetDisplayName(info, expectedGame)}\n" +
            $"Game ID:   {(string.IsNullOrEmpty(info.GameId) ? "(unknown)" : info.GameId)}\n" +
            $"Region:    {info.RegionLabel}\n\n" +
            $"Boot line: {(string.IsNullOrEmpty(info.RawBootLine) ? "(none)" : info.RawBootLine)}";

        MessageBox.Show(
            message,
            "Game Info",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private bool ShowIsoValidationMessage(GameInfo? info, SupportedGame expectedGame) {
        if (info != null && info.Game != expectedGame) {
            MessageBox.Show(
                $"This ISO is {info.GameLabel}, but it was selected for the wrong game slot.",
                "Wrong ISO",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return true;
        }

        return ShowUnsupportedGameMessage(info);
    }

    private bool ShowUnsupportedGameMessage(GameInfo? info) {
        var message = GameSupport.GetUnsupportedMessage(info);
        if (message == null)
            return false;

        MessageBox.Show(
            message,
            "Unsupported Game Region",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        return true;
    }

    private bool ValidateSelectedIsoIsSupported() {
        if (!HasAnyIsoPath()) {
            MessageBox.Show(
                "Please select an Up Your Arsenal ISO, a Deadlocked ISO, or both before saving.",
                "Missing ISO",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        return ValidateIsoSlot(Rac3IsoPathTextBox.Text, SupportedGame.Rac3, "Up Your Arsenal") &&
               ValidateIsoSlot(Rac4IsoPathTextBox.Text, SupportedGame.Rac4, "Deadlocked");
    }

    private bool ValidateIsoSlot(string isoPath, SupportedGame expectedGame, string slotName) {
        if (string.IsNullOrWhiteSpace(isoPath))
            return true;

        var info = GameDetector.ReadFromIso(isoPath);
        if (info?.Game != expectedGame) {
            MessageBox.Show(
                $"The {slotName} ISO field does not contain a {slotName} ISO.",
                "Wrong ISO",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }

        return !ShowUnsupportedGameMessage(info);
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
        if (!ValidateSelectedIsoIsSupported())
            return;

        SaveConfiguration();
        
        if (_isHotkeyMode) {
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
        if (!ValidateSelectedIsoIsSupported())
            return;

        SaveConfiguration();

        var exePath = Environment.ProcessPath ?? AppContext.BaseDirectory;

        Process.Start(new ProcessStartInfo {
            FileName = exePath,
            UseShellExecute = true
        });

        Application.Current.Shutdown();
    }

    private void Launch_Click(object sender, RoutedEventArgs e) {
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

        if (!_isHotkeyMode && DialogResult != true && !_cancelled) {
            _cancelled = true;
        }
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e) {
        try {
            Process.Start(new ProcessStartInfo {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
        }
        catch (Exception ex) {
            MessageBox.Show(
                $"Unable to open link:\n\n{ex.Message}",
                "Navigation Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        e.Handled = true;
    }
}