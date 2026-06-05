using System;
using System.Collections.Generic;
using System.Linq;

namespace DesktopAiTestAgent.AgentRunner;

/// <summary>
/// V11 analytics: derives deterministic insight from a run history (a list of
/// <see cref="RunArtifact"/>) — pass/fail per test, flaky detection, selector-drift grouping,
/// and duration/step stats. Pure + key-free (no disk, no LLM): the CLI loads <c>runs/</c> via
/// <see cref="RunArtifactLoader"/> and hands the in-memory list here, so the same computation is
/// trivially unit-testable. Null-safe: missing <c>EndedAt</c>, empty history, and partial runs
/// never throw — they're just excluded from the affected stat.
/// </summary>
public static class RunAnalytics
{
    /// <summary>A run result counts as "passing" when it is Passed or Succeeded (mirrors
    /// <c>--to-junit</c>'s pass semantics — see runner.md invariant).</summary>
    public static bool IsPassing(string? result) =>
        string.Equals(result, "Passed", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(result, "Succeeded", StringComparison.OrdinalIgnoreCase);

    public static RunAnalyticsResult Compute(IReadOnlyList<RunArtifact> runs)
    {
        var result = new RunAnalyticsResult();
        if (runs == null || runs.Count == 0)
            return result;

        result.TotalRuns = runs.Count;

        // --- Per-test pass/fail + flaky detection ---
        // Group by TestId (runs with no TestId fold into a single "(unknown)" bucket so they're
        // still counted, not silently dropped).
        var byTest = runs
            .GroupBy(r => string.IsNullOrWhiteSpace(r.TestId) ? "(unknown)" : r.TestId!)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var group in byTest)
        {
            var passed = 0;
            var failed = 0;
            foreach (var run in group)
            {
                if (IsPassing(run.Result)) passed++;
                else failed++;
            }

            // Flaky = the SAME test id produced BOTH a passing and a non-passing run.
            var flaky = passed > 0 && failed > 0;
            result.Tests.Add(new TestAnalytics
            {
                TestId = group.Key,
                Runs = passed + failed,
                Passed = passed,
                Failed = failed,
                Flaky = flaky
            });
        }

        result.FlakyTestCount = result.Tests.Count(t => t.Flaky);

        // Most-failing tests: most failures first, then lowest pass rate, then id for determinism.
        result.MostFailingTests = result.Tests
            .Where(t => t.Failed > 0)
            .OrderByDescending(t => t.Failed)
            .ThenBy(t => t.Runs == 0 ? 0d : (double)t.Passed / t.Runs)
            .ThenBy(t => t.TestId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // --- Selector drift: every step carrying a HealingSuggestion, grouped by old->new ---
        var driftGroups = new Dictionary<string, SelectorDriftGroup>(StringComparer.Ordinal);
        var driftTotal = 0;
        foreach (var run in runs)
        {
            if (run.Steps == null) continue;
            foreach (var step in run.Steps)
            {
                var heal = step?.HealingSuggestion;
                if (heal == null) continue;

                driftTotal++;
                var oldT = heal.OldTarget ?? "";
                var newT = heal.NewTarget ?? "";
                var key = oldT + "" + newT;
                if (!driftGroups.TryGetValue(key, out var grp))
                {
                    grp = new SelectorDriftGroup
                    {
                        OldTarget = oldT,
                        NewTarget = newT,
                        MaxConfidence = heal.Confidence
                    };
                    driftGroups[key] = grp;
                }
                grp.Count++;
                if (heal.Confidence > grp.MaxConfidence)
                    grp.MaxConfidence = heal.Confidence;
            }
        }

        result.SelectorDriftCount = driftTotal;
        result.SelectorDrift = driftGroups.Values
            .OrderByDescending(g => g.Count)
            .ThenBy(g => g.OldTarget, StringComparer.OrdinalIgnoreCase)
            .ThenBy(g => g.NewTarget, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // --- Duration stats (only runs with a usable EndedAt >= StartedAt) ---
        var durations = new List<double>();
        foreach (var run in runs)
        {
            if (run.EndedAt is { } ended && ended >= run.StartedAt)
                durations.Add((ended - run.StartedAt).TotalSeconds);
        }

        if (durations.Count > 0)
        {
            result.RunsWithDuration = durations.Count;
            result.AverageRunDurationSeconds = Math.Round(durations.Average(), 3);
            result.MaxRunDurationSeconds = Math.Round(durations.Max(), 3);
        }

        // --- Average step count across all runs ---
        var totalSteps = runs.Sum(r => r.Steps?.Count ?? 0);
        result.TotalSteps = totalSteps;
        result.AverageStepCount = Math.Round((double)totalSteps / runs.Count, 3);

        return result;
    }
}

/// <summary>Structured analytics output; serialized as-is by <c>--analytics --format json</c>.</summary>
public sealed class RunAnalyticsResult
{
    public string Kind { get; set; } = "runAnalytics";
    public int TotalRuns { get; set; }
    public int FlakyTestCount { get; set; }

    /// <summary>Steps carrying a healing suggestion across all runs (the selector-drift total).</summary>
    public int SelectorDriftCount { get; set; }

    /// <summary>How many runs had a usable duration (EndedAt present and &gt;= StartedAt).</summary>
    public int RunsWithDuration { get; set; }
    public double AverageRunDurationSeconds { get; set; }
    public double MaxRunDurationSeconds { get; set; }

    public int TotalSteps { get; set; }
    public double AverageStepCount { get; set; }

    public List<TestAnalytics> Tests { get; set; } = [];
    public List<TestAnalytics> MostFailingTests { get; set; } = [];
    public List<SelectorDriftGroup> SelectorDrift { get; set; } = [];
}

public sealed class TestAnalytics
{
    public string TestId { get; set; } = "";
    public int Runs { get; set; }
    public int Passed { get; set; }
    public int Failed { get; set; }
    public bool Flaky { get; set; }
}

public sealed class SelectorDriftGroup
{
    public string OldTarget { get; set; } = "";
    public string NewTarget { get; set; } = "";
    public int Count { get; set; }
    public int MaxConfidence { get; set; }
}
