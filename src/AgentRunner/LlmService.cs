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

        _agent = chatClient.AsAIAgent(
            instructions: @"You are an AI automation agent controlling a desktop Windows application via UI Automation.
You receive a description of visible UI elements and a goal to achieve.
You must output ONLY valid JSON. Do not wrap in markdown blocks.

JSON schema for your response:
{
  ""actionType"": ""EnterText|Click|DoubleClick|Scroll|Wait|Done|Explore"",
  ""automationId"": ""The AutomationId or Name of the target element (null for Wait/Done)"",
  ""value"": ""Text to enter, or scroll direction up/down (null if not applicable)"",
  ""reason"": ""Brief explanation of why you chose this action"",
  ""confidence"": 0-100
}

Rules:
- Use AutomationId when available, otherwise use Name.
- Use 'Explore' when you want to investigate an unfamiliar part of the UI.
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
        var loopLine = loopWarning != null
            ? "WARNING: " + loopWarning + "\nYou MUST try a DIFFERENT action than before.\n"
            : "";

        var prompt = $@"
=== GOAL ===
{goal.Description}
{successLine}

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
}
