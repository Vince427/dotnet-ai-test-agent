using DesktopAiTestAgent.AgentRunner;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner.Tests;

/// <summary>
/// V7 prompt preview: renders the exact prompt the LLM would receive for a test (key-free,
/// reuses PromptBuilder), with the live UI snapshot as a labelled placeholder.
/// </summary>
public sealed class PromptPreviewTests
{
    [Fact]
    public void BuildForTest_IncludesGoalCategoryAllowedActionsAndSuccess()
    {
        var test = new TestDefinition
        {
            Id = "X-001",
            Goal = "Log in with admin / password123 and confirm",
            SuccessCondition = "Login successful",
            TargetWindow = "Sample Login App",
            Category = TestCategory.Smoke,
            AllowedActions = ["Click", "Done"]
        };

        var prompt = PromptPreview.BuildForTest(test);

        Assert.Contains("Log in with admin", prompt);
        Assert.Contains("Login successful", prompt);
        Assert.Contains("Click, Done", prompt);                 // allowed-actions line
        Assert.Contains("CATEGORY: Smoke Test", prompt);        // category framing
        Assert.Contains("captured live at runtime", prompt);    // honest snapshot placeholder
    }

    [Fact]
    public void BuildForTest_RedactsSecretsInTheGoal()
    {
        var test = new TestDefinition { Id = "X-002", Goal = "authenticate using password=hunter2" };

        var prompt = PromptPreview.BuildForTest(test);

        Assert.DoesNotContain("hunter2", prompt);
        Assert.Contains("[REDACTED]", prompt);
    }
}
