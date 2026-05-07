using System;
using System.ClientModel;
using System.Text.Json;
using System.Threading.Tasks;
using DesktopAiTestAgent.Core;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.OpenAI;
using OpenAI;
using OpenAI.Chat;

namespace DesktopAiTestAgent.AgentRunner;

/// <summary>
/// LLM service that builds generic prompts from UI state + goals.
/// Supports any WinForms app, not just the login form.
/// </summary>
public class LlmService
{
    private readonly AIAgent _agent;
    private readonly string? _promptTemplate;

    public LlmService(WorkflowConfig config)
    {
        // Enforce configuration: fail fast if keys are missing
        var proxyEndpointUrl = config.LlmEndpoint ?? throw new InvalidOperationException("LLM_ENDPOINT is not configured. Check your WORKFLOW.md or .env file.");
        var apiKey = config.LlmApiKey ?? throw new InvalidOperationException("LLM_API_KEY is not configured. Check your WORKFLOW.md or .env file.");
        var modelName = config.LlmModel ?? "gpt-4o-mini"; 

        var proxyEndpoint = new Uri(proxyEndpointUrl);
        var openAiClientOptions = new OpenAIClientOptions { Endpoint = proxyEndpoint };
        var openAiClient = new OpenAIClient(new ApiKeyCredential(apiKey), openAiClientOptions);
        var chatClient = openAiClient.GetChatClient(modelName);
        _promptTemplate = config.PromptTemplate;

        _agent = chatClient.AsAIAgent(
            instructions: @"You are an AI automation agent controlling a desktop Windows application via UI Automation.
You receive a description of visible UI elements and a goal to achieve.
You must output ONLY valid JSON. Do not wrap in markdown blocks.

JSON schema for your response:
{
  ""actionType"": ""EnterText|Click|DoubleClick|Scroll|Wait|Assert|Done|Explore"",
  ""automationId"": ""The AutomationId or Name of the target element (null for Wait/Done)"",
  ""value"": ""Text to enter, or scroll direction up/down (null if not applicable)"",
  ""reason"": ""Brief explanation of why you chose this action"",
  ""confidence"": 0-100
}

Rules:
- Use AutomationId when available, otherwise use Name.
- Use 'Explore' when you want to investigate an unfamiliar part of the UI.
- Use 'Assert' to check if an element's text matches 'value'. If it matches, the goal continues.
- Use 'Done' only when you believe the goal has been achieved.
- If you detect you are stuck or looping, try a different approach.
- Always provide a confidence score (0=uncertain, 100=certain).",
            name: "DesktopAgent"
        );
    }

    /// <summary>
    /// Builds a prompt from the current UI state, goal, and agent context, then asks the LLM.
    /// </summary>
    public async Task<AgentAction> DecideActionAsync(
        UiSnapshot snapshot,
        AgentGoal goal,
        string memoryContext,
        string? loopWarning = null)
    {
        var successLine = goal.SuccessCondition != null
            ? "Success condition: UI shows \"" + goal.SuccessCondition + "\""
            : "";
        var allowedActionsLine = goal.AllowedActions.Count > 0
            ? "Allowed actions for this test: " + string.Join(", ", goal.AllowedActions)
            : "Allowed actions: EnterText, Click, DoubleClick, Scroll, Wait, Assert, Done, Explore";
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

        var workflowPolicy = BuildWorkflowPolicy(goal);

        var prompt = $@"
=== WORKFLOW POLICY ===
{workflowPolicy}

=== GOAL ===
{goal.Description}
{categoryLine}
{successLine}
{allowedActionsLine}

=== CURRENT UI STATE ===
{snapshot.ToPromptText()}

=== AGENT CONTEXT ===
{memoryContext}

{loopLine}

What is your next action? Output only JSON.";

        var response = await _agent.RunAsync(prompt);
        var json = response?.ToString()?.Trim() ?? "";

        // Strip markdown code fences if present
        if (json.StartsWith("```json"))
        {
            json = json[7..];
            if (json.EndsWith("```"))
                json = json[..^3];
        }
        else if (json.StartsWith("```"))
        {
            json = json[3..];
            if (json.EndsWith("```"))
                json = json[..^3];
        }

        json = json.Trim();

        try
        {
            return JsonSerializer.Deserialize<AgentAction>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new AgentAction { ActionType = "Wait", Reason = "Deserialized null" };
        }
        catch (Exception ex)
        {
            Console.WriteLine("Failed to parse LLM response: " + ex.Message);
            return new AgentAction { ActionType = "Wait", Reason = "Parse error: " + response };
        }
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
