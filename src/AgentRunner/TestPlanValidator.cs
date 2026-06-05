using System;
using System.Collections.Generic;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner;

public sealed class TestPlanValidationResult
{
    public List<string> Errors { get; } = [];

    /// <summary>Non-fatal policy advisories (V7): the plan is still valid, but worth a look
    /// before running (e.g. unknown framework, no success condition, very high max_steps).</summary>
    public List<string> Warnings { get; } = [];

    public bool IsValid => Errors.Count == 0;
}

public static class TestPlanValidator
{
    /// <summary>First-class desktop targets; an unknown framework is a warning, not an error.</summary>
    private static readonly HashSet<string> KnownFrameworks =
        new(new[] { "winforms", "wpf", "maui", "avalonia" }, StringComparer.OrdinalIgnoreCase);

    /// <summary>Above this, a run is likely to be slow/expensive — advisory only.</summary>
    private const int HighMaxSteps = 100;


    /// <summary>
    /// Strips the "{sourceName}:{label}: " location prefix this validator prepends to every message,
    /// leaving just the human-readable advisory. Lives next to the producer (<see cref="Validate"/>)
    /// so the prefix shape has a single owner — consumers (dashboard, workbench, recorder) call this
    /// instead of each re-implementing the split. The path/id never contain ": " (colon+space), so the
    /// first ": " marks the start of the message.
    /// </summary>
    public static string StripLocationPrefix(string message)
    {
        if (string.IsNullOrEmpty(message)) return message;
        var i = message.IndexOf(": ", StringComparison.Ordinal);
        return i >= 0 ? message[(i + 2)..] : message;
    }

    public static TestPlanValidationResult Validate(TestPlan plan, string sourceName)
    {
        if (plan == null) throw new ArgumentNullException(nameof(plan));

        var result = new TestPlanValidationResult();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (plan.Tests.Count == 0)
            result.Errors.Add($"{sourceName}: plan must define at least one test.");

        foreach (var test in plan.Tests)
        {
            var label = string.IsNullOrWhiteSpace(test.Id) ? "(missing id)" : test.Id;

            if (string.IsNullOrWhiteSpace(test.Id))
                result.Errors.Add($"{sourceName}:{label}: id is required.");
            else if (!seenIds.Add(test.Id))
                result.Errors.Add($"{sourceName}:{label}: duplicate test id.");

            if (string.IsNullOrWhiteSpace(test.Goal))
                result.Errors.Add($"{sourceName}:{label}: goal is required.");

            if (test.MaxSteps <= 0)
                result.Errors.Add($"{sourceName}:{label}: max_steps must be a positive integer.");
            if (!string.IsNullOrWhiteSpace(test.Risk) && !IsKnownRisk(test.Risk!))
                result.Errors.Add($"{sourceName}:{label}: risk must be one of low, medium, high, critical.");

            ValidateList(result, sourceName, label, "allowed_actions", test.AllowedActions);
            ValidateList(result, sourceName, label, "tags", test.Tags);
            ValidateList(result, sourceName, label, "blocked_if", test.BlockedIf);
            ValidateList(result, sourceName, label, "existing_tests", test.ExistingTests);

            foreach (var action in test.AllowedActions)
            {
                if (!ActionVocabulary.IsKnown(action))
                    result.Errors.Add($"{sourceName}:{label}: unsupported allowed action '{action}'.");
            }

            // --- Policy advisories (warnings, non-fatal) — V7 ---
            if (!string.IsNullOrWhiteSpace(test.Framework) && !KnownFrameworks.Contains(test.Framework!))
                result.Warnings.Add($"{sourceName}:{label}: framework '{test.Framework}' is not a first-class target (winforms, wpf, maui, avalonia).");
            if (test.MaxSteps > HighMaxSteps)
                result.Warnings.Add($"{sourceName}:{label}: max_steps {test.MaxSteps} is high (> {HighMaxSteps}); runs may be slow/costly.");
            if (string.IsNullOrWhiteSpace(test.SuccessCondition))
                result.Warnings.Add($"{sourceName}:{label}: no success_condition; success will rely on an explicit Assert or the agent's Done.");
        }

        return result;
    }

    private static void ValidateList(
        TestPlanValidationResult result,
        string sourceName,
        string testId,
        string fieldName,
        IReadOnlyList<string> values)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                result.Errors.Add($"{sourceName}:{testId}: {fieldName} contains an empty value.");
                continue;
            }

            if (!seen.Add(value))
                result.Errors.Add($"{sourceName}:{testId}: {fieldName} contains duplicate value '{value}'.");
        }
    }

    private static bool IsKnownRisk(string risk)
    {
        return string.Equals(risk, "low", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(risk, "medium", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(risk, "high", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(risk, "critical", StringComparison.OrdinalIgnoreCase);
    }
}
