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

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "desktop-ai-test-agent-artifacts-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
