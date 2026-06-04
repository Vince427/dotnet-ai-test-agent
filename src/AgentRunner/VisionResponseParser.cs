using System;
using System.Collections.Generic;
using System.Text.Json;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner;

/// <summary>
/// Parses a VLM's overlay response into an <see cref="AgentAction"/> (V3 Tier-2). The model is
/// asked to pick a numbered box from the overlay index plus an action; this maps the chosen box
/// back to its element identifier. Deterministic and key-free — the only non-deterministic step
/// is the VLM call itself (<see cref="IVisionClient"/>), mirroring how <see cref="LlmResponseParser"/>
/// isolates the text path. Never throws: any bad/empty/out-of-range response degrades to a safe
/// <c>Wait</c>.
/// </summary>
internal static class VisionResponseParser
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static AgentAction Parse(string? rawResponse, IReadOnlyList<OverlayBox> index)
    {
        var json = StripFences(rawResponse);

        VisionResponse? vr;
        try { vr = JsonSerializer.Deserialize<VisionResponse>(json, Options); }
        catch (Exception ex)
        {
            Console.WriteLine("Failed to parse vision response: " + ex.Message);
            return Wait("Vision parse error: " + rawResponse);
        }
        if (vr == null)
            return Wait("Vision response deserialized null");

        var action = new AgentAction
        {
            ActionType = vr.ActionType,
            Value = vr.Value,
            Reason = vr.Reason,
            Confidence = vr.Confidence
        };

        // Map the chosen overlay box number back to its element's identifier. Box-less actions
        // (Wait/Done/Explore) are allowed; a box that isn't in the index is a hallucination.
        if (vr.Box is int n && n > 0)
        {
            OverlayBox? match = null;
            foreach (var b in index)
                if (b.N == n) { match = b; break; }

            if (match == null)
                return Wait($"Vision chose box {n}, which is not in the overlay index");

            action.AutomationId = string.IsNullOrEmpty(match.AutomationId) ? match.Name : match.AutomationId;
        }

        if (string.IsNullOrWhiteSpace(action.ActionType))
            return Wait("Vision response had no actionType");

        return action;
    }

    private static string StripFences(string? raw)
    {
        var json = raw?.Trim() ?? "";
        if (json.StartsWith("```json")) json = json[7..];
        else if (json.StartsWith("```")) json = json[3..];
        if (json.EndsWith("```")) json = json[..^3];
        return json.Trim();
    }

    private static AgentAction Wait(string reason) => new() { ActionType = "Wait", Reason = reason };

    private sealed class VisionResponse
    {
        public int? Box { get; set; }
        public string? ActionType { get; set; }
        public string? Value { get; set; }
        public string? Reason { get; set; }
        public int? Confidence { get; set; }
    }
}
