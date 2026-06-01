using System;
using System.ClientModel;
using System.Threading.Tasks;
using DesktopAiTestAgent.Core;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.OpenAI;
using OpenAI;
using OpenAI.Chat;

namespace DesktopAiTestAgent.AgentRunner;

/// <summary>
/// LLM service that asks the agent for the next action given UI state and goals.
/// Supports any desktop app, not just the login form.
///
/// The deterministic parts of the pipeline are extracted so they can be unit-tested
/// without an LLM key:
///   - prompt assembly + secret redaction -> <see cref="PromptBuilder"/>
///   - response fence-stripping, JSON parsing, and safe fallback -> <see cref="LlmResponseParser"/>
/// The only non-deterministic step is the network call <c>_agent.RunAsync</c>.
/// </summary>
public class LlmService
{
    private readonly AIAgent _agent;
    private readonly PromptBuilder _promptBuilder;

    public LlmService(WorkflowConfig config, SecretRedactor? redactor = null)
    {
        var effectiveRedactor = redactor ?? new SecretRedactor();
        _promptBuilder = new PromptBuilder(effectiveRedactor, config.PromptTemplate);

        // Enforce configuration: fail fast if keys are missing.
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
    /// Builds a prompt from the current UI state, goal, and agent context, asks the
    /// LLM, and parses the response into an action.
    /// </summary>
    public async Task<AgentAction> DecideActionAsync(
        UiSnapshot snapshot,
        AgentGoal goal,
        string memoryContext,
        string? loopWarning = null)
    {
        var prompt = _promptBuilder.Build(snapshot, goal, memoryContext, loopWarning);
        var response = await _agent.RunAsync(prompt);
        return LlmResponseParser.Parse(response?.ToString());
    }
}
