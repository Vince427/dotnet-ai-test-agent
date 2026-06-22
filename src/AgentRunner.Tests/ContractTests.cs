using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using DesktopAiTestAgent.AgentRunner;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner.Tests;

/// <summary>
/// Golden / contract tests that lock the public surface documented in CONTRACT.md so a future
/// change (including AI-assisted edits) that silently breaks users becomes a RED test, not a
/// user ticket. These assert STRUCTURE (key sets, exit codes, schema/loader agreement), never
/// volatile values. If one of these fails, either the change is a contract break (revert / bump
/// to 2.0 per the SemVer policy) or CONTRACT.md must be updated in the same change.
/// </summary>
public sealed class ContractTests
{
    private static readonly JsonSerializerOptions ManualJson = BuildManualJson();

    private static JsonSerializerOptions BuildManualJson()
    {
        // Mirror Program.WriteJson: camelCase + string enums. The CLI emits this exact shape on
        // stdout for `--format json`, so the contract is the key set of this serialization.
        var o = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        o.Converters.Add(new JsonStringEnumConverter());
        return o;
    }

    private static string RepoRoot =>
        DesktopE2E.FindRepoRoot(System.AppContext.BaseDirectory)
            ?? throw new System.InvalidOperationException("Could not locate repo root (DesktopAiTestAgent.sln).");

    // --- 1) Every shipped tests/**/*.yaml (excluding tests/archived/) loads AND validates clean ---

    public static IEnumerable<object[]> ShippedPlanPaths()
    {
        foreach (var path in TestPlanLoader.DiscoverPlanPaths(RepoRoot))
            yield return new object[] { path };
    }

    [Theory]
    [MemberData(nameof(ShippedPlanPaths))]
    public void EveryShippedPlanLoadsAndValidatesWithZeroErrors(string planPath)
    {
        // Catches schema/loader drift that breaks a shipped example (a real user-visible contract).
        var plan = TestPlanLoader.Load(planPath);
        var result = TestPlanValidator.Validate(plan, planPath);

        Assert.True(
            result.IsValid,
            $"Shipped plan failed validation: {planPath}\n  " + string.Join("\n  ", result.Errors));
    }

    [Fact]
    public void RepoShipsAtLeastOnePlan()
    {
        // Guards the discovery contract itself (so the theory above can't pass vacuously).
        Assert.NotEmpty(TestPlanLoader.DiscoverPlanPaths(RepoRoot));
    }

    [Fact]
    public void DiscoveryExcludesArchivedPlans()
    {
        var archivedPrefix = Path.Combine(RepoRoot, "tests", "archived") + Path.DirectorySeparatorChar;
        Assert.DoesNotContain(
            TestPlanLoader.DiscoverPlanPaths(RepoRoot),
            p => p.StartsWith(archivedPrefix, System.StringComparison.OrdinalIgnoreCase));
    }

    // --- 2) Snapshot the SHAPE (top-level key set) of the manual --format json payloads ---

    [Fact]
    public void ListTestsJsonHasTheContractTopLevelKeys()
    {
        var output = new TestListOutput
        {
            Valid = true,
            Count = 1,
            Tests =
            {
                new ListedTestOutput { PlanPath = "tests/smoke.yaml", Id = "X-1", Goal = "do it" }
            }
        };

        var keys = TopLevelKeys(output);
        Assert.Equal(
            new[] { "kind", "valid", "count", "tests", "errors" }.OrderBy(k => k),
            keys.OrderBy(k => k));

        // The per-test object shape is part of the contract too (renaming a field breaks consumers).
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(output, ManualJson));
        var test = doc.RootElement.GetProperty("tests")[0];
        var testKeys = test.EnumerateObject().Select(p => p.Name).OrderBy(k => k).ToArray();
        Assert.Equal(
            new[]
            {
                "planPath", "suite", "id", "title", "priority", "framework", "targetWindow",
                "sourceIssue", "sourcePr", "authoringAgent", "risk", "ciProfile", "goal",
                "successCondition", "maxSteps", "allowedActions", "tags", "existingTests"
            }.OrderBy(k => k),
            testKeys);

