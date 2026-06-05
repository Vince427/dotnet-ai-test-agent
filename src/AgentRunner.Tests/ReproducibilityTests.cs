using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DesktopAiTestAgent.AgentRunner;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner.Tests;

/// <summary>
/// Reproducibility gate (the desktop-agent analogue of open-cognitive-bench's run-twice-diff): the
/// key-free deterministic surfaces must produce IDENTICAL output across two runs on the same input.
/// ContractTests pin the *shape*; these pin run-to-run *stability* — so a future change that smuggles
/// in dict/set-ordering, DateTime.Now, file-glob order, or an unseeded source becomes a RED test.
/// The one legitimately volatile bit (the workbench "Generated &lt;timestamp&gt;" line) is normalized,
/// exactly like the report.md run-id in the bench. Stochastic LLM runs are deliberately not covered.
/// </summary>
public sealed class ReproducibilityTests
{
    private static string RepoRoot =>
        DesktopE2E.FindRepoRoot(AppContext.BaseDirectory)
            ?? throw new InvalidOperationException("Could not locate repo root.");

    [Fact]
    public void DiscoverPlanPaths_IsStableAcrossRuns()
    {
        // Glob/enumeration order is a classic hidden non-determinism source; discovery must be stable.
        var a = TestPlanLoader.DiscoverPlanPaths(RepoRoot);
        var b = TestPlanLoader.DiscoverPlanPaths(RepoRoot);
        Assert.Equal(a, b); // sequence equality — same paths, same order
    }

    [Fact]
    public void ComposeRecording_IsReproducible()
    {
        var session = new RecordedSession
        {
            Window = "Sample Login App (.NET 8)", Framework = "winforms", Title = "Login",
            Actions =
            [
                new RecordedAction { Verb = "EnterText", Target = "txtUsername", Name = "Username", Value = "admin" },
                new RecordedAction { Verb = "EnterText", Target = "txtPassword", Name = "Password", Value = "secret" },
                new RecordedAction { Verb = "Click", Target = "btnLogin", Name = "Log In" },
            ],
        };
        var a = RecordingComposer.Compose(session);
        var b = RecordingComposer.Compose(session);
        Assert.Equal(a.TestId, b.TestId);
        Assert.Equal(a.Yaml, b.Yaml); // byte-identical YAML draft
    }

    [Fact]
    public void RunAnalytics_IsReproducible()
    {
        var runs = SampleRuns();
        var a = JsonSerializer.Serialize(RunAnalytics.Compute(runs));
        var b = JsonSerializer.Serialize(RunAnalytics.Compute(runs));
        Assert.Equal(a, b); // identical analytics (flaky/drift/duration grouping is order-stable)
    }

    [Fact]
    public void PromptPreview_IsReproducible()
    {
        var test = new TestDefinition
        {
            Id = "REPRO-1", Framework = "winforms", Goal = "Log in and confirm.",
            SuccessCondition = "Login successful", MaxSteps = 8,
            AllowedActions = ["EnterText", "Click", "Done"],
        };
        Assert.Equal(PromptPreview.BuildForTest(test), PromptPreview.BuildForTest(test));
    }

    [Fact]
    public void Workbench_IsReproducible_ExceptTheGeneratedTimestamp()
    {
        var tests = new List<TestDefinition>
        {
            new() { Id = "WB-1", Title = "t", Framework = "winforms", Goal = "g",
                    SuccessCondition = "ok", MaxSteps = 8, AllowedActions = ["Click", "Done"] },
        };
        var runs = SampleRuns();
        string Render() => AgentLoopWorkbenchGenerator.RenderHtml(
            RepoRoot, System.IO.Path.Combine(RepoRoot, "docs", "symphony.html"), [], tests, runs);

        // Strip the single volatile line ("Generated <UTC> from ...") — the only run-to-run difference,
        // the direct analogue of the bench's report.md run-id timestamp.
        static string Normalize(string html) =>
            string.Join("\n", html.Split('\n').Where(l => !l.Contains("Generated ")));

        Assert.Equal(Normalize(Render()), Normalize(Render()));
    }

    private static IReadOnlyList<RunArtifact> SampleRuns() =>
    [
        new()
        {
            RunId = "r1", TestId = "LOGIN-001", Result = "Passed", FinalScore = 12,
            StartedAt = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc),
            EndedAt = new DateTime(2026, 6, 1, 10, 0, 20, DateTimeKind.Utc),
            Steps = [new RunStep { StepNumber = 1, ActionType = "Click", Outcome = "Succeeded" }],
        },
        new()
        {
            RunId = "r2", TestId = "LOGIN-001", Result = "Failed", FinalScore = 3,
            StartedAt = new DateTime(2026, 6, 2, 10, 0, 0, DateTimeKind.Utc),
            EndedAt = new DateTime(2026, 6, 2, 10, 0, 30, DateTimeKind.Utc),
            Steps =
            [
                new RunStep
                {
                    StepNumber = 1, ActionType = "Click", ActionTarget = "btnLogn", Outcome = "Failed",
                    HealingSuggestion = new HealingSuggestion { OldTarget = "btnLogn", NewTarget = "btnLogin", Confidence = 92 },
                },
            ],
        },
    ];
}
