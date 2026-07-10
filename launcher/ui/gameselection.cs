using System;
using System.Windows;
using System.Windows.Controls;

namespace UYALauncher;

public class GameSelectionWindow : Window {
    public SupportedGame? SelectedGame { get; private set; }
    public bool OpenSettingsRequested { get; private set; }

    public GameSelectionWindow(ConfigurationData config) {
        Title = "Horizon Launcher";
        Width = 560;
        Height = 340;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;

        ThemeHelper.ApplyTheme(this);

        var panel = new StackPanel {
            Margin = new Thickness(24),
            VerticalAlignment = VerticalAlignment.Center
        };

        panel.Children.Add(new TextBlock {
            Text = "Horizon Launcher",
            FontSize = 24,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 18)
        });

        if (!string.IsNullOrWhiteSpace(config.Rac3IsoPath)) {
            panel.Children.Add(CreateGameButton(
                $"Play {GetGameDisplayName(config.Rac3IsoPath, SupportedGame.Rac3)}",
                SupportedGame.Rac3));
        }

        if (!string.IsNullOrWhiteSpace(config.Rac4IsoPath)) {
            panel.Children.Add(CreateGameButton(
                $"Play {GetGameDisplayName(config.Rac4IsoPath, SupportedGame.Rac4)}",
                SupportedGame.Rac4));
        }

        panel.Children.Add(CreateCommandButton("Open Settings", () => {
            OpenSettingsRequested = true;
            DialogResult = true;
        }));

        panel.Children.Add(CreateCommandButton("Exit", () => {
            DialogResult = false;
        }));

        Content = panel;
    }

    private static string GetGameDisplayName(string isoPath, SupportedGame game) {
        return GameDisplayNames.GetDisplayName(GameDetector.ReadFromIso(isoPath), game);
    }

    private Button CreateGameButton(string text, SupportedGame game) {
        return CreateButton(text, "PrimarySmallButton", () => {
            SelectedGame = game;
            DialogResult = true;
        });
    }

    private Button CreateCommandButton(string text, Action action) {
        return CreateButton(text, "SecondaryButton", action);
    }

    private Button CreateButton(string text, string styleKey, Action action) {
        var button = new Button {
            Content = text,
            MinHeight = 42,
            Margin = new Thickness(0, 0, 0, 10)
        };

        if (TryFindResource(styleKey) is Style style)
            button.Style = style;

        button.Click += (_, _) => action();
        return button;
    }
}
