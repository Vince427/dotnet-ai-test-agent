namespace Sample.WinFormsApp.Net8;

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
    private readonly RadioButton _standardModeRadioButton;
    private readonly RadioButton _premiumModeRadioButton;
    private readonly ComboBox _environmentComboBox;
    private readonly ListBox _moduleListBox;
    private readonly DataGridView _casesGrid;
    private readonly Button _saveControlsButton;
    private readonly Button _openConfirmationButton;
    private readonly Button _confirmDialogButton;
    private readonly Button _cancelDialogButton;
    private readonly Button _enableProtectedActionButton;
    private readonly Button _protectedActionButton;
    private readonly Button _loadCasesButton;
    private readonly Label _controlsStatusLabel;
    private readonly Panel _confirmationPanel;

    public LoginForm()
    {
        Text = "Sample Login App (.NET 8)";
        Width = 760;
        Height = 820;
        AutoScroll = true;
        StartPosition = FormStartPosition.CenterScreen;

        var titleLabel = new Label { Text = ".NET 8 sample", Left = 20, Top = 20, Width = 220, Font = new Font("Segoe UI", 12, FontStyle.Bold) };
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

        var controlsTitleLabel = new Label { Text = "Test controls", Left = 20, Top = 455, Width = 220, Font = new Font("Segoe UI", 11, FontStyle.Bold) };
        var modeLabel = new Label { Text = "Mode", Left = 20, Top = 495, Width = 100 };
        _standardModeRadioButton = new RadioButton { Left = 130, Top = 492, Width = 100, Text = "Standard", Name = "rdoStandard", Checked = true };
        _premiumModeRadioButton = new RadioButton { Left = 240, Top = 492, Width = 100, Text = "Premium", Name = "rdoPremium" };
        var environmentLabel = new Label { Text = "Environment", Left = 20, Top = 530, Width = 100 };
        _environmentComboBox = new ComboBox { Left = 130, Top = 527, Width = 170, Name = "cmbEnvironment", DropDownStyle = ComboBoxStyle.DropDownList };
        _environmentComboBox.Items.AddRange(new object[] { "Dev", "Staging", "Production" });
        _environmentComboBox.SelectedIndex = 0;
        var moduleLabel = new Label { Text = "Module", Left = 20, Top = 565, Width = 100 };
        _moduleListBox = new ListBox { Left = 130, Top = 562, Width = 170, Height = 72, Name = "lstModules" };
        _moduleListBox.Items.AddRange(new object[] { "Login", "Billing", "Reports" });
        _moduleListBox.SelectedIndex = 0;
        // Start past the Premium radio (Left 240 + Width 100 = 340) so the "C" isn't clipped.
        var casesLabel = new Label { Text = "Case grid", Left = 350, Top = 495, Width = 100 };
        _casesGrid = new DataGridView
        {
            Left = 330,
            Top = 527,
            Width = 360,
            Height = 120,
            Name = "gridCases",
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };
        _casesGrid.Columns.Add("Feature", "Feature");
        _casesGrid.Columns.Add("Status", "Status");
        _casesGrid.Rows.Add("Login", "Ready");
        _casesGrid.Rows.Add("Profile", "Ready");
        _casesGrid.Rows.Add("Reports", "Queued");

        _saveControlsButton = new Button { Left = 130, Top = 650, Width = 120, Height = 32, Text = "Save Controls", Name = "btnSaveControls" };
        _openConfirmationButton = new Button { Left = 270, Top = 650, Width = 150, Height = 32, Text = "Open Confirmation", Name = "btnOpenConfirmation" };
        _loadCasesButton = new Button { Left = 445, Top = 650, Width = 120, Height = 32, Text = "Load Cases", Name = "btnLoadCases" };
        _enableProtectedActionButton = new Button { Left = 130, Top = 688, Width = 120, Height = 32, Text = "Enable Action", Name = "btnEnableProtectedAction" };
        _protectedActionButton = new Button { Left = 270, Top = 688, Width = 150, Height = 32, Text = "Protected Action", Name = "btnProtectedAction", Enabled = false };
        _controlsStatusLabel = new Label { Left = 20, Top = 742, Width = 670, Height = 24, Text = "Controls waiting", Name = "lblControlsStatus" };

        _confirmationPanel = new Panel { Left = 430, Top = 686, Width = 260, Height = 52, Name = "pnlConfirmationDialog", BorderStyle = BorderStyle.FixedSingle, BackColor = Color.LightYellow, Visible = false };
        var confirmationLabel = new Label { Left = 8, Top = 8, Width = 170, Text = "Confirm sample action?", Name = "lblConfirmationTitle" };
        _confirmDialogButton = new Button { Left = 8, Top = 26, Width = 80, Height = 22, Text = "Confirm", Name = "btnConfirmDialog" };
        _cancelDialogButton = new Button { Left = 98, Top = 26, Width = 80, Height = 22, Text = "Cancel", Name = "btnCancelDialog" };
        _confirmationPanel.Controls.Add(confirmationLabel);
        _confirmationPanel.Controls.Add(_confirmDialogButton);
        _confirmationPanel.Controls.Add(_cancelDialogButton);

        _loginButton.Click += OnLoginClicked;
        _saveProfileButton.Click += OnSaveProfileClicked;
        _saveControlsButton.Click += OnSaveControlsClicked;
        _openConfirmationButton.Click += OnOpenConfirmationClicked;
        _confirmDialogButton.Click += OnConfirmDialogClicked;
        _cancelDialogButton.Click += OnCancelDialogClicked;
        _enableProtectedActionButton.Click += OnEnableProtectedActionClicked;
        _protectedActionButton.Click += OnProtectedActionClicked;
        _loadCasesButton.Click += OnLoadCasesClicked;

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
        Controls.Add(controlsTitleLabel);
        Controls.Add(modeLabel);
        Controls.Add(_standardModeRadioButton);
        Controls.Add(_premiumModeRadioButton);
        Controls.Add(environmentLabel);
        Controls.Add(_environmentComboBox);
        Controls.Add(moduleLabel);
        Controls.Add(_moduleListBox);
        Controls.Add(casesLabel);
        Controls.Add(_casesGrid);
        Controls.Add(_saveControlsButton);
        Controls.Add(_openConfirmationButton);
        Controls.Add(_loadCasesButton);
        Controls.Add(_enableProtectedActionButton);
        Controls.Add(_protectedActionButton);
        Controls.Add(_controlsStatusLabel);
        Controls.Add(_confirmationPanel);
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

    private void OnSaveProfileClicked(object? sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_displayNameTextBox.Text) &&
            _emailTextBox.Text.Contains('@') &&
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

    private void OnSaveControlsClicked(object? sender, EventArgs e)
    {
        var mode = _premiumModeRadioButton.Checked ? "Premium" : "Standard";
        var environment = _environmentComboBox.SelectedItem?.ToString() ?? "None";
        var module = _moduleListBox.SelectedItem?.ToString() ?? "None";
        _controlsStatusLabel.Text = $"Selection saved: {mode} / {environment} / {module} / {_casesGrid.Rows.Count} cases";
        _controlsStatusLabel.ForeColor = Color.DarkGreen;
    }

    private void OnOpenConfirmationClicked(object? sender, EventArgs e)
    {
        _confirmationPanel.Visible = true;
        _confirmationPanel.BringToFront();
        _controlsStatusLabel.Text = "Modal waiting";
        _controlsStatusLabel.ForeColor = Color.DarkGoldenrod;
    }

    private void OnConfirmDialogClicked(object? sender, EventArgs e)
    {
        _confirmationPanel.Visible = false;
        _controlsStatusLabel.Text = "Modal confirmed";
        _controlsStatusLabel.ForeColor = Color.DarkGreen;
    }

    private void OnCancelDialogClicked(object? sender, EventArgs e)
    {
        _confirmationPanel.Visible = false;
        _controlsStatusLabel.Text = "Modal cancelled";
        _controlsStatusLabel.ForeColor = Color.DarkRed;
    }

    private void OnEnableProtectedActionClicked(object? sender, EventArgs e)
    {
        _protectedActionButton.Enabled = true;
        _controlsStatusLabel.Text = "Protected action enabled";
        _controlsStatusLabel.ForeColor = Color.DarkGoldenrod;
    }

    private void OnProtectedActionClicked(object? sender, EventArgs e)
    {
        _controlsStatusLabel.Text = "Protected action completed";
        _controlsStatusLabel.ForeColor = Color.DarkGreen;
    }

    private async void OnLoadCasesClicked(object? sender, EventArgs e)
    {
        _loadCasesButton.Enabled = false;
        _controlsStatusLabel.Text = "Loading cases";
        _controlsStatusLabel.ForeColor = Color.DarkGoldenrod;

        await Task.Delay(500);

        if (_casesGrid.Rows.Count < 4)
            _casesGrid.Rows.Add("Async", "Loaded");

        _controlsStatusLabel.Text = "Async load complete";
        _controlsStatusLabel.ForeColor = Color.DarkGreen;
        _loadCasesButton.Enabled = true;
    }
}
