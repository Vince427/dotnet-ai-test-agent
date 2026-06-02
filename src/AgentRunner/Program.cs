using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using DesktopAiTestAgent.Core;
using DesktopAiTestAgent.UIAutomation;

namespace DesktopAiTestAgent.AgentRunner;

/// <summary>
/// AgentLoop agent orchestrator.
/// Implements: observe → decide → act → score → record loop
/// with retry, loop detection, scoring, structured logging, and run artifacts.
/// </summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        // --- Load Config ---
        var manualOnlyRequested =
            HasArgument(args, "--render-ui") ||
            HasArgument(args, "--validate-plan") ||
            HasArgument(args, "--list-tests") ||
            HasArgument(args, "--write-guard-demos") ||
            HasArgument(args, "--to-junit");
        var jsonManualOutputRequested =
            manualOnlyRequested &&
            HasOptionValue(args, "--format", "json");
        var config = WorkflowConfig.Load(
            loadDotEnv: !manualOnlyRequested,
            logConfig: !jsonManualOutputRequested);
        RunnerOptions options;
        try
        {
            options = RunnerOptions.Parse(args, config);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine("Invalid arguments: " + ex.Message);
            return 2;
        }

        if (options.RenderUiOnly)
        {
            var repoRoot = config.WorkflowDirectory ?? Directory.GetCurrentDirectory();

            SymphonyWorkbenchOptions BuildWorkbenchOptions(int autoRefresh) => new()
            {
                RepoRoot = repoRoot,
                OutputPath = options.UiOutputPath!,
                RunsRoot = config.WorkspaceRoot,
                AutoRefreshSeconds = autoRefresh,
                PlanPaths = string.IsNullOrWhiteSpace(options.PlanPath)
                    ? []
                    : new List<string> { options.PlanPath! }
            };

            if (!options.Watch)
            {
                var result = SymphonyWorkbenchGenerator.Generate(BuildWorkbenchOptions(0));
                Console.WriteLine($"AgentLoop Workbench written to {result.OutputPath} tests={result.TestCount} runs={result.RunCount}");
                return 0;
            }

            // Watch mode: regenerate on any change under the runs root, and embed a
            // browser auto-refresh so the page updates hands-free. No server, no .env.
            const int refreshSeconds = 3;
            var first = SymphonyWorkbenchGenerator.Generate(BuildWorkbenchOptions(refreshSeconds));
            Console.WriteLine($"AgentLoop Workbench (watch) written to {first.OutputPath} tests={first.TestCount} runs={first.RunCount}");
            Console.WriteLine($"Watching {config.WorkspaceRoot} for changes. Open the file in a browser; it auto-refreshes every {refreshSeconds}s. Press Ctrl+C to stop.");

            Directory.CreateDirectory(config.WorkspaceRoot);
            using var watcher = new FileSystemWatcher(config.WorkspaceRoot)
            {
                IncludeSubdirectories = true,
                EnableRaisingEvents = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
            };

            var pending = 1; // render once at startup already done; arm for subsequent changes
            void Arm(object? _, FileSystemEventArgs __) => System.Threading.Interlocked.Exchange(ref pending, 1);
            watcher.Created += Arm;
            watcher.Changed += Arm;
            watcher.Deleted += Arm;
            watcher.Renamed += (_, __) => System.Threading.Interlocked.Exchange(ref pending, 1);

            using var stop = new System.Threading.ManualResetEventSlim(false);
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Set(); };

            while (!stop.IsSet)
            {
                stop.Wait(500);
                if (System.Threading.Interlocked.Exchange(ref pending, 0) == 1 && !stop.IsSet)
                {
                    try
                    {
                        var r = SymphonyWorkbenchGenerator.Generate(BuildWorkbenchOptions(refreshSeconds));
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] workbench regenerated: runs={r.RunCount}");
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("Workbench regeneration failed: " + ex.Message);
                    }
                }
            }

            Console.WriteLine("Watch stopped.");
            return 0;
        }

        if (options.ValidatePlanOnly)
            return ValidatePlans(config, options);

        if (options.ListTestsOnly)
            return ListTests(config, options);

        if (options.WriteGuardDemosOnly)
            return WriteGuardDemos(options);

        if (options.ToJUnitOnly)
            return ToJUnit(config, options);

        // --- Runtime agent loop ---
        // Wire the real driver + LLM decider, then hand off to the orchestrator.
        // The driver is IDisposable, so Program owns its lifetime.
        var secretRedactor = new SecretRedactor();
        var llmService = new LlmService(config, secretRedactor);
        using var driver = new FlaUiDesktopDriver();

        var orchestrator = new RunOrchestrator(driver, llmService, config);
        return await orchestrator.RunAsync(options);
    }

    private static int WriteGuardDemos(RunnerOptions options)
    {
        var outputRoot = options.GuardDemoOutputRoot ?? Path.GetFullPath("runs");
        var result = GuardFailureDemoFactory.WriteAll(outputRoot);
        Console.WriteLine($"Guard demo artifacts written to {result.OutputRoot} runs={result.Artifacts.Count}");
        foreach (var artifact in result.Artifacts)
            Console.WriteLine($"- {artifact.RunId}: {artifact.TestId} result={artifact.Result}");

        return 0;
    }

    // Manual command: convert captured run artifacts (runs/<id>/report.json) into a
    // JUnit XML report for CI dashboards. No .env / LLM / desktop app required.
    private static int ToJUnit(WorkflowConfig config, RunnerOptions options)
    {
        var runs = RunArtifactLoader.LoadFromDirectory(config.WorkspaceRoot);
        var xml = JUnitReportWriter.Write(runs);

        var outputPath = Path.GetFullPath(options.JUnitOutputPath!);
        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir))
            Directory.CreateDirectory(outputDir);
        File.WriteAllText(outputPath, xml);

        Console.WriteLine($"JUnit report written to {outputPath} runs={runs.Count}");
        return 0;
    }

    private static bool HasArgument(string[] args, string name)
    {
        return Array.Exists(args, arg => string.Equals(arg, name, StringComparison.Ordinal));
    }

    private static bool HasOptionValue(string[] args, string name, string value)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.Ordinal) &&
                string.Equals(args[i + 1], value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static int ValidatePlans(WorkflowConfig config, RunnerOptions options)
    {
        var planPaths = ResolvePlanPaths(config, options);
        var output = new PlanValidationOutput
        {
            PlanCount = planPaths.Count
        };

        if (planPaths.Count == 0)
        {
            output.Errors.Add("No test plans found. Use --plan <path>, --suite <name>, or add YAML files under tests/.");
            output.ErrorCount = output.Errors.Count;
            output.Valid = false;
            WriteValidationOutput(options, output);
            return 2;
        }

        foreach (var planPath in planPaths)
        {
            var planOutput = new PlanValidationPlanOutput { Path = planPath };
            output.Plans.Add(planOutput);

            try
            {
                var plan = TestPlanLoader.Load(planPath);
                planOutput.Suite = plan.Suite;
                planOutput.TestCount = plan.Tests.Count;
                output.TestCount += plan.Tests.Count;

                if (!string.IsNullOrWhiteSpace(options.TestId) && plan.FindById(options.TestId!) == null)
                {
                    planOutput.Errors.Add($"{planPath}:{options.TestId}: test id was not found.");
                }

                var validation = TestPlanValidator.Validate(plan, planPath);
                foreach (var error in validation.Errors)
                {
                    planOutput.Errors.Add(error);
                }
            }
            catch (Exception ex)
            {
                planOutput.Errors.Add($"{planPath}: {ex.Message}");
            }

            planOutput.Valid = planOutput.Errors.Count == 0;
            output.Errors.AddRange(planOutput.Errors);
        }

        output.ErrorCount = output.Errors.Count;
        output.Valid = output.ErrorCount == 0;
        WriteValidationOutput(options, output);
        return output.Valid ? 0 : 2;
    }

    private static void WriteValidationOutput(RunnerOptions options, PlanValidationOutput output)
    {
        if (options.OutputFormat == CommandOutputFormat.Json)
        {
            WriteJson(output);
            return;
        }

        foreach (var plan in output.Plans)
        {
            if (plan.Valid)
                Console.WriteLine($"OK {plan.Path} suite={plan.Suite ?? "-"} tests={plan.TestCount}");
            else
                foreach (var error in plan.Errors)
                    Console.Error.WriteLine("ERROR " + error);
        }

        foreach (var error in output.Errors)
        {
            if (!output.Plans.Exists(plan => plan.Errors.Contains(error)))
                Console.Error.WriteLine(error);
        }

        if (output.Valid)
            Console.WriteLine($"Validation passed: plans={output.PlanCount} tests={output.TestCount}");
        else
            Console.Error.WriteLine($"Validation failed: plans={output.PlanCount} tests={output.TestCount} errors={output.ErrorCount}");
    }

    private static int ListTests(WorkflowConfig config, RunnerOptions options)
    {
        var planPaths = ResolvePlanPaths(config, options);
        var output = new TestListOutput();

        if (planPaths.Count == 0)
        {
            output.Errors.Add("No test plans found. Use --plan <path>, --suite <name>, or add YAML files under tests/.");
            output.Valid = false;
            WriteTestListOutput(options, output);
            return 2;
        }

        foreach (var planPath in planPaths)
        {
            TestPlan plan;
            try
            {
                plan = TestPlanLoader.Load(planPath);
            }
            catch (Exception ex)
            {
                output.Errors.Add($"{planPath}: {ex.Message}");
                continue;
            }

            foreach (var test in plan.Tests)
            {
                if (!string.IsNullOrWhiteSpace(options.TestId) &&
                    !string.Equals(test.Id, options.TestId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                output.Tests.Add(new ListedTestOutput
                {
                    PlanPath = planPath,
                    Suite = plan.Suite,
                    Id = test.Id,
                    Title = test.Title,
                    Priority = test.Priority,
                    Framework = test.Framework,
                    TargetWindow = test.TargetWindow,
                    SourceIssue = test.SourceIssue,
                    SourcePr = test.SourcePr,
                    AuthoringAgent = test.AuthoringAgent,
                    Risk = test.Risk,
                    CiProfile = test.CiProfile,
                    Goal = test.Goal,
                    SuccessCondition = test.SuccessCondition,
                    MaxSteps = test.MaxSteps,
                    AllowedActions = new List<string>(test.AllowedActions),
                    Tags = new List<string>(test.Tags),
                    ExistingTests = new List<string>(test.ExistingTests)
                });
            }
        }

        output.Count = output.Tests.Count;
        if (output.Count == 0 && output.Errors.Count == 0)
        {
            output.Errors.Add($"No tests matched '{options.TestId}'.");
        }

        output.Valid = output.Errors.Count == 0;
        WriteTestListOutput(options, output);
        return output.Valid ? 0 : 2;
    }

    private static void WriteTestListOutput(RunnerOptions options, TestListOutput output)
    {
        if (options.OutputFormat == CommandOutputFormat.Json)
        {
            WriteJson(output);
            return;
        }

        foreach (var error in output.Errors)
            Console.Error.WriteLine("ERROR " + error);

        if (output.Errors.Count > 0)
            return;

        Console.WriteLine("ID\tSuite\tFramework\tPriority\tTitle\tTarget");
        foreach (var test in output.Tests)
            Console.WriteLine($"{test.Id}\t{test.Suite ?? "-"}\t{test.Framework ?? "-"}\t{test.Priority ?? "-"}\t{test.Title ?? "-"}\t{test.TargetWindow ?? "-"}");
    }

    private static List<string> ResolvePlanPaths(WorkflowConfig config, RunnerOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.PlanPath))
            return [options.PlanPath!];

        var repoRoot = config.WorkflowDirectory ?? Directory.GetCurrentDirectory();
        return TestPlanLoader.DiscoverPlanPaths(repoRoot);
    }

    private static void WriteJson<T>(T value)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        options.Converters.Add(new JsonStringEnumConverter());
        Console.WriteLine(JsonSerializer.Serialize(value, options));
    }
}