        // The "kind" discriminator value is a stable contract literal.
        Assert.Equal("testList", doc.RootElement.GetProperty("kind").GetString());
    }

    [Fact]
    public void ValidatePlanJsonHasTheContractTopLevelKeys()
    {
        var output = new PlanValidationOutput
        {
            Valid = true,
            PlanCount = 1,
            TestCount = 1,
            Plans =
            {
                new PlanValidationPlanOutput { Path = "tests/smoke.yaml", TestCount = 1, Valid = true }
            }
        };

        var keys = TopLevelKeys(output);
        Assert.Equal(
            new[]
            {
                "kind", "valid", "planCount", "testCount", "errorCount", "warningCount",
                "plans", "errors", "warnings"
            }.OrderBy(k => k),
            keys.OrderBy(k => k));

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(output, ManualJson));
        var plan = doc.RootElement.GetProperty("plans")[0];
        var planKeys = plan.EnumerateObject().Select(p => p.Name).OrderBy(k => k).ToArray();
        Assert.Equal(
            new[] { "path", "suite", "testCount", "valid", "errors", "warnings" }.OrderBy(k => k),
            planKeys);

        Assert.Equal("planValidation", doc.RootElement.GetProperty("kind").GetString());
    }

    private static string[] TopLevelKeys<T>(T value)
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(value, ManualJson));
        return doc.RootElement.EnumerateObject().Select(p => p.Name).ToArray();
    }

    // --- 3) Documented exit-code contract (headless cases: invalid args -> 2; good plan -> 0) ---

    [Fact]
    public void InvalidArgsAreRejectedByParse()
    {
        // Program maps a RunnerOptions.Parse ArgumentException to exit code 2. Asserting the throw
        // (rather than spawning the process) keeps this headless while covering the same branch.
        Assert.Throws<System.ArgumentException>(() =>
            RunnerOptions.Parse(["--not-a-flag"], new WorkflowConfig()));
    }

    [Fact]
    public void JsonFormatOnRuntimeCommandIsRejected()
    {
        Assert.Throws<System.ArgumentException>(() =>
            RunnerOptions.Parse(["--format", "json"], new WorkflowConfig()));
    }

    [Fact]
    public void KnownGoodPlanValidatesAsExitZeroShape()
    {
        // The validate-of-a-known-good-plan -> 0 contract: a valid plan yields IsValid == true,
        // which Program turns into exit code 0. We exercise the same validator the CLI uses.
        var plan = TestPlanLoader.Parse("""
suite: smoke

tests:
  CONTRACT-OK-001:
    goal: "Open the app and confirm the main window."
    framework: "winforms"
    success_condition: "Ready"
    max_steps: 8
    allowed_actions: ["Click", "Assert", "Done"]
""");

        var result = TestPlanValidator.Validate(plan, "tests/smoke.yaml");

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    // --- 4) Schema <-> loader/validator agreement (structural, best-effort) ---
    //
    // The JSON schema and the C# loader/validator are two independent encodings of the same YAML
    // contract; they can silently drift. These assert the load-bearing numbers/enums match.

    [Fact]
    public void SchemaMaxStepsBoundsMatchValidatorAndLoader()
    {
        var schema = LoadSchema();
        var maxSteps = schema
            .GetProperty("$defs").GetProperty("testDefinition")
            .GetProperty("properties").GetProperty("max_steps");

        // Loader rejects max_steps <= 0; schema must declare the same lower bound.
        Assert.Equal(1, maxSteps.GetProperty("minimum").GetInt32());

        // Validator's HighMaxSteps advisory threshold (100) is the schema's hard maximum: a value
        // the schema accepts as the ceiling is exactly where the runner starts warning. If either
        // side moves without the other, this fails.
        var schemaMax = maxSteps.GetProperty("maximum").GetInt32();
        Assert.Equal(100, schemaMax);

        var plan = TestPlanLoader.Parse($"""
tests:
  T-1:
    goal: "g"
    max_steps: {schemaMax}
    success_condition: "ok"
""");
        Assert.Equal(schemaMax, plan.Tests[0].MaxSteps);
        // At the ceiling the validator is still error-free (warning, not error).
        Assert.True(TestPlanValidator.Validate(plan, "p").IsValid);
    }

    [Fact]
    public void SchemaRequiredFieldsMatchLoaderEnforcement()
    {
        var schema = LoadSchema();

        // Plan-level: "tests" is required.
        var planRequired = schema.GetProperty("required").EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.Contains("tests", planRequired);

        // Test-level: only "goal" is required — and the loader/validator enforce exactly that.
        var testRequired = schema
            .GetProperty("$defs").GetProperty("testDefinition")
            .GetProperty("required").EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.Equal(new[] { "goal" }, testRequired);

        // Loader throws when goal is missing (matches the schema's required list).
        Assert.Throws<System.InvalidOperationException>(() => TestPlanLoader.Parse("""
tests:
  T-1:
    title: "no goal here"
"""));
    }

    [Fact]
    public void SchemaAllowedActionsEnumMatchesActionVocabulary()
    {
        var schema = LoadSchema();
        var schemaActions = schema
            .GetProperty("$defs").GetProperty("testDefinition")
            .GetProperty("properties").GetProperty("allowed_actions")
            .GetProperty("items").GetProperty("enum")
            .EnumerateArray().Select(e => e.GetString()!).ToArray();

        // Every action the schema advertises must be one the validator accepts. This catches the
        // common drift — adding an action to the schema that the runner then rejects on every plan
        // that uses it.
        foreach (var action in schemaActions)
            Assert.True(
                ActionVocabulary.IsKnown(action),
                $"Schema allows action '{action}' that ActionVocabulary rejects.");
    }

    [Fact]
    public void SchemaCategoryEnumMatchesTestCategoryEnum()
    {
        var schema = LoadSchema();
        var schemaCategories = schema
            .GetProperty("$defs").GetProperty("testDefinition")
            .GetProperty("properties").GetProperty("category")
            .GetProperty("enum")
            .EnumerateArray().Select(e => e.GetString()!).ToArray();

        var codeCategories = System.Enum.GetNames(typeof(TestCategory));

        Assert.Equal(
            codeCategories.OrderBy(c => c),
            schemaCategories.OrderBy(c => c));
    }

    [Fact]
    public void SchemaPriorityEnumMatchesValidatorEnforcement()
    {
        var schema = LoadSchema();
        var schemaPriorities = schema
            .GetProperty("$defs").GetProperty("testDefinition")
            .GetProperty("properties").GetProperty("priority")
            .GetProperty("enum")
            .EnumerateArray().Select(e => e.GetString()!).ToArray();

        foreach (var p in schemaPriorities)
        {
            var plan = TestPlanLoader.Parse($"""
tests:
  T-1:
    goal: "g"
    priority: "{p}"
""");
            Assert.True(TestPlanValidator.Validate(plan, "p").IsValid);
        }
    }

    [Fact]
    public void YamlLoaderIsTolerantOfUnknownFields()
    {
        // Assert that parsing a test plan with unknown fields (e.g. legacy_field)
        // works without error and successfully loads known fields.
        var plan = TestPlanLoader.Parse("""
suite: smoke
schema_version: "1.0"
unknown_plan_field: "ignore_me"

tests:
  CONTRACT-TOLERANT-001:
    goal: "Confirm tolerant loading"
    framework: "winforms"
    unknown_test_field: "ignore_me_too"
    max_steps: 5
""");

        Assert.NotNull(plan);
        Assert.Equal("smoke", plan.Suite);
        Assert.Equal("1.0", plan.SchemaVersion);
        Assert.Single(plan.Tests);
        var test = plan.Tests[0];
        Assert.Equal("CONTRACT-TOLERANT-001", test.Id);
        Assert.Equal("Confirm tolerant loading", test.Goal);
        Assert.Equal("winforms", test.Framework);
        Assert.Equal(5, test.MaxSteps);
    }

    [Fact]
    public void RunArtifactLoaderIsTolerantOfUnknownProperties()
    {
        // Assert that JSON deserialization of run reports is tolerant of unknown properties.
        var json = """
{
  "version": "1.0",
  "runId": "abc12345",
  "testId": "T-1",
  "unknownProperty": "someValue",
  "steps": [
    {
      "stepNumber": 1,
      "outcome": "Succeeded",
      "unknownStepProp": 42
    }
  ]
}
""";
        var tempDir = Path.Combine(Path.GetTempPath(), "tolerant-json-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var reportPath = Path.Combine(tempDir, "report.json");
            File.WriteAllText(reportPath, json);

            var runs = RunArtifactLoader.LoadFromDirectory(tempDir);
            Assert.Single(runs);
            var run = runs[0];
            Assert.Equal("1.0", run.Version);
            Assert.Equal("abc12345", run.RunId);
            Assert.Equal("T-1", run.TestId);
            Assert.Single(run.Steps);
            Assert.Equal(1, run.Steps[0].StepNumber);
            Assert.Equal("Succeeded", run.Steps[0].Outcome);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    private static JsonElement LoadSchema()
    {
        var schemaPath = Path.Combine(RepoRoot, "schemas", "test-plan.schema.json");
        Assert.True(File.Exists(schemaPath), $"Schema file not found: {schemaPath}");
        using var doc = JsonDocument.Parse(File.ReadAllText(schemaPath));
        return doc.RootElement.Clone();
    }
}
