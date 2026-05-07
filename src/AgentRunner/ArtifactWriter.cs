using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner;

/// <summary>
/// Writes run artifacts (JSON report + screenshots + markdown summary) to disk.
/// Inspired by Symphony's workspace + run artifact model.
/// </summary>
public class ArtifactWriter(string? baseDir = null)
{
    private readonly string _baseDir = baseDir ?? Path.Combine(Directory.GetCurrentDirectory(), "runs");

    /// <summary>
    /// Returns the directory path for a specific run.
    /// </summary>
    public string GetRunDir(string runId)
    {
        return Path.Combine(_baseDir, runId);
    }

    /// <summary>
    /// Saves a screenshot for a specific step.
    /// </summary>
    public string SaveScreenshot(string runId, int stepNumber, byte[] pngBytes)
    {
        var dir = Path.Combine(GetRunDir(runId), "screenshots");
        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, $"step_{stepNumber:D3}.png");
        File.WriteAllBytes(path, pngBytes);
        return path;
    }

    /// <summary>
    /// Saves the observed UI tree for a specific step.
    /// </summary>
    public string SaveUiTreeSnapshot(string runId, int stepNumber, UiSnapshot snapshot)
    {
        var dir = Path.Combine(GetRunDir(runId), "ui-tree");
        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, $"step_{stepNumber:D3}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(snapshot, CreateJsonOptions()));
        return path;
    }

    /// <summary>
    /// Writes the final JSON report for the run.
    /// </summary>
    public void WriteReport(RunArtifact artifact)
    {
        var dir = GetRunDir(artifact.RunId);
        Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(artifact, CreateJsonOptions());

        File.WriteAllText(Path.Combine(dir, "report.json"), json);
    }

    /// <summary>
    /// Writes a human-readable Markdown summary of the run.
    /// </summary>
    public void WriteSummary(RunArtifact artifact)
    {
        var dir = GetRunDir(artifact.RunId);
        Directory.CreateDirectory(dir);

        var sb = new StringBuilder();
        sb.AppendLine($"# Run {artifact.RunId}");
        sb.AppendLine();
        sb.AppendLine($"- **Goal**: {artifact.GoalDescription}");
        if (!string.IsNullOrEmpty(artifact.TestId))
            sb.AppendLine($"- **Test**: {artifact.TestId} - {artifact.TestTitle ?? artifact.GoalIdentifier}");
        if (!string.IsNullOrEmpty(artifact.Suite))
            sb.AppendLine($"- **Suite**: {artifact.Suite}");
        if (!string.IsNullOrEmpty(artifact.Framework))
            sb.AppendLine($"- **Framework**: {artifact.Framework}");
        sb.AppendLine($"- **Target**: {artifact.TargetWindow}");
        sb.AppendLine($"- **Evidence level**: {artifact.EvidenceLevel}");
        sb.AppendLine($"- **Result**: {artifact.Result}");
        sb.AppendLine($"- **Score**: {artifact.FinalScore}");
        sb.AppendLine($"- **Started**: {artifact.StartedAt:yyyy-MM-dd HH:mm:ss} UTC");
        if (artifact.EndedAt.HasValue)
        {
            var duration = artifact.EndedAt.Value - artifact.StartedAt;
            sb.AppendLine($"- **Ended**: {artifact.EndedAt:yyyy-MM-dd HH:mm:ss} UTC ({duration.TotalSeconds:F1}s)");
        }
        if (!string.IsNullOrEmpty(artifact.ErrorMessage))
            sb.AppendLine($"- **Error**: {artifact.ErrorMessage}");
        sb.AppendLine();

        sb.AppendLine("## Steps");
        sb.AppendLine();
        sb.AppendLine("| # | Action | Target | Outcome | Guard | Score | Evidence |");
        sb.AppendLine("|---|--------|--------|---------|-------|-------|----------|");

        foreach (var step in artifact.Steps)
        {
            var guard = !string.IsNullOrEmpty(step.GuardStatus)
                ? $"{step.GuardStatus}:{step.GuardCode}"
                : "-";
            var evidence = BuildEvidenceList(step);
            sb.AppendLine($"| {step.StepNumber} | {step.ActionType} | {step.ActionTarget ?? "-"} | {step.Outcome} | {guard} | {step.CumulativeScore} | {evidence} |");
        }

        File.WriteAllText(Path.Combine(dir, "summary.md"), sb.ToString());
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static string BuildEvidenceList(RunStep step)
    {
        var values = new System.Collections.Generic.List<string>();
        if (!string.IsNullOrWhiteSpace(step.ScreenshotPath))
            values.Add("screenshot");
        if (!string.IsNullOrWhiteSpace(step.UiTreePath))
            values.Add("ui-tree");

        return values.Count == 0 ? "-" : string.Join(", ", values);
    }
}
