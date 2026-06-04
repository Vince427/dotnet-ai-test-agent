using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner;

/// <summary>
/// Default <see cref="IRunOrchestrator"/>: the observe → decide → act → score →
/// record loop, extracted out of <c>Program.Main</c> so it can be driven by a
/// fake <see cref="IAutomationDriver"/> and a scripted <see cref="IActionDecider"/>
/// in deterministic unit tests.
///
/// The driver is injected (the caller owns its lifetime / disposal). Supporting
/// components — logger, memory, loop detector, scoring, quality guards, artifact
/// writer — are deterministic and constructed internally from <paramref name="config"/>.
/// The two inter-step delays are parameterized so tests can pass <c>0</c>.
/// </summary>
public sealed class RunOrchestrator(
    IAutomationDriver driver,
    IActionDecider decider,
    WorkflowConfig config,
    int interStepDelayMs = 500,
    int waitActionDelayMs = 1000) : IRunOrchestrator
{
    private readonly IAutomationDriver _driver = driver;
    private readonly IActionDecider _decider = decider;
    private readonly WorkflowConfig _config = config;
    private readonly int _interStepDelayMs = interStepDelayMs;
    private readonly int _waitActionDelayMs = waitActionDelayMs;

    public RunArtifact? LastArtifact { get; private set; }

    public async Task<int> RunAsync(RunnerOptions options)
    {
        // Root span for the whole run. Null when nobody is listening (telemetry off).
        using var runActivity = RunnerTelemetry.Source.StartActivity("agentloop.run", ActivityKind.Internal);
        var runStopwatch = Stopwatch.StartNew();
        var exitCode = 3;
        try
        {
            exitCode = await RunCoreAsync(options, runActivity);
            return exitCode;
        }
        finally
        {
            runStopwatch.Stop();
            var result = LastArtifact?.Result ?? "Unknown";
            var resultTag = new KeyValuePair<string, object?>("agentloop.result", result);
            RunnerTelemetry.RunDuration.Record(runStopwatch.Elapsed.TotalMilliseconds, resultTag);
            if (LastArtifact != null)
                RunnerTelemetry.RunScore.Record(LastArtifact.FinalScore, resultTag);
            runActivity?.SetTag("agentloop.result", result);
            runActivity?.SetTag("agentloop.exit_code", exitCode);
            if (exitCode != 0)
                runActivity?.SetStatus(ActivityStatusCode.Error, LastArtifact?.ErrorMessage);
        }
    }

    private async Task<int> RunCoreAsync(RunnerOptions options, Activity? runActivity)
    {
        var targetWindow = options.TargetWindow;
        var goal = options.Goal;

        // --- Initialize components ---
        var secretRedactor = new SecretRedactor();
        var logger = new StructuredLogger(goal.Identifier, null);
        var memory = new AgentMemory(secretRedactor);
        var loopDetector = new LoopDetector();
        var scoring = new ScoringEngine { AbortThreshold = _config.AbortThreshold };
        var qualityGuards = QualityGuardEngine.CreateDefault();
        var artifactWriter = new ArtifactWriter(_config.WorkspaceRoot, secretRedactor);
        var actionExecutor = new ActionExecutor(_driver, secretRedactor, logger, memory, _waitActionDelayMs);

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
            EvidenceLevel = options.EvidenceLevel,
            ExistingTests = options.Test?.ExistingTests is { } existingTests ? new List<string>(existingTests) : [],
            SourceIssue = options.Test?.SourceIssue,
            SourcePr = options.Test?.SourcePr
        };
        LastArtifact = runArtifact;

        // Persist the trace id so a recorded run links to its live trace (OBS-1).
        runArtifact.TraceId = runActivity?.TraceId.ToString();
        runActivity?.SetTag("agentloop.run_id", runArtifact.RunId);
        runActivity?.SetTag("agentloop.goal_id", goal.Identifier);
        runActivity?.SetTag("agentloop.target_window", targetWindow);
        runActivity?.SetTag("agentloop.test_id", options.TestId);
        runActivity?.SetTag("agentloop.max_steps", goal.MaxSteps);

        var sessionId = $"{runArtifact.RunId}-{DateTime.UtcNow:HHmmss}";
        logger.SetContext(goal.Identifier, sessionId);

        logger.Info($"Desktop AI Test Agent V1.3 (AgentLoop Architecture)");
        logger.Info($"goal=\"{goal.Description}\" target=\"{targetWindow}\" max_steps={goal.MaxSteps}");

        // --- Attach to window ---
        logger.Info("Attaching to target window...");
        if (!_driver.AttachToWindow(targetWindow, TimeSpan.FromSeconds(20)))
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
            using var stepActivity = RunnerTelemetry.Source.StartActivity("agentloop.step", ActivityKind.Internal);
            stepActivity?.SetTag("agentloop.step", step);
            var stepStopwatch = Stopwatch.StartNew();

            // 1. OBSERVE — capture full UI state
            UiSnapshot snapshot;
            using (RunnerTelemetry.Source.StartActivity("agentloop.observe"))
            {
                try
                {
                    snapshot = _driver.Capture();
                }
                catch (Exception ex)
                {
                    logger.Error("Failed to capture UI state", ex);
                    continue;
                }
            }

            // Record screen signature for exploration tracking
            var screenSig = $"{snapshot.WindowTitle}|{snapshot.Elements.Count}";
            memory.RecordScreen(screenSig);

            // Check for goal success condition (scans every status region, not just the first).
            var statusText = snapshot.FindStatusText();
            if (!string.IsNullOrEmpty(goal.SuccessCondition) &&
                snapshot.StatusContains(goal.SuccessCondition!))
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
            using (var decideActivity = RunnerTelemetry.Source.StartActivity("agentloop.decide", ActivityKind.Client))
            {
                try
                {
                    action = await _decider.DecideActionAsync(
                        snapshot, goal, memory.GetFullContextString(), loopWarning);
                }
                catch (Exception ex)
                {
                    decideActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                    logger.Error("LLM call failed", ex);
                    var llmScoreDelta = scoring.ScoreAction("LlmCall", false, false, ex.Message);
                    runArtifact.Steps.Add(new RunStep
                    {
                        StepNumber = step,
                        UiStateSnapshot = $"{snapshot.WindowTitle} ({snapshot.Elements.Count} elements)",
                        ActionType = "LlmCall",
                        Reasoning = ex.Message,
                        Outcome = "Failed",
                        FailureCode = "llm_call_failed",
                        FailureMessage = ex.Message,
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

                    await Task.Delay(_config.PollIntervalMs); // Wait before retrying to avoid spamming 429
                    continue;
                }
            }

            if (action == null)
            {
                logger.Warning("Agent returned null action.");
                break;
            }

            // Update loop detector with actual action
            var actionKey = $"{action.ActionType}:{action.AutomationId}";
            bool isLoop = loopDetector.RecordAndCheck(actionKey);
            var safeActionValue = secretRedactor.RedactActionValue(action);
            var safeReason = secretRedactor.RedactText(action.Reason);

            logger.Decision($"action={action.ActionType} target={action.AutomationId} value={safeActionValue} confidence={action.Confidence} reason=\"{safeReason}\"");

            // 3. ACT — execute the action
            bool succeeded = true;
            string outcomeDetail = "ok";
            var runStep = new RunStep
            {
                StepNumber = step,
                UiStateSnapshot = $"{snapshot.WindowTitle} ({snapshot.Elements.Count} elements)",
                ActionType = action.ActionType,
                ActionTarget = action.AutomationId,
                ActionValue = safeActionValue,
                Reasoning = safeReason
            };

            if (options.EvidenceLevel == EvidenceLevel.Full)
                runStep.UiTreePath = artifactWriter.SaveUiTreeSnapshot(runArtifact.RunId, step, snapshot);

            var execResult = await actionExecutor.ExecuteAsync(action, snapshot, goal);
            succeeded = execResult.Succeeded;
            outcomeDetail = execResult.OutcomeDetail;
            if (execResult.FailureCode != null)
                runStep.FailureCode = execResult.FailureCode;
            if (execResult.FailureMessage != null)
                runStep.FailureMessage = execResult.FailureMessage;

            // V8 self-healing (evidence only): if the target drifted (named but not present),
            // record the closest present element as a suggestion. Never auto-applied — CI stays
            // deterministic; a human or a later local-only step decides whether to adopt it.
            if (execResult.FailureCode == "action_target_not_found")
            {
                var heal = SelectorHealer.Suggest(action.AutomationId, snapshot);
                if (heal != null)
                {
                    runStep.HealingSuggestion = heal;
                    logger.Info($"Heal suggestion: {heal.Rationale}");
                }
            }

            // Done with the success condition satisfied is the only terminal-success path; the
            // loop records the final step and stops here. Every other outcome falls through to
            // the guard / score / record stages below.
            if (execResult.DoneSucceeded)
            {
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

            QualityGuardResult? guardResult = null;
            if (succeeded)
            {
                guardResult = qualityGuards.Check(new QualityGuardContext
                {
                    StepNumber = step,
                    Driver = _driver,
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
                    runStep.FailureCode = guardResult.Code;
                    runStep.FailureMessage = guardResult.Message;
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
                    var screenshotBytes = _driver.CaptureScreenshot();
                    // Redact-at-source: paint over secret-field regions before the PNG
                    // ever hits disk, so artifacts can't leak rendered secrets.
                    var secretRegions = ScreenshotRedaction.SecretRegions(snapshot, secretRedactor);
                    if (secretRegions.Count > 0)
                        screenshotBytes = UIAutomation.ScreenshotMasker.MaskRegions(screenshotBytes, secretRegions);
                    var screenshotPath = artifactWriter.SaveScreenshot(runArtifact.RunId, step, screenshotBytes);
                    runStep.ScreenshotPath = screenshotPath;

                    // V3 Tier-2: at full evidence, also emit the numbered-box overlay + its index
                    // (the no-key artifact a VLM later consumes to disambiguate). Built on top of
                    // the already-masked bytes so secrets stay masked under the annotations.
                    if (options.EvidenceLevel == EvidenceLevel.Full)
                    {
                        var overlayIndex = ScreenshotOverlay.BuildIndex(snapshot);
                        if (overlayIndex.Count > 0)
                        {
                            var annotated = UIAutomation.ScreenshotAnnotator.Annotate(
                                screenshotBytes, ScreenshotOverlay.ToBoxes(overlayIndex));
                            runStep.OverlayPath = artifactWriter.SaveOverlay(runArtifact.RunId, step, annotated);
                            runStep.OverlayIndexPath = artifactWriter.SaveOverlayIndex(runArtifact.RunId, step, overlayIndex);
                        }
                    }
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

            // Telemetry: the step span carries the act/guard/score outcome; metrics
            // aggregate per-step duration and outcome. (Secret-free tags only.)
            stepStopwatch.Stop();
            var outcome = succeeded ? "Succeeded" : "Failed";
            var actionTypeTag = action.ActionType ?? "Unknown";
            stepActivity?.SetTag("agentloop.action_type", actionTypeTag);
            stepActivity?.SetTag("agentloop.action_target", action.AutomationId);
            stepActivity?.SetTag("agentloop.outcome", outcome);
            stepActivity?.SetTag("agentloop.score_delta", scoreDelta);
            stepActivity?.SetTag("agentloop.cumulative_score", scoring.TotalScore);
            if (guardResult is { Status: not QualityGuardStatus.Passed })
                stepActivity?.SetTag("agentloop.guard", $"{guardResult.Status}:{guardResult.Code}");
            if (!succeeded)
                stepActivity?.SetStatus(ActivityStatusCode.Error, outcomeDetail);
            var stepTags = new TagList
            {
                { "agentloop.action_type", actionTypeTag },
                { "agentloop.outcome", outcome }
            };
            RunnerTelemetry.StepCount.Add(1, stepTags);
            RunnerTelemetry.StepDuration.Record(stepStopwatch.Elapsed.TotalMilliseconds, stepTags);

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

            await Task.Delay(_interStepDelayMs); // Small pause for UI to update
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
