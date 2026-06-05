using System;
using System.Collections.Generic;
using System.Linq;
using DesktopAiTestAgent.AgentRunner.Dashboard;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner;

/// <summary>One observed interaction in a recorded manual session (V9.5 recording mode).</summary>
public sealed class RecordedAction
{
    /// <summary>An <see cref="ActionVocabulary"/> verb: EnterText | Click | DoubleClick | Scroll | Wait | Assert | …</summary>
    public string Verb { get; set; } = "";
    /// <summary>The control's AutomationId, when known.</summary>
    public string? Target { get; set; }
    /// <summary>The control's human-readable Name/label, when known.</summary>
    public string? Name { get; set; }
    /// <summary>Entered text (EnterText) or direction (Scroll). Redacted if the target looks secret.</summary>
    public string? Value { get; set; }
}

/// <summary>
/// A captured manual session: the window driven plus the ordered interactions. This is the portable
/// hand-off artifact between the (env-bound) live capture step and the pure, testable composition into
/// a YAML draft — so the draft can be produced and validated without a desktop or LLM.
/// </summary>
public sealed class RecordedSession
{
    public string? Window { get; set; }
    public string? Framework { get; set; }
    public string? Title { get; set; }
    public List<RecordedAction> Actions { get; set; } = [];
}

/// <summary>Outcome of composing a session into a YAML test draft: the YAML plus validator feedback.</summary>
public sealed class RecordingComposeResult
{
    public string TestId { get; set; } = "";
    public string Yaml { get; set; } = "";
    public List<string> Errors { get; } = [];
    public List<string> Warnings { get; } = [];
    public bool IsValid => Errors.Count == 0;
}

/// <summary>
/// V9.5 recording mode (increment 1): turns a <see cref="RecordedSession"/> into a first YAML test
/// draft — the biggest top-of-funnel authoring lever (record once, edit the draft, run it). Pure and
/// key-free: it reuses the dashboard's YAML emitter (<see cref="DashboardApi.BuildYaml"/>) so there is
/// one source of truth for the test shape, then re-parses + validates with <see cref="TestPlanValidator"/>.
/// The draft stays goal-based (the existing schema): the goal is synthesised in plain language from the
/// recorded steps (secret values redacted), allowed_actions are the distinct verbs used, and the window
/// /framework carry over. A human edits the goal + adds a success condition before committing.
/// A <see cref="SessionRecorder"/> (increment 2) produces the <see cref="RecordedSession"/> from live
/// UIA events; this composer is also driven directly from a hand-written/JSON session.
/// </summary>
public static class RecordingComposer
{
    public static RecordingComposeResult Compose(
        RecordedSession session, string? idHint = null, SecretRedactor? redactor = null)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));
        var r = redactor ?? new SecretRedactor();

        var id = MakeId(idHint ?? session.Title ?? session.Window);

        // allowed_actions = the distinct known verbs actually used, in first-seen order; always allow
        // Done so the agent can declare success.
        var verbs = new List<string>();
        foreach (var a in session.Actions)
        {
            if (ActionVocabulary.IsKnown(a.Verb) &&
                !verbs.Any(v => ActionVocabulary.Is(v, a.Verb)))
                verbs.Add(Canonical(a.Verb));
        }
        if (!verbs.Any(v => ActionVocabulary.Is(v, ActionVocabulary.Done)))
            verbs.Add(ActionVocabulary.Done);

        var req = new DashboardApi.CreateTestRequest
        {
            Id = id,
            Suite = "recorded",
            Title = session.Title ??
                    (string.IsNullOrWhiteSpace(session.Window) ? "Recorded flow" : $"Recorded flow — {session.Window}"),
            Framework = session.Framework,
            TargetWindow = session.Window,
            Category = "Scenario",
            AuthoringAgent = "recorder",
            Goal = ComposeGoal(session, r),
            MaxSteps = Math.Max(8, session.Actions.Count + 3),
            AllowedActions = verbs
        };

        var yaml = DashboardApi.BuildYaml(req);
        var result = new RecordingComposeResult { TestId = id, Yaml = yaml };

        try
        {
            var plan = TestPlanLoader.Parse(yaml, id);
            var validation = TestPlanValidator.Validate(plan, id);
            result.Errors.AddRange(validation.Errors.Select(Friendly));
            result.Warnings.AddRange(validation.Warnings.Select(Friendly));
        }
        catch (Exception ex)
        {
            result.Errors.Add("Generated YAML is invalid: " + ex.Message);
        }

        return result;
    }

    /// <summary>Synthesise a plain-language goal from the recorded steps; secret values are redacted.</summary>
    private static string ComposeGoal(RecordedSession session, SecretRedactor r)
    {
        if (session.Actions.Count == 0)
            return "Recorded flow (no actions captured). Edit this goal to describe the intent and set a success condition.";

        var parts = new List<string>();
        foreach (var a in session.Actions)
        {
            var label = !string.IsNullOrWhiteSpace(a.Name) ? $"\"{a.Name}\""
                      : !string.IsNullOrWhiteSpace(a.Target) ? $"'{a.Target}'"
                      : "a control";

            if (ActionVocabulary.Is(a.Verb, ActionVocabulary.EnterText))
            {
                var val = r.RedactValueForIdentifier(a.Target ?? a.Name, a.Value) ?? "";
                parts.Add($"enter \"{val}\" into {label}");
            }
            else if (ActionVocabulary.Is(a.Verb, ActionVocabulary.Click)) parts.Add($"click {label}");
            else if (ActionVocabulary.Is(a.Verb, ActionVocabulary.DoubleClick)) parts.Add($"double-click {label}");
            else if (ActionVocabulary.Is(a.Verb, ActionVocabulary.Scroll)) parts.Add($"scroll {label} {a.Value}".TrimEnd());
            else if (ActionVocabulary.Is(a.Verb, ActionVocabulary.Assert)) parts.Add($"verify {label}");
            else if (ActionVocabulary.Is(a.Verb, ActionVocabulary.Wait)) parts.Add("wait for the UI to settle");
            else parts.Add($"{a.Verb} {label}");
        }

        return $"Recorded flow: {string.Join("; ", parts)}. " +
               "(Draft — edit this goal to describe the intent and set a success_condition.)";
    }

    /// <summary>Map a recorded verb to its canonical <see cref="ActionVocabulary"/> casing.</summary>
    private static string Canonical(string verb) =>
        ActionVocabulary.All.FirstOrDefault(v => ActionVocabulary.Is(v, verb)) ?? verb;

    /// <summary>Derive a safe UPPER-DASHED test id from a hint; always suffixed so it reads as a draft.</summary>
    private static string MakeId(string? hint)
    {
        var basis = string.IsNullOrWhiteSpace(hint) ? "RECORDED" : hint!;
        var chars = basis.Select(c => char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : '-').ToArray();
        var slug = new string(chars);
        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        slug = slug.Trim('-');
        if (slug.Length > 40) slug = slug[..40].Trim('-');
        if (string.IsNullOrEmpty(slug)) slug = "RECORDED";
        return slug + "-001";
    }

    /// <summary>Strip the "{source}:{id}: " location prefix a validator message carries, for display.</summary>
    private static string Friendly(string message)
    {
        var i = message.IndexOf(": ", StringComparison.Ordinal);
        return i >= 0 ? message[(i + 2)..] : message;
    }
}
