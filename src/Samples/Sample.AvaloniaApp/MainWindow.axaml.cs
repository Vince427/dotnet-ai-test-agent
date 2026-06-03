using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace Sample.AvaloniaApp;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnLoginClicked(object? sender, RoutedEventArgs e)
    {
        if (txtUsername.Text == "admin" && txtPassword.Text == "password123")
        {
            lblStatus.Text = "Login successful";
            lblStatus.Foreground = Brushes.Green;
        }
        else
        {
            lblStatus.Text = "Login failed";
            lblStatus.Foreground = Brushes.Red;
        }
    }

    private void OnEnableProtectedActionClicked(object? sender, RoutedEventArgs e)
    {
        btnProtectedAction.IsEnabled = true;
        lblControlsStatus.Text = "Protected action enabled";
        lblControlsStatus.Foreground = Brushes.DarkGoldenrod;
    }

    private void OnProtectedActionClicked(object? sender, RoutedEventArgs e)
    {
        lblControlsStatus.Text = "Protected action completed";
        lblControlsStatus.Foreground = Brushes.Green;
    }
}
