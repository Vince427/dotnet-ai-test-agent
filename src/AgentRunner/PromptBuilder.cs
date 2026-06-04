using System;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner;

/// <summary>
/// Builds the deterministic LLM prompt from UI state, goal, and agent memory.
/// This is pure and side-effect free: no network calls, so it is fully
/// unit-testable without an LLM key. The only non-deterministic step in the
/// decision pipeline is the network call in <see cref="LlmService"/>.
/// </summary>
public sealed class PromptBuilder
{
    private readonly SecretRedactor _redactor;
    private readonly string? _promptTemplate;

    public PromptBuilder(SecretRedactor redactor, string? promptTemplate = null)
    {
        _redactor = redactor;
        _promptTemplate = promptTemplate;
    }

    /// <summary>
    /// Assembles the full prompt sent to the agent. Secret-like values in the goal,
    /// memory context, and UI snapshot are redacted before they enter the prompt.
    /// </summary>
    public string Build(
        UiSnapshot snapshot,
        AgentGoal goal,
        string memoryContext,
        string? loopWarning = null)
    {
        var safeGoalDescription = _redactor.RedactText(goal.Description) ?? goal.Description;
        var safeSuccessCondition = _redactor.RedactText(goal.SuccessCondition) ?? goal.SuccessCondition;
        var safeWorkflowPolicy = _redactor.RedactText(BuildWorkflowPolicy(goal)) ?? "";
        var safeMemoryContext = _redactor.RedactText(memoryContext) ?? memoryContext;
        var successLine = safeSuccessCondition != null
            ? "Success condition: UI shows \"" + safeSuccessCondition + "\""
            : "";
        var allowedActionsLine = goal.AllowedActions.Count > 0
            ? "Allowed actions for this test: " + string.Join(", ", goal.AllowedActions)
            : "Allowed actions: " + ActionVocabulary.DefaultAllowedList;
        var loopLine = loopWarning != null
            ? "WARNING: " + loopWarning + "\nYou MUST try a DIFFERENT action than before.\n"
            : "";

        var categoryLine = goal.Category switch
        {
            TestCategory.Monkey => "CATEGORY: Monkey Testing. Your goal is to click random interactive elements, enter random texts, and try to break the app. Do NOT follow a logical path.",
            TestCategory.Audit => "CATEGORY: Accessibility Audit. Your goal is to identify interactive elements (buttons, inputs) that are missing an 'AutomationId' or 'Name' property.",
            TestCategory.Smoke => "CATEGORY: Smoke Test. Your goal is to perform a basic happy-path navigation to ensure the app doesn't crash.",
            _ => $"CATEGORY: {goal.Category}. Follow the goal description strictly."
        };

        return $@"
=== WORKFLOW POLICY ===
{safeWorkflowPolicy}

=== GOAL ===
{safeGoalDescription}
{categoryLine}
{successLine}
{allowedActionsLine}

=== CURRENT UI STATE ===
{_redactor.RedactSnapshotForPrompt(snapshot)}

=== AGENT CONTEXT ===
{safeMemoryContext}

{loopLine}

What is your next action? Output only JSON.";
    }

    private string BuildWorkflowPolicy(AgentGoal goal)
    {
        if (string.IsNullOrWhiteSpace(_promptTemplate))
            return goal.Description;

        var rendered = _promptTemplate!;
        rendered = ReplaceSuccessConditionBlock(rendered, goal);
        rendered = RemoveAttemptBlock(rendered);
        rendered = rendered.Replace("{{ goal.description }}", goal.Description);
        rendered = rendered.Replace("{{ goal.success_condition }}", goal.SuccessCondition ?? "");
        rendered = rendered.Replace("{% endif %}", "");
        return rendered.Trim();
    }

    private static string ReplaceSuccessConditionBlock(string template, AgentGoal goal)
    {
        const string startToken = "{% if goal.success_condition %}";
        const string endToken = "{% endif %}";

        var startIndex = template.IndexOf(startToken, StringComparison.Ordinal);
        if (startIndex < 0)
            return template;

        var endIndex = template.IndexOf(endToken, startIndex + startToken.Length, StringComparison.Ordinal);
        if (endIndex < 0)
            return template;

        var before = template[..startIndex];
        var block = template[(startIndex + startToken.Length)..endIndex];
        var after = template[(endIndex + endToken.Length)..];

        return string.IsNullOrEmpty(goal.SuccessCondition)
            ? before + after
            : before + block + after;
    }

    private static string RemoveAttemptBlock(string template)
    {
        const string startToken = "{% if attempt %}";
        const string endToken = "{% endif %}";

        var startIndex = template.IndexOf(startToken, StringComparison.Ordinal);
        if (startIndex < 0)
            return template;

        var endIndex = template.IndexOf(endToken, startIndex + startToken.Length, StringComparison.Ordinal);
        if (endIndex < 0)
            return template;

        var before = template[..startIndex];
        var after = template[(endIndex + endToken.Length)..];
        return before + after;
    }
}
