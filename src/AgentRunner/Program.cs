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
/// Symphony-inspired agent orchestrator.
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
            HasArgument(args, "--list-tests");
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

        var targetWindow = options.TargetWindow;
        var goal = options.Goal;

        if (options.RenderUiOnly)
        {
            var repoRoot = config.WorkflowDirectory ?? Directory.GetCurrentDirectory();
            var result = SymphonyWorkbenchGenerator.Generate(new SymphonyWorkbenchOptions
            {
                RepoRoot = repoRoot,
                OutputPath = options.UiOutputPath!,
                RunsRoot = config.WorkspaceRoot,
                PlanPaths = string.IsNullOrWhiteSpace(options.PlanPath)
                    ? []
                    : new List<string> { options.PlanPath! }
            });

            Console.WriteLine($"Symphony workbench written to {result.OutputPath} tests={result.TestCount} runs={result.RunCount}");
            return 0;
        }

        if (options.ValidatePlanOnly)
            return ValidatePlans(config, options);

        if (options.ListTestsOnly)
            return ListTests(config, options);

        // --- Initialize components ---
        var logger = new StructuredLogger(goal.Identifier, null);
        var memory = new AgentMemory();
        var loopDetector = new LoopDetector();
        var scoring = new ScoringEngine { AbortThreshold = config.AbortThreshold };
        var qualityGuards = QualityGuardEngine.CreateDefault();
        var artifactWriter = new ArtifactWriter(config.WorkspaceRoot);
        var llmService = new LlmService(config);

        var runArtifact = new RunArtifact
        {
            GoalDescription = goal.Description,
            GoalIdentifier = goal.Identifier,
            TargetWindow = targetWindow,
            TestId = options.TestId,
            TestTitle = options.Test?.Title,
            TestPriority = options.Test?.Priority,
            Framework = options.Test?.Framework,
            Suite = options.Suite,
            EvidenceLevel = options.EvidenceLevel
        };

        var sessionId = $"{runArtifact.RunId}-{DateTime.UtcNow:HHmmss}";
        logger.SetContext(goal.Identifier, sessionId);

        logger.Info($"Desktop AI Test Agent V1.3 (Symphony Architecture)");
        logger.Info($"goal=\"{goal.Description}\" target=\"{targetWindow}\" max_steps={goal.MaxSteps}");

        // --- Attach to window ---
        using var driver = new FlaUiDesktopDriver();
        {
            logger.Info("Attaching to target window...");
            if (!driver.AttachToWindow(targetWindow, TimeSpan.FromSeconds(20)))
            {
                logger.Error("Could not attach to target window.");
                runArtifact.Result = options.Test != null ? "Blocked" : "Failed";
                runArtifact.ErrorMessage = "Window not found: " + targetWindow;
                runArtifact.EndedAt = DateTime.UtcNow;
                artifactWriter.WriteReport(runArtifact);
                artifactWriter.WriteSummary(runArtifact);
                return options.Test != null ? 4 : 1;
            }

            logger.Info("Window attached. Starting agent loop...");

            // --- Main observe → decide → act → score loop ---
            string? loopWarning = null;
            for (int step = 1; step <= goal.MaxSteps; step++)
            {
                // 1. OBSERVE — capture full UI state
                UiSnapshot snapshot;
                try
                {
                    snapshot = driver.Capture();
                }
                catch (Exception ex)
                {
                    logger.Error("Failed to capture UI state", ex);
                    continue;
                }

                // Record screen signature for exploration tracking
                var screenSig = $"{snapshot.WindowTitle}|{snapshot.Elements.Count}";
                memory.RecordScreen(screenSig);

                // Check for goal success condition
                var statusText = snapshot.FindStatusText();
                if (!string.IsNullOrEmpty(goal.SuccessCondition) &&
                    !string.IsNullOrEmpty(statusText) &&
                    statusText!.IndexOf(goal.SuccessCondition, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    logger.Info($"SUCCESS: Goal achieved. Status=\"{statusText}\"");
                    scoring.ScoreAction("Done", true, false, "success");

                    runArtifact.Result = options.Test != null ? "Passed" : "Succeeded";
                    runArtifact.FinalScore = scoring.TotalScore;
                    runArtifact.EndedAt = DateTime.UtcNow;
                    artifactWriter.WriteReport(runArtifact);
                    artifactWriter.WriteSummary(runArtifact);
                    logger.Score(scoring.GetSummary());
                    return 0;
                }

                // 2. DECIDE — ask LLM for next action
                logger.Info($"[Step {step}/{goal.MaxSteps}] Asking agent... elements={snapshot.Elements.Count}");

                AgentAction action;
                try
                {
                    action = await llmService.DecideActionAsync(
                        snapshot, goal, memory.GetFullContextString(), loopWarning);
                }
                catch (Exception ex)
                {
                    logger.Error("LLM call failed", ex);
                    var llmScoreDelta = scoring.ScoreAction("LlmCall", false, false, ex.Message);
                    runArtifact.Steps.Add(new RunStep
                    {
                        StepNumber = step,
                        UiStateSnapshot = $"{snapshot.WindowTitle} ({snapshot.Elements.Count} elements)",
                        ActionType = "LlmCall",
                        Reasoning = ex.Message,
                        Outcome = "Failed",
                        ScoreDelta = llmScoreDelta,
                        CumulativeScore = scoring.TotalScore
                    });
                    logger.Score(scoring.GetSummary());

                    if (scoring.ShouldAbort())
                    {
                        logger.Warning($"Score below threshold ({scoring.TotalScore}). Aborting.");
                        runArtifact.Result = "Aborted";
                        runArtifact.ErrorMessage = $"Score dropped to {scoring.TotalScore} after LLM call failures.";
                        runArtifact.FinalScore = scoring.TotalScore;
                        runArtifact.EndedAt = DateTime.UtcNow;
                        artifactWriter.WriteReport(runArtifact);
                        artifactWriter.WriteSummary(runArtifact);
                        return 3;
                    }

                    await Task.Delay(config.PollIntervalMs); // Wait before retrying to avoid spamming 429
                    continue;
                }

                if (action == null)
                {
                    logger.Warning("Agent returned null action.");
                    break;
                }

                // Update loop detector with actual action
                var actionKey = $"{action.ActionType}:{action.AutomationId}";
                bool isLoop = loopDetector.RecordAndCheck(actionKey);

                logger.Decision($"action={action.ActionType} target={action.AutomationId} value={action.Value} confidence={action.Confidence} reason=\"{action.Reason}\"");

                // 3. ACT — execute the action
                bool succeeded = true;
                string outcomeDetail = "ok";
                var runStep = new RunStep
                {
                    StepNumber = step,
                    UiStateSnapshot = $"{snapshot.WindowTitle} ({snapshot.Elements.Count} elements)",
                    ActionType = action.ActionType,
                    ActionTarget = action.AutomationId,
                    ActionValue = action.Value,
                    Reasoning = action.Reason
                };

                if (options.EvidenceLevel == EvidenceLevel.Full)
                    runStep.UiTreePath = artifactWriter.SaveUiTreeSnapshot(runArtifact.RunId, step, snapshot);

                try
                {
                    if (!IsActionAllowed(goal, action.ActionType))
                    {
                        succeeded = false;
                        outcomeDetail = "action_not_allowed";
                    }
                    else if (string.Equals(action.ActionType, "EnterText", StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrEmpty(action.AutomationId))
                    {
                        driver.EnterText(action.AutomationId!, action.Value ?? "");
                        memory.AddFact($"entered_{action.AutomationId}", action.Value ?? "");
                    }
                    else if (string.Equals(action.ActionType, "Click", StringComparison.OrdinalIgnoreCase) &&
                             !string.IsNullOrEmpty(action.AutomationId))
                    {
                        driver.Click(action.AutomationId!);
                    }
                    else if (string.Equals(action.ActionType, "DoubleClick", StringComparison.OrdinalIgnoreCase) &&
                             !string.IsNullOrEmpty(action.AutomationId))
                    {
                        driver.DoubleClick(action.AutomationId!);
                    }
                    else if (string.Equals(action.ActionType, "Scroll", StringComparison.OrdinalIgnoreCase) &&
                             !string.IsNullOrEmpty(action.AutomationId))
                    {
                        driver.Scroll(action.AutomationId!, action.Value ?? "down");
                    }
                    else if (string.Equals(action.ActionType, "Assert", StringComparison.OrdinalIgnoreCase) &&
                             !string.IsNullOrEmpty(action.AutomationId))
                    {
                        var actualText = driver.ReadText(action.AutomationId!);
                        if (!string.Equals(actualText, action.Value, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new Exception($"Assertion failed on {action.AutomationId}. Expected: '{action.Value}', Actual: '{actualText}'");
                        }
                        logger.Info($"Assertion passed on {action.AutomationId}: '{actualText}'");
                    }
                    else if (string.Equals(action.ActionType, "Wait", StringComparison.OrdinalIgnoreCase))
                    {
                        await Task.Delay(1000);
                    }
                    else if (string.Equals(action.ActionType, "Done", StringComparison.OrdinalIgnoreCase))
                    {
                        var doneStatusText = snapshot.FindStatusText();
                        if (!string.IsNullOrEmpty(goal.SuccessCondition) &&
                            (string.IsNullOrEmpty(doneStatusText) ||
                             doneStatusText!.IndexOf(goal.SuccessCondition, StringComparison.OrdinalIgnoreCase) < 0))
                        {
                            succeeded = false;
                            outcomeDetail = "done_without_success_condition";
                            logger.Warning($"Agent signaled Done before success condition was visible. status=\"{doneStatusText ?? ""}\"");
                        }
                        else
                        {
                            logger.Info("Agent signaled Done.");
                            outcomeDetail = "agent_done";
                            runStep.Outcome = "Succeeded";

                            runArtifact.Result = options.Test != null ? "Passed" : "Succeeded";
                            runArtifact.FinalScore = scoring.TotalScore;
                            runArtifact.EndedAt = DateTime.UtcNow;
                            runArtifact.Steps.Add(runStep);
                            artifactWriter.WriteReport(runArtifact);
                            artifactWriter.WriteSummary(runArtifact);
                            logger.Score(scoring.GetSummary());
                            return 0;
                        }
                    }
                    else if (string.Equals(action.ActionType, "Explore", StringComparison.OrdinalIgnoreCase))
                    {
                        // Explore = just re-capture, no real action
                        outcomeDetail = "explore";
                    }
                    else
                    {
                        succeeded = false;
                        outcomeDetail = "unsupported_action";
                    }
                }
                catch (Exception ex)
                {
                    succeeded = false;
                    outcomeDetail = ex.Message;
                    logger.Error($"Action failed: {action.ActionType} on {action.AutomationId}", ex);
                }

                QualityGuardResult? guardResult = null;
                if (succeeded)
                {
                    guardResult = qualityGuards.Check(new QualityGuardContext
                    {
                        StepNumber = step,
                        Driver = driver,
                        SnapshotBefore = snapshot,
                        Action = action,
                        Goal = goal
                    });

                    if (guardResult.Status != QualityGuardStatus.Passed)
                    {
                        succeeded = false;
                        var guardPrefix = guardResult.Status == QualityGuardStatus.Abort
                            ? "guard_abort"
                            : "guard_force_reject";
                        outcomeDetail = $"{guardPrefix}:{guardResult.Code}";
                        runStep.GuardStatus = guardResult.Status.ToString();
                        runStep.GuardCode = guardResult.Code;
                        runStep.GuardMessage = guardResult.Message;
                        logger.Warning($"Quality guard {guardResult.Status}: {guardResult.Code} - {guardResult.Message}");
                    }
                }

                // 4. SCORE
                int scoreDelta = scoring.ScoreAction(action.ActionType ?? "Wait", succeeded, isLoop, outcomeDetail);
                runStep.Outcome = succeeded ? "Succeeded" : "Failed";
                runStep.ScoreDelta = scoreDelta;
                runStep.CumulativeScore = scoring.TotalScore;

                // Save screenshot
                if (options.EvidenceLevel != EvidenceLevel.Minimal)
                {
                    try
                    {
                        var screenshotBytes = driver.CaptureScreenshot();
                        var screenshotPath = artifactWriter.SaveScreenshot(runArtifact.RunId, step, screenshotBytes);
                        runStep.ScreenshotPath = screenshotPath;
                    }
                    catch (Exception ex)
                    {
                        logger.Warning($"Screenshot failed: {ex.Message}");
                    }
                }

                runArtifact.Steps.Add(runStep);

                // 5. RECORD
                memory.AddAction($"[Step {step}] {action.ActionType} on {action.AutomationId} → {outcomeDetail}");
                logger.Action(action.ActionType ?? "Unknown", action.AutomationId ?? "N/A", outcomeDetail);
                logger.Score(scoring.GetSummary());

                // 6. CHECK — should we abort?
                if (guardResult?.Status == QualityGuardStatus.Abort)
                {
                    logger.Warning($"Quality guard requested abort: {guardResult.Code}");
                    runArtifact.Result = "Aborted";
                    runArtifact.ErrorMessage = guardResult.Message;
                    break;
                }

                if (scoring.ShouldAbort())
                {
                    logger.Warning($"Score below threshold ({scoring.TotalScore}). Aborting.");
                    runArtifact.Result = "Aborted";
                    runArtifact.ErrorMessage = $"Score dropped to {scoring.TotalScore}";
                    break;
                }

                if (isLoop)
                {
                    loopWarning = loopDetector.GetPatternSummary();
                    logger.Warning($"Loop detected: {loopDetector.GetPatternSummary()}");
                    // Don't abort on first loop, but the LLM will get a warning next iteration
                }
                else
                {
                    loopWarning = null;
                }

                await Task.Delay(500); // Small pause for UI to update
            }

            // --- Finalize ---
            if (runArtifact.Result == "Running")
            {
                runArtifact.Result = "Failed";
                runArtifact.ErrorMessage = "Reached max steps without achieving goal.";
            }

            runArtifact.FinalScore = scoring.TotalScore;
            runArtifact.EndedAt = DateTime.UtcNow;
            artifactWriter.WriteReport(runArtifact);
            artifactWriter.WriteSummary(runArtifact);

            logger.Score(scoring.GetSummary());
            logger.Error($"FAILURE: {runArtifact.ErrorMessage}");
            return 3;
        }
    }

    private static bool IsActionAllowed(AgentGoal goal, string? actionType)
    {
        if (goal.AllowedActions.Count == 0)
            return true;
        if (string.IsNullOrWhiteSpace(actionType))
            return false;

        foreach (var allowedAction in goal.AllowedActions)
        {
            if (string.Equals(allowedAction, actionType, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
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
        var testsDir = Path.Combine(repoRoot, "tests");
        if (!Directory.Exists(testsDir))
            return [];

        return Directory.GetFiles(testsDir, "*.yaml")
            .Concat(Directory.GetFiles(testsDir, "*.yml"))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
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
