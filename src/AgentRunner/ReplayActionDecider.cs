using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner;

/// <summary>
/// Deterministic, key-free decider that **replays** a recorded action sequence (V9.5: the steps a
/// <c>RecordedSession</c> / <c>--record</c> captured) instead of asking an LLM. Each loop step it
/// emits the next recorded action verbatim (verb + target + value); when the script is exhausted it
/// returns <c>Done</c>. It does NOT skip a drifted target — it replays the action as recorded so the
/// loop's executor fails it visibly, which makes <c>SelectorHealer</c> record a drift suggestion that
/// <c>--heal-apply</c> can then apply. This is the "replay" half of record -> replay -> heal: no model,
/// no `.env`, fully reproducible. Ignores the live snapshot/goal/memory (the script is the plan).
/// </summary>
public sealed class ReplayActionDecider : IActionDecider
{
    private readonly IReadOnlyList<RecordedAction> _actions;
    private int _next;

    public ReplayActionDecider(IReadOnlyList<RecordedAction> actions)
    {
        _actions = actions ?? throw new ArgumentNullException(nameof(actions));
    }

    public Task<AgentAction> DecideActionAsync(
        UiSnapshot snapshot, AgentGoal goal, string memoryContext, string? loopWarning = null)
    {
        var i = Interlocked.Increment(ref _next) - 1;
        if (i >= _actions.Count)
            return Task.FromResult(new AgentAction
            {
                ActionType = ActionVocabulary.Done,
                Reason = "Replay complete — all recorded steps were issued.",
                Confidence = 100,
            });

        var step = _actions[i];
        return Task.FromResult(new AgentAction
        {
            ActionType = string.IsNullOrWhiteSpace(step.Verb) ? ActionVocabulary.Wait : step.Verb,
            AutomationId = step.Target,
            Value = step.Value,
            Reason = $"Replay step {i + 1}/{_actions.Count}" + (string.IsNullOrWhiteSpace(step.Name) ? "" : $" ({step.Name})"),
            Confidence = 100,
        });
    }
}
