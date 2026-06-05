using System;
using System.Collections.Generic;
using System.Linq;

namespace DesktopAiTestAgent.AgentRunner;

/// <summary>Kind of change a rewrite made to one declared fact of a test.</summary>
public enum FactChangeKind
{
    /// <summary>The fact had a value before and is empty/absent after.</summary>
    Dropped,
    /// <summary>The fact changed from one non-empty value to a different one.</summary>
    Changed,
    /// <summary>The fact was empty/absent before and has a value after.</summary>
    Added,
}

/// <summary>One fact that differs between the original and rewritten test.</summary>
public sealed class FactChange
{
    public string Field { get; set; } = "";
    public string? Before { get; set; }
    public string? After { get; set; }
    public FactChangeKind Kind { get; set; }

    public override string ToString() => Kind switch
    {
        FactChangeKind.Dropped => $"{Field}: dropped (was '{Before}')",
        FactChangeKind.Added => $"{Field}: added ('{After}')",
        _ => $"{Field}: changed '{Before}' -> '{After}'",
    };
}

/// <summary>Outcome of a fact-gate check: the violations a rewrite introduced.</summary>
public sealed class FactGuardResult
{
    /// <summary>Changes that were NOT in the allowed-to-change set — i.e. facts silently altered.</summary>
    public List<FactChange> Violations { get; } = [];
    public bool Ok => Violations.Count == 0;
}

/// <summary>
/// A fact-gate for test rewrites (the desktop-agent analogue of drift-guard's fact-preservation
/// check). When a tool re-writes a test's YAML — a future <c>--heal-apply</c> selector rewrite,
/// recording compose, or an MCP/dashboard edit — it must change ONLY what it intends and preserve
/// every other declared fact. <see cref="Verify"/> compares the before/after <see cref="TestDefinition"/>
/// and reports any field that was dropped or silently changed outside the caller's allow-list, so a
/// rewrite can be refused before it touches disk. Pure, deterministic, key-free — no LLM.
/// </summary>
public static class TestFactGuard
{
    // The declared facts of a test, each projected to a stable string for comparison. Lists are
    // order-insensitive (sorted) so a reordering isn't flagged as a change; the *set* of facts is
    // what must be preserved.
    private static readonly (string Field, Func<TestDefinition, string?> Get)[] Facts =
    [
        ("id", t => t.Id),
        ("title", t => t.Title),
        ("priority", t => t.Priority),
        ("framework", t => t.Framework),
        ("target_window", t => t.TargetWindow),
        ("risk", t => t.Risk),
        ("ci_profile", t => t.CiProfile),
        ("authoring_agent", t => t.AuthoringAgent),
        ("source_issue", t => t.SourceIssue),
        ("source_pr", t => t.SourcePr),
        ("category", t => t.Category.ToString()),
        ("goal", t => t.Goal),
        ("success_condition", t => t.SuccessCondition),
        ("max_steps", t => t.MaxSteps.ToString()),
        ("allowed_actions", t => JoinList(t.AllowedActions)),
        ("tags", t => JoinList(t.Tags)),
        ("blocked_if", t => JoinList(t.BlockedIf)),
        ("existing_tests", t => JoinList(t.ExistingTests)),
        ("selectors", t => JoinList(t.Selectors)),
    ];

    /// <summary>Every fact that differs between <paramref name="before"/> and <paramref name="after"/>.</summary>
    public static List<FactChange> Diff(TestDefinition before, TestDefinition after)
    {
        if (before == null) throw new ArgumentNullException(nameof(before));
        if (after == null) throw new ArgumentNullException(nameof(after));

        var changes = new List<FactChange>();
        foreach (var (field, get) in Facts)
        {
            var b = Norm(get(before));
            var a = Norm(get(after));
            if (b == a)
                continue;

            var kind = b.Length == 0 ? FactChangeKind.Added
                     : a.Length == 0 ? FactChangeKind.Dropped
                     : FactChangeKind.Changed;
            changes.Add(new FactChange { Field = field, Before = get(before), After = get(after), Kind = kind });
        }
        return changes;
    }

    /// <summary>
    /// A rewrite is safe when every change is to a field the caller explicitly allows to change.
    /// Field names match the YAML keys (e.g. <c>success_condition</c>, <c>allowed_actions</c>),
    /// case-insensitive. Pass the fields a tool intends to touch; everything else must be preserved.
    /// </summary>
    public static FactGuardResult Verify(
        TestDefinition before, TestDefinition after, IEnumerable<string>? allowedToChange = null)
    {
        var allowed = new HashSet<string>(allowedToChange ?? [], StringComparer.OrdinalIgnoreCase);
        var result = new FactGuardResult();
        foreach (var change in Diff(before, after))
            if (!allowed.Contains(change.Field))
                result.Violations.Add(change);
        return result;
    }

    private static string Norm(string? s) => (s ?? "").Trim();

    private static string JoinList(IReadOnlyList<string>? values) =>
        values == null || values.Count == 0
            ? ""
            : string.Join(",", values.Select(v => (v ?? "").Trim()).OrderBy(v => v, StringComparer.Ordinal));
}
