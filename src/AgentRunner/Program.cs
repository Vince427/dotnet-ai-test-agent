using System;
using System.Threading;
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
        // --- Parse arguments ---
        var targetWindow = "Sample Login App (.NET 8)";
        var goalDescription = "Log in to the application using username 'admin' and password 'password123'.";
        var successCondition = "Login successful";
        var goalId = "login";
        int maxSteps = 30;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--window" && i + 1 < args.Length)
                targetWindow = args[++i];
            else if (args[i] == "--goal" && i + 1 < args.Length)
                goalDescription = args[++i];
            else if (args[i] == "--success" && i + 1 < args.Length)
                successCondition = args[++i];
            else if (args[i] == "--goal-id" && i + 1 < args.Length)
                goalId = args[++i];
            else if (args[i] == "--max-steps" && i + 1 < args.Length)
                maxSteps = int.Parse(args[++i]);
            else if (i == 0 && !args[i].StartsWith("--"))
                targetWindow = args[i]; // backward compat: first positional arg = window title
        }

        var goal = new AgentGoal
        {
            Description = goalDescription,
            SuccessCondition = successCondition,
            MaxSteps = maxSteps,
            Identifier = goalId
        };

        // --- Initialize components ---
        var logger = new StructuredLogger(goal.Identifier, null);
        var memory = new AgentMemory();
        var loopDetector = new LoopDetector();
        var scoring = new ScoringEngine();
        var artifactWriter = new ArtifactWriter();
        var llmService = new LlmService();

        var runArtifact = new RunArtifact
        {
            GoalDescription = goal.Description,
            GoalIdentifier = goal.Identifier,
            TargetWindow = targetWindow
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
                runArtifact.Result = "Failed";
                runArtifact.ErrorMessage = "Window not found: " + targetWindow;
                runArtifact.EndedAt = DateTime.UtcNow;
                artifactWriter.WriteReport(runArtifact);
                artifactWriter.WriteSummary(runArtifact);
                return 1;
            }

            logger.Info("Window attached. Starting agent loop...");

            // --- Main observe → decide → act → score loop ---
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

                    runArtifact.Result = "Succeeded";
                    runArtifact.FinalScore = scoring.TotalScore;
                    runArtifact.EndedAt = DateTime.UtcNow;
                    artifactWriter.WriteReport(runArtifact);
                    artifactWriter.WriteSummary(runArtifact);
                    logger.Score(scoring.GetSummary());
                    return 0;
                }

                // 2. DECIDE — ask LLM for next action
                string? loopWarning = null;
                if (loopDetector.RecordAndCheck("pending"))
                    loopWarning = loopDetector.GetPatternSummary();

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
                    scoring.ScoreAction("Wait", false, false);
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

                try
                {
                    if (string.Equals(action.ActionType, "EnterText", StringComparison.OrdinalIgnoreCase) &&
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
                    else if (string.Equals(action.ActionType, "Wait", StringComparison.OrdinalIgnoreCase))
                    {
                        Thread.Sleep(1000);
                    }
                    else if (string.Equals(action.ActionType, "Done", StringComparison.OrdinalIgnoreCase))
                    {
                        logger.Info("Agent signaled Done.");
                        outcomeDetail = "agent_done";
                        runStep.Outcome = "Succeeded";

                        runArtifact.Result = "Succeeded";
                        runArtifact.FinalScore = scoring.TotalScore;
                        runArtifact.EndedAt = DateTime.UtcNow;
                        runArtifact.Steps.Add(runStep);
                        artifactWriter.WriteReport(runArtifact);
                        artifactWriter.WriteSummary(runArtifact);
                        logger.Score(scoring.GetSummary());
                        return 0;
                    }
                    else if (string.Equals(action.ActionType, "Explore", StringComparison.OrdinalIgnoreCase))
                    {
                        // Explore = just re-capture, no real action
                        outcomeDetail = "explore";
                    }
                }
                catch (Exception ex)
                {
                    succeeded = false;
                    outcomeDetail = ex.Message;
                    logger.Error($"Action failed: {action.ActionType} on {action.AutomationId}", ex);
                }

                // 4. SCORE
                int scoreDelta = scoring.ScoreAction(action.ActionType ?? "Wait", succeeded, isLoop, outcomeDetail);
                runStep.Outcome = succeeded ? "Succeeded" : "Failed";
                runStep.ScoreDelta = scoreDelta;
                runStep.CumulativeScore = scoring.TotalScore;

                // Save screenshot
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

                runArtifact.Steps.Add(runStep);

                // 5. RECORD
                memory.AddAction($"[Step {step}] {action.ActionType} on {action.AutomationId} → {outcomeDetail}");
                logger.Action(action.ActionType ?? "Unknown", action.AutomationId ?? "N/A", outcomeDetail);
                logger.Score(scoring.GetSummary());

                // 6. CHECK — should we abort?
                if (scoring.ShouldAbort())
                {
                    logger.Warning($"Score below threshold ({scoring.TotalScore}). Aborting.");
                    runArtifact.Result = "Aborted";
                    runArtifact.ErrorMessage = $"Score dropped to {scoring.TotalScore}";
                    break;
                }

                if (isLoop)
                {
                    logger.Warning($"Loop detected: {loopDetector.GetPatternSummary()}");
                    // Don't abort on first loop, but the LLM will get a warning next iteration
                }

                Thread.Sleep(500); // Small pause for UI to update
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
}
