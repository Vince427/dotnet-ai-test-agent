using System.Windows;
using System.Windows.Media;

namespace Sample.WpfApp.Net48
{
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
}