using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner;

/// <summary>
/// A rule-based <see cref="IActionDecider"/> that drives simple form + submit flows with
/// no LLM and no provider key — a "brain" for exercising the real agent loop + driver +
/// app in CI without OpenRouter. It is not intelligent: given a small intent (field values
/// to enter and buttons to click, in order) it decides the next action from the <em>live</em>
/// UI snapshot — filling inputs that are present and not yet entered, then clicking the next
/// enabled target in sequence, then signalling <c>Done</c>.
///
/// Because it reads the current snapshot each step (rather than replaying a fixed script),
/// it tolerates re-renders and disabled-until-enabled gating, and it is loop-safe via its
/// own progress state.
/// </summary>
public sealed class HeuristicActionDecider : IActionDecider
{
    private readonly IReadOnlyList<KeyValuePair<string, string>> _inputs;
    private readonly IReadOnlyList<string> _clickSequence;
    private readonly HashSet<string> _entered = new(StringComparer.OrdinalIgnoreCase);
    private int _clickIndex;

    /// <param name="inputs">AutomationId → value to type, applied when the field is present.</param>
    /// <param name="clickSequence">Button AutomationIds to click in order once inputs are done.</param>
    public HeuristicActionDecider(
        IEnumerable<KeyValuePair<string, string>> inputs, IEnumerable<string> clickSequence)
    {
        _inputs = inputs?.ToList() ?? [];
        _clickSequence = clickSequence?.ToList() ?? [];
    }

    public Task<AgentAction> DecideActionAsync(
        UiSnapshot snapshot, AgentGoal goal, string memoryContext, string? loopWarning = null)
    {
        // 1. Fill any configured input that is present and not yet entered.
        foreach (var input in _inputs)
        {
            if (_entered.Contains(input.Key))
                continue;
            if (FindById(snapshot, input.Key) is null)
                continue;

            _entered.Add(input.Key);
            return Result("EnterText", input.Key, input.Value, "fill field");
        }

        // 2. Click the next target in sequence once it is present and enabled.
        if (_clickIndex < _clickSequence.Count)
        {
            var target = _clickSequence[_clickIndex];
            var element = FindById(snapshot, target);
            if (element is { IsEnabled: true })
            {
                _clickIndex++;
                return Result("Click", target, null, "click target");
            }

            // Present but disabled (e.g. gated until a prior action), or not rendered yet:
            // wait for the UI to catch up rather than clicking blindly.
            return Result("Wait", null, null, $"waiting for '{target}' to become actionable");
        }

        // 3. Nothing left to do.
        return Result("Done", null, null, "heuristic flow complete");
    }

    private static UiElement? FindById(UiSnapshot snapshot, string id) =>
        snapshot.Elements.FirstOrDefault(e =>
            string.Equals(e.AutomationId, id, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(e.Name, id, StringComparison.OrdinalIgnoreCase));

    private static Task<AgentAction> Result(string type, string? target, string? value, string reason) =>
        Task.FromResult(new AgentAction
        {
            ActionType = type,
            AutomationId = target,
            Value = value,
            Reason = reason,
            Confidence = 100
        });
}
