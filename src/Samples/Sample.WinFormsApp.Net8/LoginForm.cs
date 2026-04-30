namespace Sample.WinFormsApp.Net8;

public sealed class LoginForm : Form
{
    private readonly TextBox _usernameTextBox;
    private readonly TextBox _passwordTextBox;
    private readonly Button _loginButton;
    private readonly Label _statusLabel;

    public LoginForm()
    {
        Text = "Sample Login App (.NET 8)";
        Width = 430;
        Height = 260;
        StartPosition = FormStartPosition.CenterScreen;

        var titleLabel = new Label { Text = ".NET 8 sample", Left = 20, Top = 20, Width = 220, Font = new Font("Segoe UI", 12, FontStyle.Bold) };
        var usernameLabel = new Label { Text = "Username", Left = 20, Top = 65, Width = 90 };
        var passwordLabel = new Label { Text = "Password", Left = 20, Top = 100, Width = 90 };

        _usernameTextBox = new TextBox { Left = 130, Top = 62, Width = 220, Name = "txtUsername" };
        _passwordTextBox = new TextBox { Left = 130, Top = 97, Width = 220, Name = "txtPassword", UseSystemPasswordChar = true };
        _loginButton = new Button { Left = 130, Top = 135, Width = 120, Height = 32, Text = "Login", Name = "btnLogin" };
        _statusLabel = new Label { Left = 20, Top = 182, Width = 330, Height = 24, Text = "Waiting", Name = "lblStatus" };

        _loginButton.Click += OnLoginClicked;

        Controls.Add(titleLabel);
        Controls.Add(usernameLabel);
        Controls.Add(_usernameTextBox);
        Controls.Add(passwordLabel);
        Controls.Add(_passwordTextBox);
        Controls.Add(_loginButton);
        Controls.Add(_statusLabel);
    }

    private void OnLoginClicked(object? sender, EventArgs e)
    {
        if (_usernameTextBox.Text == "admin" && _passwordTextBox.Text == "password123")
        {
            _statusLabel.Text = "Login successful";
            _statusLabel.ForeColor = Color.DarkGreen;
        }
        else
        {
            _statusLabel.Text = "Login failed";
            _statusLabel.ForeColor = Color.DarkRed;
        }
    }
}
