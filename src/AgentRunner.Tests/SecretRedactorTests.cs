using DesktopAiTestAgent.AgentRunner;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner.Tests;

public sealed class SecretRedactorTests
{
    [Theory]
    [InlineData("txtPassword")]
    [InlineData("client_secret")]
    [InlineData("AuthToken")]
    [InlineData("apiKeyInput")]
    [InlineData("api_key_input")]
    [InlineData("cvvField")]
    [InlineData("ssnBox")]
    public void RedactValueForIdentifierMasksDefaultSensitivePatterns(string identifier)
    {
        var redactor = new SecretRedactor();

        var redacted = redactor.RedactValueForIdentifier(identifier, "sensitive-value");

        Assert.Equal("[REDACTED]", redacted);
    }

    [Fact]
    public void AgentMemoryRedactsSensitiveFactsByKey()
    {
        var memory = new AgentMemory(new SecretRedactor());

        memory.AddFact("entered_txtPassword", "hunter2");

        var facts = memory.GetFactsString();
        Assert.Contains("- entered_txtPassword: [REDACTED]", facts);
        Assert.DoesNotContain("hunter2", facts);
    }

    [Fact]
    public void RedactSnapshotForPromptMasksValueWhenAutomationIdIsSensitive()
    {
        var redactor = new SecretRedactor();
        var snapshot = new UiSnapshot(
            "Login",
            [
                new UiElement
                {
                    AutomationId = "txtPassword",
                    Name = "Password",
                    ControlType = "Edit",
                    Value = "hunter2"
                }
            ]);

        var promptText = redactor.RedactSnapshotForPrompt(snapshot);

        Assert.Contains("txtPassword", promptText);
        Assert.Contains("[REDACTED]", promptText);
        Assert.DoesNotContain("hunter2", promptText);
    }

    [Fact]
    public void RedactSnapshotForPromptMasksValueWhenNameIsSensitive()
    {
        var redactor = new SecretRedactor();
        var snapshot = new UiSnapshot(
            "Settings",
            [
                new UiElement
                {
                    AutomationId = "input1",
                    Name = "API Key",
                    ControlType = "Edit",
                    Value = "sk-test-123"
                }
            ]);

        var promptText = redactor.RedactSnapshotForPrompt(snapshot);

        Assert.Contains("input1", promptText);
        Assert.Contains("[REDACTED]", promptText);
        Assert.DoesNotContain("sk-test-123", promptText);
    }

    [Fact]
    public void RedactSnapshotForPromptLeavesNormalFieldsReadable()
    {
        var redactor = new SecretRedactor();
        var snapshot = new UiSnapshot(
            "Profile",
            [
                new UiElement
                {
                    AutomationId = "txtEmail",
                    Name = "Email",
                    ControlType = "Edit",
                    Value = "ada@example.com"
                }
            ]);

        var promptText = redactor.RedactSnapshotForPrompt(snapshot);

        Assert.Contains("ada@example.com", promptText);
        Assert.DoesNotContain("[REDACTED]", promptText);
    }
}
