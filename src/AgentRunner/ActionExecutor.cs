using System;
using System.Threading.Tasks;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner;

/// <summary>
/// Outcome of executing one agent action, applied back onto the run step by the
/// orchestrator. Keeps <see cref="ActionExecutor"/> free of artifact/scoring concerns —
/// it decides <em>what happened</em>; the loop decides what to record and whether to stop.
/// </summary>
public sealed class ActionExecutionResult
{
    public bool Succeeded { get; private set; } = true;
    public string OutcomeDetail { get; private set; } = "ok";
    public string? FailureCode { get; private set; }
    public string? FailureMessage { get; private set; }

    /// <summary>The agent signaled Done and the success condition was satisfied — the loop
    /// should record success and return. (A Done with the condition unmet is a normal failure.)</summary>
    public bool DoneSucceeded { get; private set; }

    private static ActionExecutionResult Ok(string detail) => new() { OutcomeDetail = detail };

    public static readonly ActionExecutionResult Success = Ok("ok");

    public static ActionExecutionResult Failure(string outcomeDetail, string code, string message) =>
        new() { Succeeded = false, OutcomeDetail = outcomeDetail, FailureCode = code, FailureMessage = message };

    public static ActionExecutionResult Detail(string detail) => Ok(detail);

    public static readonly ActionExecutionResult Done = new() { DoneSucceeded = true, OutcomeDetail = "agent_done" };
}

/// <summary>Executes a single decided action against the automation driver.</summary>
public interface IActionExecutor
{
    Task<ActionExecutionResult> ExecuteAsync(AgentAction action, UiSnapshot snapshot, AgentGoal goal);
}

/// <summary>
/// The act stage of the loop, extracted out of <c>RunOrchestrator.RunCoreAsync</c>: validate
/// the action against the goal's allow-list and the live snapshot, then dispatch the verb to
/// the driver. The verb set comes from <see cref="ActionVocabulary"/> so it cannot drift from
/// plan/prompt/target validation. Any driver exception becomes an <c>action_failed</c> result
/// (with a redacted message) rather than propagating — identical to the previous inline catch.
/// </summary>
public sealed class ActionExecutor(
    IAutomationDriver driver,
    SecretRedactor secretRedactor,
    StructuredLogger logger,
    AgentMemory memory,
    int waitActionDelayMs) : IActionExecutor
{
    public async Task<ActionExecutionResult> ExecuteAsync(AgentAction action, UiSnapshot snapshot, AgentGoal goal)
    {
        try
        {
            // Pre-dispatch validation: allow-list, then target existence.
            if (!IsActionAllowed(goal, action.ActionType))
            {
                return ActionExecutionResult.Failure(
                    "action_not_allowed",
                    "action_not_allowed",
                    $"Action '{action.ActionType}' is not listed in allowed_actions.");
            }

            var targetValidation = AgentActionValidator.ValidateTargetExists(action, snapshot);
            if (!targetValidation.IsValid)
            {
                var code = targetValidation.Code ?? "action_target_invalid";
                logger.Warning(targetValidation.Message ?? code);
                return ActionExecutionResult.Failure(code, code, targetValidation.Message ?? code);
            }

            var id = action.AutomationId;
            var hasId = !string.IsNullOrEmpty(id);

            if (ActionVocabulary.Is(action.ActionType, ActionVocabulary.EnterText) && hasId)
            {
                driver.EnterText(id!, action.Value ?? "");
                memory.AddFact($"entered_{id}", action.Value ?? "");
                return ActionExecutionResult.Success;
            }

            if (ActionVocabulary.Is(action.ActionType, ActionVocabulary.Click) && hasId)
            {
                driver.Click(id!);
                return ActionExecutionResult.Success;
            }

            if (ActionVocabulary.Is(action.ActionType, ActionVocabulary.DoubleClick) && hasId)
            {
                driver.DoubleClick(id!);
                return ActionExecutionResult.Success;
            }

            if (ActionVocabulary.Is(action.ActionType, ActionVocabulary.Scroll) && hasId)
            {
                driver.Scroll(id!, action.Value ?? "down");
                return ActionExecutionResult.Success;
            }

            if (ActionVocabulary.Is(action.ActionType, ActionVocabulary.Assert) && hasId)
                return AssertEquals(action, id!);

            if (ActionVocabulary.Is(action.ActionType, ActionVocabulary.Wait))
            {
                await Task.Delay(waitActionDelayMs);
                return ActionExecutionResult.Success;
            }

            if (ActionVocabulary.Is(action.ActionType, ActionVocabulary.Done))
                return Done(action, snapshot, goal);

            if (ActionVocabulary.Is(action.ActionType, ActionVocabulary.Explore))
            {
                // Explore = just re-capture, no real action.
                return ActionExecutionResult.Detail("explore");
            }

            return ActionExecutionResult.Failure(
                "unsupported_action",
                "unsupported_action",
                $"Unsupported action '{action.ActionType}'.");
        }
        catch (Exception ex)
        {
            var detail = secretRedactor.RedactText(ex.Message) ?? ex.Message;
            logger.Error($"Action failed: {action.ActionType} on {action.AutomationId}: {detail}");
            return ActionExecutionResult.Failure(detail, "action_failed", detail);
        }
    }

    private ActionExecutionResult AssertEquals(AgentAction action, string id)
    {
        var actualText = driver.ReadText(id);
        if (!string.Equals(actualText, action.Value, StringComparison.OrdinalIgnoreCase))
        {
            var safeExpected = secretRedactor.RedactActionValue(action);
            var safeActual = secretRedactor.RedactValueForIdentifier(id, actualText);
            var detail = $"assertion_failed on {id}. Expected: '{safeExpected}', Actual: '{safeActual}'";
            return ActionExecutionResult.Failure(detail, "assertion_failed", detail);
        }

        var safe = secretRedactor.RedactValueForIdentifier(id, actualText);
        logger.Info($"Assertion passed on {id}: '{safe}'");
        return ActionExecutionResult.Success;
    }

    private ActionExecutionResult Done(AgentAction action, UiSnapshot snapshot, AgentGoal goal)
    {
        // Scan every status region (not just the first label) for the success condition.
        if (!string.IsNullOrEmpty(goal.SuccessCondition) &&
            !snapshot.StatusContains(goal.SuccessCondition!))
        {
            var doneStatusText = snapshot.FindStatusText();
            logger.Warning($"Agent signaled Done before success condition was visible. status=\"{doneStatusText ?? ""}\"");
            return ActionExecutionResult.Failure(
                "done_without_success_condition",
                "done_without_success_condition",
                "Agent signaled Done before the configured success condition was visible.");
        }

        logger.Info("Agent signaled Done.");
        return ActionExecutionResult.Done;
    }

    private static bool IsActionAllowed(AgentGoal goal, string? actionType)
    {
        if (goal.AllowedActions.Count == 0)
            return true;
        if (string.IsNullOrWhiteSpace(actionType))
            return false;

        foreach (var allowedAction in goal.AllowedActions)
        {
            if (ActionVocabulary.Is(actionType, allowedAction))
                return true;
        }

        return false;
    }
}
