using System;
using System.Collections.Generic;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner;

/// <summary>
/// Maps a normalized UIA event (<see cref="CapturedUiEvent"/>) to the <see cref="RecordedAction"/> a
/// test would replay. Pure and framework-agnostic (V9.5 recording mode). Returns null for events that
/// carry no usable target identifier — those can't be replayed.
/// </summary>
public static class RecordedActionMapper
{
    public static RecordedAction? Map(CapturedUiEvent ev)
    {
        if (ev == null)
            return null;
        // Need an AutomationId or Name to target the control on replay.
        if (string.IsNullOrWhiteSpace(ev.AutomationId) && string.IsNullOrWhiteSpace(ev.Name))
            return null;

        // Invoked / Toggled / SelectionChanged all replay as a click; a value change replays as text.
        var verb = ev.Kind == UiEventKind.ValueChanged ? ActionVocabulary.EnterText : ActionVocabulary.Click;

        return new RecordedAction
        {
            Verb = verb,
            Target = ev.AutomationId,
            Name = ev.Name,
            Value = ActionVocabulary.Is(verb, ActionVocabulary.EnterText) ? ev.Value : null
        };
    }
}

/// <summary>
/// Accumulates observed UIA events into a <see cref="RecordedSession"/> (V9.5 recording mode). Pure +
/// stateful: the env-bound live FlaUI source feeds it <see cref="CapturedUiEvent"/>s, then
/// <see cref="ToSession"/> hands off to <see cref="RecordingComposer"/> for the YAML draft. It smooths
/// raw event noise into intentional-looking steps: consecutive text edits on the same field collapse
/// to the final value (UIA raises one ValueChanged per keystroke), and an immediately-repeated click
/// on the same control is de-duplicated.
///
/// Caveat: the click dedup is adjacency-only, so two *intentional* consecutive clicks on the same
/// control (e.g. an increment/stepper button) collapse to one — the author re-adds the repeat in the
/// draft. A non-adjacent repeat (any other action in between) is preserved.
/// </summary>
public sealed class SessionRecorder
{
    private readonly List<RecordedAction> _actions = [];

    public string? Window { get; set; }
    public string? Framework { get; set; }
    public string? Title { get; set; }

    /// <summary>Recorded actions so far (after smoothing).</summary>
    public int Count => _actions.Count;

    public void Observe(CapturedUiEvent ev)
    {
        var action = RecordedActionMapper.Map(ev);
        if (action == null)
            return;

        var last = _actions.Count > 0 ? _actions[_actions.Count - 1] : null;
        if (last != null && SameTarget(last, action))
        {
            // Many ValueChanged on one field → keep only the final text.
            if (Is(action, ActionVocabulary.EnterText) && Is(last, ActionVocabulary.EnterText))
            {
                last.Value = action.Value;
                return;
            }
            // A doubled click event on the same control → drop the repeat.
            if (Is(action, ActionVocabulary.Click) && Is(last, ActionVocabulary.Click))
                return;
        }

        _actions.Add(action);
    }

    public RecordedSession ToSession() => new()
    {
        Window = Window,
        Framework = Framework,
        Title = Title,
        Actions = new List<RecordedAction>(_actions)
    };

    private static bool Is(RecordedAction a, string verb) => ActionVocabulary.Is(a.Verb, verb);

    private static bool SameTarget(RecordedAction a, RecordedAction b) =>
        string.Equals(a.Target, b.Target, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
}
