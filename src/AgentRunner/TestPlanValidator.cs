using System;
using System.Collections.Generic;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner;

public sealed class TestPlanValidationResult
{
    public List<string> Errors { get; } = [];
    public bool IsValid => Errors.Count == 0;
}

public static class TestPlanValidator
{
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
