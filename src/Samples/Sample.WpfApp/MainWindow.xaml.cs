using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Sample.WpfApp;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly ObservableCollection<TestCaseRow> _caseRows = new();

    public MainWindow()
    {
        InitializeComponent();

        _caseRows.Add(new TestCaseRow("Login", "Ready"));
        _caseRows.Add(new TestCaseRow("Profile", "Ready"));
        _caseRows.Add(new TestCaseRow("Reports", "Queued"));
        gridCases.ItemsSource = _caseRows;
        cmbEnvironment.SelectedIndex = 0;
        lstModules.SelectedIndex = 0;
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

    private void BtnSaveProfile_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(txtDisplayName.Text) &&
            txtEmail.Text.Contains('@') &&
            chkActive.IsChecked == true)
        {
            lblProfileStatus.Text = "Profile saved";
            lblProfileStatus.Foreground = Brushes.Green;
        }
        else
        {
            lblProfileStatus.Text = "Profile validation failed";
            lblProfileStatus.Foreground = Brushes.Red;
        }
    }

    private void BtnSaveControls_Click(object sender, RoutedEventArgs e)
    {
        var mode = rdoPremium.IsChecked == true ? "Premium" : "Standard";
        var environment = (cmbEnvironment.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "None";
        var module = (lstModules.SelectedItem as ListBoxItem)?.Content?.ToString() ?? "None";
        lblControlsStatus.Text = $"Selection saved: {mode} / {environment} / {module} / {_caseRows.Count} cases";
        lblControlsStatus.Foreground = Brushes.Green;
    }

    private void BtnOpenConfirmation_Click(object sender, RoutedEventArgs e)
    {
        confirmationDialog.Visibility = Visibility.Visible;
        lblControlsStatus.Text = "Modal waiting";
        lblControlsStatus.Foreground = Brushes.DarkGoldenrod;
    }

    private void BtnConfirmDialog_Click(object sender, RoutedEventArgs e)
    {
        confirmationDialog.Visibility = Visibility.Collapsed;
        lblControlsStatus.Text = "Modal confirmed";
        lblControlsStatus.Foreground = Brushes.Green;
    }

    private void BtnCancelDialog_Click(object sender, RoutedEventArgs e)
    {
        confirmationDialog.Visibility = Visibility.Collapsed;
        lblControlsStatus.Text = "Modal cancelled";
        lblControlsStatus.Foreground = Brushes.Red;
    }

    private void BtnEnableProtectedAction_Click(object sender, RoutedEventArgs e)
    {
        btnProtectedAction.IsEnabled = true;
        lblControlsStatus.Text = "Protected action enabled";
        lblControlsStatus.Foreground = Brushes.DarkGoldenrod;
    }

    private void BtnProtectedAction_Click(object sender, RoutedEventArgs e)
    {
        lblControlsStatus.Text = "Protected action completed";
        lblControlsStatus.Foreground = Brushes.Green;
    }

    private async void BtnLoadCases_Click(object sender, RoutedEventArgs e)
    {
        btnLoadCases.IsEnabled = false;
        lblControlsStatus.Text = "Loading cases";
        lblControlsStatus.Foreground = Brushes.DarkGoldenrod;

        await Task.Delay(500);

        if (_caseRows.Count < 4)
            _caseRows.Add(new TestCaseRow("Async", "Loaded"));

        lblControlsStatus.Text = "Async load complete";
        lblControlsStatus.Foreground = Brushes.Green;
        btnLoadCases.IsEnabled = true;
    }

    public sealed class TestCaseRow
    {
        public TestCaseRow(string feature, string status)
        {
            Feature = feature;
            Status = status;
        }

        public string Feature { get; }

        public string Status { get; }
    }
}
