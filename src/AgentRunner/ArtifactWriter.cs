using System;
using System.IO;
using System.Text;
using System.Text.Json;

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
    /// Writes the final JSON report for the run.
    /// </summary>
    public void WriteReport(RunArtifact artifact)
    {
        var dir = GetRunDir(artifact.RunId);
        Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(artifact, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

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
        sb.AppendLine($"- **Target**: {artifact.TargetWindow}");
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
        sb.AppendLine("| # | Action | Target | Outcome | Score |");
        sb.AppendLine("|---|--------|--------|---------|-------|");

        foreach (var step in artifact.Steps)
        {
            sb.AppendLine($"| {step.StepNumber} | {step.ActionType} | {step.ActionTarget ?? "-"} | {step.Outcome} | {step.CumulativeScore} |");
        }

        File.WriteAllText(Path.Combine(dir, "summary.md"), sb.ToString());
    }
}
