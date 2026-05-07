using System;
using System.Drawing;
using System.Windows.Forms;

namespace Sample.WinFormsApp.Net48
{
    public sealed class LoginForm : Form
    {
        private readonly TextBox _usernameTextBox;
        private readonly TextBox _passwordTextBox;
        private readonly Button _loginButton;
        private readonly Label _statusLabel;
        private readonly TextBox _displayNameTextBox;
        private readonly TextBox _emailTextBox;
        private readonly CheckBox _activeCheckBox;
        private readonly Button _saveProfileButton;
        private readonly Label _profileStatusLabel;

        public LoginForm()
        {
            Text = "Sample Login App (.NET Framework 4.8)";
            Width = 500;
            Height = 470;
            StartPosition = FormStartPosition.CenterScreen;

            var titleLabel = new Label { Text = ".NET Framework 4.8 sample", Left = 20, Top = 20, Width = 260, Font = new Font("Segoe UI", 12, FontStyle.Bold) };
            var usernameLabel = new Label { Text = "Username", Left = 20, Top = 65, Width = 90 };
            var passwordLabel = new Label { Text = "Password", Left = 20, Top = 100, Width = 90 };

            _usernameTextBox = new TextBox { Left = 130, Top = 62, Width = 220, Name = "txtUsername" };
            _passwordTextBox = new TextBox { Left = 130, Top = 97, Width = 220, Name = "txtPassword", UseSystemPasswordChar = true };
            _loginButton = new Button { Left = 130, Top = 135, Width = 120, Height = 32, Text = "Login", Name = "btnLogin" };
            _statusLabel = new Label { Left = 20, Top = 182, Width = 330, Height = 24, Text = "Waiting", Name = "lblStatus" };

            var profileTitleLabel = new Label { Text = "Profile form", Left = 20, Top = 230, Width = 220, Font = new Font("Segoe UI", 11, FontStyle.Bold) };
            var displayNameLabel = new Label { Text = "Display name", Left = 20, Top = 270, Width = 100 };
            var emailLabel = new Label { Text = "Email", Left = 20, Top = 305, Width = 100 };

            _displayNameTextBox = new TextBox { Left = 130, Top = 267, Width = 260, Name = "txtDisplayName" };
            _emailTextBox = new TextBox { Left = 130, Top = 302, Width = 260, Name = "txtEmail" };
            _activeCheckBox = new CheckBox { Left = 130, Top = 337, Width = 140, Text = "Active", Name = "chkActive" };
            _saveProfileButton = new Button { Left = 130, Top = 370, Width = 120, Height = 32, Text = "Save Profile", Name = "btnSaveProfile" };
            _profileStatusLabel = new Label { Left = 20, Top = 412, Width = 420, Height = 24, Text = "Profile waiting", Name = "lblProfileStatus" };

            _loginButton.Click += OnLoginClicked;
            _saveProfileButton.Click += OnSaveProfileClicked;

            Controls.Add(titleLabel);
            Controls.Add(usernameLabel);
            Controls.Add(_usernameTextBox);
            Controls.Add(passwordLabel);
            Controls.Add(_passwordTextBox);
            Controls.Add(_loginButton);
            Controls.Add(_statusLabel);
            Controls.Add(profileTitleLabel);
            Controls.Add(displayNameLabel);
            Controls.Add(_displayNameTextBox);
            Controls.Add(emailLabel);
            Controls.Add(_emailTextBox);
            Controls.Add(_activeCheckBox);
            Controls.Add(_saveProfileButton);
            Controls.Add(_profileStatusLabel);
        }

        private void OnLoginClicked(object sender, EventArgs e)
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

        private void OnSaveProfileClicked(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(_displayNameTextBox.Text) &&
                _emailTextBox.Text.Contains("@") &&
                _activeCheckBox.Checked)
            {
                _profileStatusLabel.Text = "Profile saved";
                _profileStatusLabel.ForeColor = Color.DarkGreen;
            }
            else
            {
                _profileStatusLabel.Text = "Profile validation failed";
                _profileStatusLabel.ForeColor = Color.DarkRed;
            }
        }
    }
}
