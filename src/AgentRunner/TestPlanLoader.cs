using System;
using System.Collections.Generic;
using System.IO;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner;

public static class TestPlanLoader
{
    public static List<string> DiscoverPlanPaths(string repoRoot)
    {
        if (string.IsNullOrWhiteSpace(repoRoot))
            throw new ArgumentException("A repository root is required.", nameof(repoRoot));

        var testsDir = Path.Combine(repoRoot, "tests");
        if (!Directory.Exists(testsDir))
            return [];

        // Archived tests live under tests/archived/ and are excluded everywhere (catalog, CLI
        // --list-tests/--suite, CI) — archiving from the dashboard simply moves a YAML there.
        var archivedPrefix = Path.Combine(testsDir, "archived") + Path.DirectorySeparatorChar;

        return Directory.EnumerateFiles(testsDir, "*.yaml", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(testsDir, "*.yml", SearchOption.AllDirectories))
            .Where(path => !path.StartsWith(archivedPrefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static TestPlan Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("A test plan path is required.", nameof(path));
        if (!File.Exists(path))
            throw new FileNotFoundException("Test plan file not found.", path);

        return Parse(File.ReadAllText(path), path);
    }

    public static TestPlan Parse(string yaml, string? sourceName = null)
    {
        var plan = new TestPlan();
        TestDefinition? currentTest = null;
        string? currentListKey = null;

        foreach (var rawLine in yaml.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#", StringComparison.Ordinal))
                continue;

            var indent = line.Length - line.TrimStart().Length;
            var trimmed = line.Trim();

            if (trimmed == "tests:")
            {
                currentTest = null;
                currentListKey = null;
                continue;
            }

            if (indent == 0)
            {
                var (key, value) = SplitKeyValue(trimmed);
                if (key == "suite")
                    plan.Suite = Unquote(value);
                currentListKey = null;
                continue;
            }

            if (indent == 2 && trimmed.EndsWith(":", StringComparison.Ordinal))
            {
                currentTest = new TestDefinition { Id = trimmed.TrimEnd(':').Trim() };
                plan.Tests.Add(currentTest);
                currentListKey = null;
                continue;
            }

            if (currentTest == null || indent < 4)
                continue;

            if (trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                AddListValue(currentTest, currentListKey, Unquote(trimmed[2..].Trim()));
                continue;
            }

            var (testKey, testValue) = SplitKeyValue(trimmed);
            currentListKey = null;

            if (string.IsNullOrEmpty(testValue))
            {
                currentListKey = testKey;
                continue;
            }

            ApplyScalar(currentTest, testKey, testValue);
        }

        if (plan.Tests.Count == 0)
            throw new InvalidOperationException($"Test plan '{sourceName ?? "(inline)"}' does not define any tests.");

        foreach (var test in plan.Tests)
        {
            if (string.IsNullOrWhiteSpace(test.Id))
                throw new InvalidOperationException("A test definition is missing an id.");
            if (string.IsNullOrWhiteSpace(test.Goal))
                throw new InvalidOperationException($"Test '{test.Id}' must define a goal.");
        }

        return plan;
    }

    private static void ApplyScalar(TestDefinition test, string key, string value)
    {
        var cleanValue = Unquote(value);
        switch (key)
        {
            case "title":
                test.Title = cleanValue;
                break;
            case "priority":
                test.Priority = cleanValue;
                break;
            case "framework":
                test.Framework = cleanValue;
                break;
            case "target_window":
                test.TargetWindow = cleanValue;
                break;
            case "source_issue":
                test.SourceIssue = string.IsNullOrWhiteSpace(cleanValue) ? null : cleanValue;
                break;
            case "source_pr":
                test.SourcePr = string.IsNullOrWhiteSpace(cleanValue) ? null : cleanValue;
                break;
            case "authoring_agent":
                test.AuthoringAgent = string.IsNullOrWhiteSpace(cleanValue) ? null : cleanValue;
                break;
            case "risk":
                test.Risk = string.IsNullOrWhiteSpace(cleanValue) ? null : cleanValue;
                break;
            case "ci_profile":
                test.CiProfile = string.IsNullOrWhiteSpace(cleanValue) ? null : cleanValue;
                break;
            case "goal":
                test.Goal = cleanValue;
                break;
            case "success_condition":
                test.SuccessCondition = string.IsNullOrWhiteSpace(cleanValue) ? null : cleanValue;
                break;
            case "max_steps":
                if (!int.TryParse(cleanValue, out var maxSteps) || maxSteps <= 0)
                    throw new InvalidOperationException($"Test '{test.Id}' max_steps must be a positive integer.");
                test.MaxSteps = maxSteps;
                break;
            case "category":
                if (Enum.TryParse<TestCategory>(cleanValue, true, out var category))
                    test.Category = category;
                break;
            case "allowed_actions":
                test.AllowedActions = ParseInlineList(cleanValue);
                break;
            case "tags":
                test.Tags = ParseInlineList(cleanValue);
                break;
            case "blocked_if":
                test.BlockedIf = ParseInlineList(cleanValue);
                break;
            case "existing_tests":
                test.ExistingTests = ParseInlineList(cleanValue);
                break;
            case "selectors":
                test.Selectors = ParseInlineList(cleanValue);
                break;
        }
    }

    private static void AddListValue(TestDefinition test, string? key, string value)
    {
        switch (key)
        {
            case "allowed_actions":
                test.AllowedActions.Add(value);
                break;
            case "tags":
                test.Tags.Add(value);
                break;
            case "blocked_if":
                test.BlockedIf.Add(value);
                break;
            case "existing_tests":
                test.ExistingTests.Add(value);
                break;
            case "selectors":
                test.Selectors.Add(value);
                break;
        }
    }

    private static (string Key, string Value) SplitKeyValue(string line)
    {
        var colonIndex = line.IndexOf(':');
        if (colonIndex < 0)
            return (line.Trim(), "");

        return (line[..colonIndex].Trim(), line[(colonIndex + 1)..].Trim());
    }

    private static List<string> ParseInlineList(string value)
    {
        var result = new List<string>();
        var trimmed = value.Trim();
        if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal))
            trimmed = trimmed[1..^1];

        if (string.IsNullOrWhiteSpace(trimmed))
            return result;

        foreach (var item in trimmed.Split(','))
        {
            var clean = Unquote(item.Trim());
            if (!string.IsNullOrWhiteSpace(clean))
                result.Add(clean);
        }

        return result;
    }

    private static string Unquote(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length >= 2 &&
            ((trimmed[0] == '"' && trimmed[^1] == '"') ||
             (trimmed[0] == '\'' && trimmed[^1] == '\'')))
        {
            return trimmed[1..^1];
        }

        return trimmed;
    }
}
