using System;
using System.Text.Json;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner;

/// <summary>
/// Parses a raw LLM response string into an <see cref="AgentAction"/>.
/// Deterministic and key-free: strips markdown code fences, deserializes the JSON,
/// and falls back to a safe "Wait" action when the response is null, empty, or
/// invalid. Keeping this isolated lets the non-deterministic LLM call be the only
/// untestable step in the decision pipeline.
/// </summary>
public static class LlmResponseParser
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Converts a raw model response to an action. Never throws: any parse failure
    /// degrades to a "Wait" action carrying a diagnostic reason.
    /// </summary>
    public static AgentAction Parse(string? rawResponse)
    {
        var json = rawResponse?.Trim() ?? "";

        // Strip markdown code fences if present.
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
            return JsonSerializer.Deserialize<AgentAction>(json, Options)
                ?? new AgentAction { ActionType = "Wait", Reason = "Deserialized null" };
        }
        catch (Exception ex)
        {
            Console.WriteLine("Failed to parse LLM response: " + ex.Message);
            return new AgentAction { ActionType = "Wait", Reason = "Parse error: " + rawResponse };
        }
    }
}
