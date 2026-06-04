using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Threading.Tasks;
using OpenAI;
using OpenAI.Chat;

namespace DesktopAiTestAgent.AgentRunner;

/// <summary>
/// Real <see cref="IVisionClient"/>: sends the annotated screenshot + the overlay index to an
/// OpenAI-compatible multimodal endpoint (same `LLM_ENDPOINT`/`LLM_API_KEY` as <see cref="LlmService"/>,
/// with an optional vision-capable `VISION_MODEL`). This is the non-deterministic edge of the
/// Tier-2 path — like <see cref="LlmService"/>'s network call, it has no unit test; the
/// deterministic box→action mapping (<c>VisionResponseParser</c>) and the escalation policy
/// (<c>VisionActionDecider</c>) are tested with a scripted client.
/// </summary>
public sealed class OpenAiVisionClient : IVisionClient
{
    private const string SystemPrompt =
        "You are a desktop UI automation agent. You are given a screenshot with NUMBERED boxes " +
        "drawn over the actionable elements, plus a JSON index mapping each box number to its " +
        "element. Choose ONE box and ONE action to advance the goal. Reply with ONLY a JSON " +
        "object, no markdown: {\"box\": <number or null>, \"actionType\": " +
        "\"EnterText|Click|DoubleClick|Scroll|Wait|Assert|Done|Explore\", \"value\": <text|direction|null>, " +
        "\"reason\": \"...\", \"confidence\": 0-100}.";

    private readonly ChatClient _chat;

    public OpenAiVisionClient(WorkflowConfig config)
    {
        var endpoint = config.LlmEndpoint ?? throw new InvalidOperationException("LLM_ENDPOINT is not configured.");
        var apiKey = config.LlmApiKey ?? throw new InvalidOperationException("LLM_API_KEY is not configured.");
        // A vision-capable model is required; fall back to the text model if the user pointed it at one.
        var model = config.VisionModel ?? config.LlmModel ?? "gpt-4o-mini";

        var options = new OpenAIClientOptions { Endpoint = new Uri(endpoint) };
        var client = new OpenAIClient(new ApiKeyCredential(apiKey), options);
        _chat = client.GetChatClient(model);
    }

    public async Task<string> AskAsync(byte[] annotatedPng, string overlayIndexJson, string goalPrompt)
    {
        var userText = goalPrompt + "\n\nOverlay index (box -> element):\n" + overlayIndexJson;

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(SystemPrompt),
            new UserChatMessage(
                ChatMessageContentPart.CreateTextPart(userText),
                ChatMessageContentPart.CreateImagePart(BinaryData.FromBytes(annotatedPng), "image/png"))
        };

        ClientResult<ChatCompletion> result = await _chat.CompleteChatAsync(messages);
        var content = result.Value.Content;
        return content.Count > 0 ? content[0].Text : "";
    }
}
