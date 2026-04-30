namespace DesktopAiTestAgent.Core;

public sealed class UiSnapshot
{
    public UiSnapshot(string windowTitle, string usernameFieldId, string passwordFieldId, string loginButtonId, string statusLabelId, string statusText)
    {
        WindowTitle = windowTitle;
        UsernameFieldId = usernameFieldId;
        PasswordFieldId = passwordFieldId;
        LoginButtonId = loginButtonId;
        StatusLabelId = statusLabelId;
        StatusText = statusText;
    }

    public string WindowTitle { get; }
    public string UsernameFieldId { get; }
    public string PasswordFieldId { get; }
    public string LoginButtonId { get; }
    public string StatusLabelId { get; }
    public string StatusText { get; }
}
