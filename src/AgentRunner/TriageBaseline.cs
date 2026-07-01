using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DesktopAiTestAgent.AgentRunner;

/// <summary>
/// P3-B3: a curated, versioned triage catalog (<c>baseline.json</c>) that separates *known*
/// failing tests from *new* regressions. Three buckets: <see cref="KnownFlakes"/> (transient
/// UIA/timing flakes, with an optional estimated rate), <see cref="DataDriftScenarios"/>
/// (broken by test-data, not a code bug) and <see cref="PreservedBugs"/> (accepted/tracked
/// bugs). <see cref="RunAnalytics"/> consumes it so <c>--analytics</c> can answer "any NEW
/// regressions?" instead of drowning known failures in the noise. Pure + key-free; hand-editable.
/// </summary>
public sealed class TriageBaseline
{
    public string SchemaVersion { get; set; } = "1.0";
    public List<BaselineEntry> KnownFlakes { get; set; } = [];
    public List<BaselineEntry> DataDriftScenarios { get; set; } = [];
    public List<BaselineEntry> PreservedBugs { get; set; } = [];

    /// <summary>
    /// Classifies a failing test id against the catalog:
    /// <c>knownFlake</c> / <c>dataDrift</c> / <c>preservedBug</c> / <c>newRegression</c>
    /// (the last = failing but listed nowhere → the signal that matters).
    /// </summary>
    public string Classify(string testId)
    {
        if (Contains(KnownFlakes, testId)) return "knownFlake";
        if (Contains(DataDriftScenarios, testId)) return "dataDrift";
        if (Contains(PreservedBugs, testId)) return "preservedBug";
        return "newRegression";
    }

    private static bool Contains(List<BaselineEntry> list, string testId) =>
        list != null && list.Any(e => string.Equals(e?.TestId, testId, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Loads a baseline from JSON. Returns null when the path is empty or the file is absent
    /// (baseline is optional). Tolerant: case-insensitive, comments and trailing commas allowed,
    /// unknown fields ignored — so a hand-edited catalog with notes never breaks analytics.
    /// </summary>
    public static TriageBaseline? Load(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };
        return JsonSerializer.Deserialize<TriageBaseline>(File.ReadAllText(path), options) ?? new TriageBaseline();
    }
}

/// <summary>One catalog entry. <see cref="TestId"/> is matched; <see cref="Reason"/> and
/// <see cref="Rate"/> are human documentation (rate = estimated flake frequency 0–1).</summary>
public sealed class BaselineEntry
{
    public string TestId { get; set; } = "";
    public string? Reason { get; set; }
    public double? Rate { get; set; }
}
