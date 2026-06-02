using System;
using System.Collections.Generic;

namespace DesktopAiTestAgent.AgentRunner;

/// <summary>
/// Represents a complete agent run, capturing all steps and outcomes.
/// Captures the outcome of one AgentLoop run attempt.
/// </summary>
public class RunArtifact
{
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
    public string Result { get; set; } = "Running"; // Running, Succeeded, Failed, Aborted, LoopDetected
    public int FinalScore { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// W3C trace id of this run's root span when OpenTelemetry export is active
    /// (OBS-1). Null when telemetry is off. Links a recorded run to its live trace.
    /// </summary>
    public string? TraceId { get; set; }

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
}

public enum EvidenceLevel
{
    Minimal,
    Standard,
    Full
}
