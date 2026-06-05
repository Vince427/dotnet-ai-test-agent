using System.Text.Json;
using DesktopAiTestAgent.AgentRunner;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner.Tests;

public sealed class ArtifactWriterTests
{
    [Fact]
    public void WriteReportSerializesEvidenceLevelAsString()
    {
        var tempDir = CreateTempDirectory();
        var writer = new ArtifactWriter(tempDir);
        var artifact = new RunArtifact
        {
            RunId = "evidence1",
            EvidenceLevel = EvidenceLevel.Full,
            GoalDescription = "Capture evidence.",
            TargetWindow = "Sample"
        };

        writer.WriteReport(artifact);

        var json = File.ReadAllText(Path.Combine(tempDir, "evidence1", "report.json"));
        Assert.Contains("\"evidenceLevel\": \"Full\"", json);
    }

    [Fact]
    public void SaveUiTreeSnapshotWritesReadableJson()
    {
        var tempDir = CreateTempDirectory();
        var writer = new ArtifactWriter(tempDir);
        var snapshot = new UiSnapshot(
            "Sample Window",
            [
                new UiElement
                {
                    AutomationId = "btnLogin",
                    Name = "Login",
                    ControlType = "Button",
                    IsEnabled = true
                }
            ],
            "Ready");

        var path = writer.SaveUiTreeSnapshot("run12345", 1, snapshot);

        Assert.True(File.Exists(path));
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal("Sample Window", document.RootElement.GetProperty("windowTitle").GetString());
        Assert.Equal("btnLogin", document.RootElement.GetProperty("elements")[0].GetProperty("automationId").GetString());
    }

    [Fact]
    public void SaveUiTreeSnapshotRedactsSensitiveElementValues()
    {
        var tempDir = CreateTempDirectory();
        var writer = new ArtifactWriter(tempDir, new SecretRedactor());
        var snapshot = new UiSnapshot(
            "Sample Window",
            [
                new UiElement
                {
                    AutomationId = "txtPassword",
                    Name = "Password",
                    ControlType = "Edit",
                    Value = "hunter2",
                    IsEnabled = true
                }
            ]);

        var path = writer.SaveUiTreeSnapshot("run12345", 1, snapshot);

        var json = File.ReadAllText(path);
        Assert.Contains("[REDACTED]", json);
        Assert.DoesNotContain("hunter2", json);
    }

    [Fact]
    public void WriteSummaryIncludesFailureCodesAndMessages()
    {
        var tempDir = CreateTempDirectory();
        var writer = new ArtifactWriter(tempDir);
        var artifact = new RunArtifact
        {
            RunId = "failure-code-demo",
            GoalDescription = "Demonstrate a missing target.",
            TargetWindow = "Sample",
            Result = "Failed",
            Steps =
            [
                new RunStep
                {
                    StepNumber = 1,
                    ActionType = "Click",
                    ActionTarget = "btnDoesNotExist",
                    Outcome = "Failed",
                    FailureCode = "action_target_not_found",
                    FailureMessage = "Action target was not present in the latest UI snapshot.",
                    ScoreDelta = -5,
                    CumulativeScore = -5
                }
            ]
        };

        writer.WriteSummary(artifact);

        var summary = File.ReadAllText(Path.Combine(tempDir, "failure-code-demo", "summary.md"));
        Assert.Contains("action_target_not_found", summary);
        Assert.Contains("Action target was not present", summary);
    }

    [Fact]
    public void WriteSummaryIncludesSelectorHealingSectionWithScreenshot()
    {
        var tempDir = CreateTempDirectory();
        var writer = new ArtifactWriter(tempDir);
        var artifact = new RunArtifact
        {
            RunId = "heal-demo",
            GoalDescription = "Drifted selector.",
            TargetWindow = "Sample",
            Result = "Failed",
            Steps =
            [
                new RunStep
                {
                    StepNumber = 1,
                    ActionType = "Click",
                    ActionTarget = "btnLogn",
                    Outcome = "Failed",
                    FailureCode = "action_target_not_found",
                    ScreenshotPath = Path.Combine(tempDir, "heal-demo", "screenshots", "step_001.png"),
                    HealingSuggestion = new HealingSuggestion
                    {
                        OldTarget = "btnLogn",
                        NewTarget = "btnLogin",
                        ControlType = "Button",
                        Confidence = 92,
                        Rationale = "'btnLogn' not found; closest present element is 'btnLogin' [Button], 92% match by automationId."
                    }
                }
            ]
        };

        writer.WriteSummary(artifact);

        var summary = File.ReadAllText(Path.Combine(tempDir, "heal-demo", "summary.md"));
        Assert.Contains("## Selector Healing Suggestions", summary);
        Assert.Contains("`btnLogn` → `btnLogin`", summary);
        Assert.Contains("92% match", summary);
        // The drift screenshot is linked relatively so a human can see the live UI.
        Assert.Contains("(screenshots/step_001.png)", summary);
    }

    [Fact]
    public void WriteSummaryOmitsHealingSectionWhenNoSuggestions()
    {
        var tempDir = CreateTempDirectory();
        var writer = new ArtifactWriter(tempDir);
        var artifact = new RunArtifact
        {
            RunId = "no-heal",
            GoalDescription = "Clean run.",
            TargetWindow = "Sample",
            Result = "Passed",
            Steps = [new RunStep { StepNumber = 1, ActionType = "Done", Outcome = "Succeeded" }]
        };

        writer.WriteSummary(artifact);

        var summary = File.ReadAllText(Path.Combine(tempDir, "no-heal", "summary.md"));
        Assert.DoesNotContain("Selector Healing", summary);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "desktop-ai-test-agent-artifacts-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
