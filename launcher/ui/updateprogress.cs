using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace UYALauncher;

public class UpdateProgressWindow : Window
{
    private readonly TextBlock _statusText;
    private readonly Rectangle _progressIndicator;
    private readonly Border _progressContainer;

    public UpdateProgressWindow() {
        Title = "Downloading Update";
        Width = 450;
        Height = 180;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.SingleBorderWindow;
        
        // Apply theme (high contrast or dark)
        ThemeHelper.ApplyTheme(this);

        var grid = new Grid();
        grid.Margin = new Thickness(20);

        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });

        _statusText = new TextBlock {
            Text = "Preparing download...",
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 15),
            Foreground = SystemParameters.HighContrast 
                ? SystemColors.WindowTextBrush 
                : new SolidColorBrush(Color.FromRgb(255, 255, 255)),
            FontSize = 14
        };
        Grid.SetRow(_statusText, 0);
        grid.Children.Add(_statusText);

        // Create custom progress bar using Rectangle
        _progressContainer = new Border {
            Height = 24,
            Background = SystemParameters.HighContrast 
                ? SystemColors.ControlBrush 
                : new SolidColorBrush(Color.FromRgb(45, 45, 45)),
            BorderBrush = SystemParameters.HighContrast 
                ? SystemColors.ControlDarkBrush 
                : new SolidColorBrush(Color.FromRgb(60, 60, 60)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2)
        };

        _progressIndicator = new Rectangle {
            Fill = SystemParameters.HighContrast 
                ? SystemColors.HighlightBrush 
                : new SolidColorBrush(Color.FromRgb(0, 120, 212)),
            HorizontalAlignment = HorizontalAlignment.Left,
            Width = 0
        };

        _progressContainer.Child = _progressIndicator;
        Grid.SetRow(_progressContainer, 2);
        grid.Children.Add(_progressContainer);

        Content = grid;
    }

    public void UpdateProgress(int percent) {
        Dispatcher.Invoke(() => {
            _statusText.Text = $"Downloading update... {percent}%";
            
            // Update rectangle width based on percentage
            var containerWidth = _progressContainer.ActualWidth - 2; // Subtract border
            _progressIndicator.Width = (containerWidth * percent) / 100.0;

            if (percent >= 100) {
                _statusText.Text = "Download complete!";
            }
        });
    }
}
