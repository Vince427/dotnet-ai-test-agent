using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Sample.WpfApp;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void BtnLogin_Click(object sender, RoutedEventArgs e)
    {
        if (txtUsername.Text == "admin" && txtPassword.Password == "password123")
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
}