using System;
using System.Collections.Generic;

namespace DesktopAiTestAgent.AgentRunner;

/// <summary>
/// Represents a complete agent run, capturing all steps and outcomes.
/// Captures the outcome of one AgentLoop run attempt.
/// </summary>
public class RunArtifact
{
    public string Version { get; set; } = "1.0";
    public string RunId { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public EvidenceLevel EvidenceLevel { get; set; } = EvidenceLevel.Standard;
    public string? GoalDescription { get; set; }
    public string? GoalIdentifier { get; set; }
    public string? TestId { get; set; }
    public string? TestTitle { get; set; }
    public string? TestPriority { get; set; }
    public string? Framework { get; set; }
    public string? Suite { get; set; }
    public string? TargetWindow { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
    public string Result { get; set; } = "Running"; // Running, Succeeded, Failed, Aborted, LoopDetected, Flaky
    public int FinalScore { get; set; }

    /// <summary>P3-B2: number of attempts this run took. 1 normally; 2 when <c>--retry-once</c> re-ran
    /// a failed run. A <c>Flaky</c> result always has Attempts=2 (failed then passed on retry).</summary>
    public int Attempts { get; set; } = 1;

    /// <summary>P3-B2: caveat set when <c>--retry-once</c> re-ran this run — the retry re-drives from
    /// the app's CURRENT state, so a non-idempotent action from the failed attempt may have replayed.
    /// Null when no retry happened.</summary>
    public string? RetryNote { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// W3C trace id of this run's root span when OpenTelemetry export is active
    /// (OBS-1). Null when telemetry is off. Links a recorded run to its live trace.
    /// </summary>
    public string? TraceId { get; set; }

    /// <summary>
    /// Links to existing automated tests this run complements (e.g. TRX/JUnit
    /// testcase ids), plus source issue/PR — surfaced as JUnit testcase properties
    /// so CI dashboards can cross-link (V4-A). Copied from the YAML test definition.
    /// </summary>
    public List<string> ExistingTests { get; set; } = [];
    public string? SourceIssue { get; set; }
    public string? SourcePr { get; set; }

    public List<RunStep> Steps { get; set; } = [];
}

/// <summary>
/// One step within an agent run.
/// </summary>
public class RunStep
{
    public int StepNumber { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? UiStateSnapshot { get; set; }
    public string? ActionType { get; set; }
    public string? ActionTarget { get; set; }
    public string? ActionValue { get; set; }
    public string? Reasoning { get; set; }
    public string? Outcome { get; set; } // Succeeded, Failed, LoopDetected
    public string? FailureCode { get; set; }
    public string? FailureMessage { get; set; }
    public string? GuardStatus { get; set; }
    public string? GuardCode { get; set; }
    public string? GuardMessage { get; set; }
    public int ScoreDelta { get; set; }
    public int CumulativeScore { get; set; }
    public string? ScreenshotPath { get; set; }
    public string? UiTreePath { get; set; }

    /// <summary>P3-B1: perceptual hash (dHash, 64-bit, 16-hex) of this step's screenshot —
    /// lets analytics detect visual regressions / state changes without keeping the image.</summary>
    public string? ScreenshotDHash { get; set; }

    /// <summary>P3-B1: Hamming distance between this step's dHash and the previous screenshotted
    /// step's (0 = visually identical frame, higher = more change). Null on the first screenshot.</summary>
    public int? ScreenshotDiffFromPrevious { get; set; }

    /// <summary>Annotated screenshot with numbered element boxes (V3 Tier-2, `full` evidence).</summary>
    public string? OverlayPath { get; set; }

    /// <summary>JSON index mapping each overlay box number to its element identifiers.</summary>
    public string? OverlayIndexPath { get; set; }

    /// <summary>V8: a proposed selector replacement when the target wasn't found. Evidence only —
    /// never auto-applied.</summary>
    public HealingSuggestion? HealingSuggestion { get; set; }
}

public enum EvidenceLevel
{
    Minimal,
    Standard,
    Full
}
