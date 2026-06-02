using System;
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
        LastArtifact = runArtifact;

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
            // 1. OBSERVE — capture full UI state
            UiSnapshot snapshot;
            try
            {
                snapshot = _driver.Capture();
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
                action = await _decider.DecideActionAsync(
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

            try
            {
                if (!IsActionAllowed(goal, action.ActionType))
                {
                    succeeded = false;
                    outcomeDetail = "action_not_allowed";
                    runStep.FailureCode = outcomeDetail;
                    runStep.FailureMessage = $"Action '{action.ActionType}' is not listed in allowed_actions.";
                }
                else
                {
                    var targetValidation = AgentActionValidator.ValidateTargetExists(action, snapshot);
                    if (!targetValidation.IsValid)
                    {
                        succeeded = false;
                        outcomeDetail = targetValidation.Code ?? "action_target_invalid";
                        runStep.FailureCode = outcomeDetail;
                        runStep.FailureMessage = targetValidation.Message;
                        logger.Warning(targetValidation.Message ?? outcomeDetail);
                    }
                }

                if (!succeeded)
                {
                    // Validation failed before dispatching to the automation driver.
                }
                else if (string.Equals(action.ActionType, "EnterText", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrEmpty(action.AutomationId))
                {
                    _driver.EnterText(action.AutomationId!, action.Value ?? "");
                    memory.AddFact($"entered_{action.AutomationId}", action.Value ?? "");
                }
                else if (string.Equals(action.ActionType, "Click", StringComparison.OrdinalIgnoreCase) &&
                         !string.IsNullOrEmpty(action.AutomationId))
                {
                    _driver.Click(action.AutomationId!);
                }
                else if (string.Equals(action.ActionType, "DoubleClick", StringComparison.OrdinalIgnoreCase) &&
                         !string.IsNullOrEmpty(action.AutomationId))
                {
                    _driver.DoubleClick(action.AutomationId!);
                }
                else if (string.Equals(action.ActionType, "Scroll", StringComparison.OrdinalIgnoreCase) &&
                         !string.IsNullOrEmpty(action.AutomationId))
                {
                    _driver.Scroll(action.AutomationId!, action.Value ?? "down");
                }
                else if (string.Equals(action.ActionType, "Assert", StringComparison.OrdinalIgnoreCase) &&
                         !string.IsNullOrEmpty(action.AutomationId))
                {
                    var actualText = _driver.ReadText(action.AutomationId!);
                    if (!string.Equals(actualText, action.Value, StringComparison.OrdinalIgnoreCase))
                    {
                        var safeExpected = secretRedactor.RedactActionValue(action);
                        var safeActual = secretRedactor.RedactValueForIdentifier(action.AutomationId, actualText);
                        succeeded = false;
                        outcomeDetail = $"assertion_failed on {action.AutomationId}. Expected: '{safeExpected}', Actual: '{safeActual}'";
                        runStep.FailureCode = "assertion_failed";
                        runStep.FailureMessage = outcomeDetail;
                    }
                    else
                    {
                        var safeActual = secretRedactor.RedactValueForIdentifier(action.AutomationId, actualText);
                        logger.Info($"Assertion passed on {action.AutomationId}: '{safeActual}'");
                    }
                }
                else if (string.Equals(action.ActionType, "Wait", StringComparison.OrdinalIgnoreCase))
                {
                    await Task.Delay(_waitActionDelayMs);
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
                        runStep.FailureCode = outcomeDetail;
                        runStep.FailureMessage = "Agent signaled Done before the configured success condition was visible.";
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
                    runStep.FailureCode = outcomeDetail;
                    runStep.FailureMessage = $"Unsupported action '{action.ActionType}'.";
                }
            }
            catch (Exception ex)
            {
                succeeded = false;
                outcomeDetail = secretRedactor.RedactText(ex.Message) ?? ex.Message;
                runStep.FailureCode = "action_failed";
                runStep.FailureMessage = outcomeDetail;
                logger.Error($"Action failed: {action.ActionType} on {action.AutomationId}: {outcomeDetail}");
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
}
