using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner;

/// <summary>
/// Writes run artifacts (JSON report + screenshots + markdown summary) to disk.
/// Writes portable workspace and run artifacts for AgentLoop runs.
/// </summary>
public class ArtifactWriter(string? baseDir = null, SecretRedactor? redactor = null)
{
    private readonly string _baseDir = baseDir ?? Path.Combine(Directory.GetCurrentDirectory(), "runs");
    private readonly SecretRedactor _redactor = redactor ?? new SecretRedactor();

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
        AtomicWriteAllBytes(path, pngBytes);
        return path;
    }

    /// <summary>
    /// Saves the annotated (numbered-box) overlay screenshot for a step (V3 Tier-2).
    /// </summary>
    public string SaveOverlay(string runId, int stepNumber, byte[] pngBytes)
    {
        var dir = Path.Combine(GetRunDir(runId), "overlay");
        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, $"step_{stepNumber:D3}.png");
        AtomicWriteAllBytes(path, pngBytes);
        return path;
    }

    /// <summary>
    /// Saves the overlay index (box number → element identifiers) for a step (V3 Tier-2).
    /// The index carries identifiers only, never element values, so it is secret-safe.
    /// </summary>
    internal string SaveOverlayIndex(string runId, int stepNumber, System.Collections.Generic.IReadOnlyList<OverlayBox> index)
    {
        var dir = Path.Combine(GetRunDir(runId), "overlay");
        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, $"step_{stepNumber:D3}.json");
        AtomicWriteAllText(path, JsonSerializer.Serialize(index, CreateJsonOptions()));
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
        AtomicWriteAllText(path, JsonSerializer.Serialize(_redactor.RedactSnapshot(snapshot), CreateJsonOptions()));
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

        AtomicWriteAllText(Path.Combine(dir, "report.json"), json);
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
        sb.AppendLine("| # | Action | Target | Outcome | Failure | Guard | Score | Evidence |");
        sb.AppendLine("|---|--------|--------|---------|---------|-------|-------|----------|");

        foreach (var step in artifact.Steps)
        {
            var failure = !string.IsNullOrEmpty(step.FailureCode)
                ? $"{step.FailureCode}: {step.FailureMessage ?? "-"}"
                : "-";
            var guard = !string.IsNullOrEmpty(step.GuardStatus)
                ? $"{step.GuardStatus}:{step.GuardCode}"
                : "-";
            var evidence = BuildEvidenceList(step);
            sb.AppendLine($"| {step.StepNumber} | {step.ActionType} | {step.ActionTarget ?? "-"} | {step.Outcome} | {failure} | {guard} | {step.CumulativeScore} | {evidence} |");
        }

        AppendHealingSection(sb, artifact);

        AtomicWriteAllText(Path.Combine(dir, "summary.md"), sb.ToString());
    }

    /// <summary>
    /// Writes text to <paramref name="path"/> atomically: serialize to a sibling
    /// <c>.tmp</c> file first, then swap it into place via <see cref="File.Replace"/>
    /// (falling back to delete+move if Replace is transiently blocked, e.g. an AV
    /// scanner holding the target). A concurrent reader (the <c>--dashboard</c> live
    /// view, <c>--analytics</c>) therefore never observes a half-written file.
    /// </summary>
    private static void AtomicWriteAllText(string path, string contents)
        => WriteThenSwap(path, tmp => File.WriteAllText(tmp, contents));

    /// <summary>
    /// Byte-array counterpart of <see cref="AtomicWriteAllText"/> (screenshots, overlays).
    /// </summary>
    private static void AtomicWriteAllBytes(string path, byte[] bytes)
        => WriteThenSwap(path, tmp => File.WriteAllBytes(tmp, bytes));

    /// <summary>
    /// Writes to a UNIQUE sibling temp file (GUID-suffixed, so two concurrent writers to the
    /// same target never clobber each other's temp), then swaps it into place. On ANY failure
    /// the temp is cleaned up, so a crash mid-write leaves no orphaned <c>.tmp</c> behind.
    /// </summary>
    private static void WriteThenSwap(string path, Action<string> write)
    {
        var tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            write(tmp);
            SwapIntoPlace(tmp, path);
        }
        catch
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* best-effort cleanup */ }
            throw;
        }
    }

    private static void SwapIntoPlace(string tmp, string path)
    {
        try
        {
            if (File.Exists(path))
                File.Replace(tmp, path, null);
            else
                File.Move(tmp, path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // File.Replace is atomic on NTFS but can fail under a transient lock (AV/indexer).
#if NET8_0_OR_GREATER
            // net8: overwrite-move is a single operation — no window where the target is absent.
            File.Move(tmp, path, overwrite: true);
#else
            // net48 has no overwrite-move: a brief window exists where the target is absent.
            // Acceptable on this already-degraded fallback (the primary File.Replace is atomic).
            if (File.Exists(path))
                File.Delete(path);
            File.Move(tmp, path);
#endif
        }
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

    /// <summary>
    /// V8 inc.2 — selector-healing evidence. Lists each drift suggestion alongside the step's
    /// screenshot so a human can see the live UI before deciding whether to adopt the new selector.
    /// Evidence only: the runner never applies a suggestion (CI stays deterministic).
    /// </summary>
    private static void AppendHealingSection(StringBuilder sb, RunArtifact artifact)
    {
        var hasHeal = false;
        foreach (var step in artifact.Steps)
            if (step.HealingSuggestion != null) { hasHeal = true; break; }
        if (!hasHeal)
            return;

        sb.AppendLine();
        sb.AppendLine("## Selector Healing Suggestions");
        sb.AppendLine();
        sb.AppendLine("Closest-match proposals for targets that drifted (named but not present in the live UI). " +
                      "Evidence only — the runner never applies them. Review the screenshot, then adopt by hand.");
        sb.AppendLine();
        foreach (var step in artifact.Steps)
        {
            var h = step.HealingSuggestion;
            if (h == null)
                continue;
            sb.AppendLine($"- **Step {step.StepNumber}**: `{h.OldTarget}` → `{h.NewTarget}` ({h.Confidence}% match, {h.ControlType})");
            sb.AppendLine($"  - {h.Rationale}");
            if (!string.IsNullOrWhiteSpace(step.ScreenshotPath))
            {
                var file = Path.GetFileName(step.ScreenshotPath);
                sb.AppendLine($"  - Screenshot: [{file}](screenshots/{file})");
            }
        }
    }

    private static string BuildEvidenceList(RunStep step)
    {
        var values = new System.Collections.Generic.List<string>();
        if (!string.IsNullOrWhiteSpace(step.ScreenshotPath))
            values.Add("screenshot");
        if (!string.IsNullOrWhiteSpace(step.UiTreePath))
            values.Add("ui-tree");
        if (!string.IsNullOrWhiteSpace(step.OverlayPath))
            values.Add("overlay");
        if (step.HealingSuggestion != null)
            values.Add($"heal→{step.HealingSuggestion.NewTarget}");

        return values.Count == 0 ? "-" : string.Join(", ", values);
    }
}
