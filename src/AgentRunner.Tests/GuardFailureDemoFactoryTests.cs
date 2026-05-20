using System.Text.Json;
using DesktopAiTestAgent.AgentRunner;

namespace DesktopAiTestAgent.AgentRunner.Tests;

public sealed class GuardFailureDemoFactoryTests
{
    [Fact]
    public void CreateAllIncludesTheV2CGuardFailureScenarios()
    {
        var startedAt = new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc);

        var demos = GuardFailureDemoFactory.CreateAll(startedAt).ToList();

        Assert.Equal(
            [
                "GUARD-MISSING-TARGET-001",
                "GUARD-CRASH-001",
                "GUARD-EMPTY-UI-001",
                "GUARD-UNEXPECTED-MODAL-001"
            ],
            demos.Select(demo => demo.TestId ?? "").ToArray());

        Assert.Contains(demos, demo => demo.Steps.Any(step => step.FailureCode == "action_target_not_found"));
        Assert.Contains(demos, demo => demo.Steps.Any(step => step.GuardCode == "uia_capture_failed"));
        Assert.Contains(demos, demo => demo.Steps.Any(step => step.GuardCode == "uia_tree_empty"));
        Assert.Contains(demos, demo => demo.Steps.Any(step => step.GuardCode == "unexpected_modal_detected"));
    }

    [Fact]
    public void WriteAllCreatesPortableReportsAndSummaries()
    {
        var tempDir = CreateTempDirectory();

        var result = GuardFailureDemoFactory.WriteAll(tempDir);

        Assert.Equal(4, result.Artifacts.Count);
        foreach (var artifact in result.Artifacts)
        {
            var runDir = Path.Combine(tempDir, artifact.RunId);
            Assert.True(File.Exists(Path.Combine(runDir, "report.json")));
            Assert.True(File.Exists(Path.Combine(runDir, "summary.md")));
        }

        var missingTargetReport = Path.Combine(tempDir, "guard-demo-missing-target", "report.json");
        using var document = JsonDocument.Parse(File.ReadAllText(missingTargetReport));
        Assert.Equal("GUARD-MISSING-TARGET-001", document.RootElement.GetProperty("testId").GetString());
        Assert.Equal("action_target_not_found", document.RootElement.GetProperty("steps")[0].GetProperty("failureCode").GetString());

        var summary = File.ReadAllText(Path.Combine(tempDir, "guard-demo-empty-ui-tree", "summary.md"));
        Assert.Contains("uia_tree_empty", summary);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "desktop-ai-test-agent-guard-demo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
