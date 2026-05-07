namespace Sample.MauiApp;

public partial class MainPage : ContentPage
{
	public MainPage()
	{
		InitializeComponent();
	}

	private void OnLoginClicked(object sender, EventArgs e)
	{
		if (txtUsername.Text == "admin" && txtPassword.Text == "password123")
		{
			lblStatus.Text = "Login successful";
			lblStatus.TextColor = Colors.Green;
		}
		else
		{
			lblStatus.Text = "Login failed";
			lblStatus.TextColor = Colors.Red;
		}
	}

	private void OnSaveProfileClicked(object sender, EventArgs e)
	{
		if (!string.IsNullOrWhiteSpace(txtDisplayName.Text) &&
			txtEmail.Text.Contains('@') &&
			chkActive.IsChecked)
		{
			lblProfileStatus.Text = "Profile saved";
			lblProfileStatus.TextColor = Colors.Green;
		}
		else
		{
			lblProfileStatus.Text = "Profile validation failed";
			lblProfileStatus.TextColor = Colors.Red;
		}
	}
}
