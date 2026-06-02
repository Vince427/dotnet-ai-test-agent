namespace DesktopAiTestAgent.AgentRunner.Tests;

/// <summary>
/// The scripted agent-action JSON for the WinForms login happy path, shared by the
/// always-run <see cref="MockLlmServerTests"/> and the gated <see cref="LoginE2ETests"/>.
/// Targets match the .NET 8 sample form's automation ids (LoginForm.cs) and the
/// success label text "Login successful".
/// </summary>
internal static class LoginScript
{
    public const string EnterUsername =
        "{\"actionType\":\"EnterText\",\"automationId\":\"txtUsername\",\"value\":\"admin\",\"reason\":\"enter username\",\"confidence\":95}";

    public const string EnterPassword =
        "{\"actionType\":\"EnterText\",\"automationId\":\"txtPassword\",\"value\":\"password123\",\"reason\":\"enter password\",\"confidence\":95}";

    public const string ClickLogin =
        "{\"actionType\":\"Click\",\"automationId\":\"btnLogin\",\"value\":null,\"reason\":\"submit login\",\"confidence\":95}";

    public const string Done =
        "{\"actionType\":\"Done\",\"automationId\":null,\"value\":null,\"reason\":\"login succeeded\",\"confidence\":95}";
}
