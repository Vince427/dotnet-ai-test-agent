using System;
using System.Text.Json;
using System.Threading.Tasks;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner;

/// <summary>
/// The "decide" call to a vision-language model. Given the annotated screenshot (numbered
/// boxes) and the overlay index (box → identifiers), the model picks a box + action. This is
/// the only non-deterministic step of the Tier-2 path; tests inject a scripted client, and a
/// real OpenAI-compatible multimodal client implements it for production (increment 2b).
/// </summary>
public interface IVisionClient
{
    Task<string> AskAsync(byte[] annotatedPng, string overlayIndexJson, string goalPrompt);
}

/// <summary>
/// V3 Tier-2 vision fallback. Wraps a Tier-1 decider (semantic UIA resolution) and only escalates
/// to a VLM when Tier-1 produces an action whose target cannot be resolved against the live
/// snapshot — exactly the flat/owner-drawn-UI case where UIA alone fails. On escalation it
/// captures the screen, masks secret regions, draws the numbered overlay, sends it plus the
/// identifiers-only index to the VLM, and maps the chosen box back to an element. Vision stays a
/// fallback (cost/latency), never the default.
/// </summary>
public sealed class VisionActionDecider(
    IActionDecider inner,
    IVisionClient vision,
    Func<byte[]> captureScreenshot,
    SecretRedactor? redactor = null) : IActionDecider
{
    private readonly SecretRedactor _redactor = redactor ?? new SecretRedactor();

    public async Task<AgentAction> DecideActionAsync(
        UiSnapshot snapshot, AgentGoal goal, string memoryContext, string? loopWarning = null)
    {
        var action = await inner.DecideActionAsync(snapshot, goal, memoryContext, loopWarning);

        // Tier-1 resolved (target exists, or the action needs none) → use it. No VLM cost.
        if (AgentActionValidator.ValidateTargetExists(action, snapshot).IsValid)
            return action;

        // Tier-1 is ambiguous. Build the overlay; without a window origin we can't map boxes to
        // pixels, so keep the Tier-1 result rather than guess.
        var index = ScreenshotOverlay.BuildIndex(snapshot);
        if (index.Count == 0)
            return action;

        byte[] png;
        try { png = captureScreenshot(); }
        catch { return action; } // can't see the screen → fall back to Tier-1

        // Mask secret regions before the image leaves the machine, then annotate (same order the
        // run uses for artifacts). The index carries identifiers only, so it is already secret-safe.
        var secretRegions = ScreenshotRedaction.SecretRegions(snapshot, _redactor);
        if (secretRegions.Count > 0)
            png = UIAutomation.ScreenshotMasker.MaskRegions(png, secretRegions);
        var annotated = UIAutomation.ScreenshotAnnotator.Annotate(png, ScreenshotOverlay.ToBoxes(index));

        var indexJson = JsonSerializer.Serialize(index, ApiJson);
        var prompt = BuildGoalPrompt(goal, memoryContext, loopWarning);

        string raw;
        try { raw = await vision.AskAsync(annotated, indexJson, prompt); }
        catch (Exception ex) { return new AgentAction { ActionType = "Wait", Reason = "Vision call failed: " + ex.Message }; }

        return VisionResponseParser.Parse(raw, index);
    }

    private string BuildGoalPrompt(AgentGoal goal, string memoryContext, string? loopWarning)
    {
        // Fall back to "" (never the unredacted original) so redaction intent can't be defeated.
        var goalText = _redactor.RedactText(goal.Description) ?? "";
        var mem = _redactor.RedactText(memoryContext) ?? "";
        var sc = string.IsNullOrEmpty(goal.SuccessCondition) ? "" : $"\nSuccess condition: {goal.SuccessCondition}";
        var warn = string.IsNullOrEmpty(loopWarning) ? "" : $"\nWARNING: {loopWarning} — try a different box/action.";
        return
            "You control a Windows desktop app via a screenshot with NUMBERED boxes over the actionable elements.\n" +
            "Each number maps to an element in the provided index (box -> automationId/name/controlType).\n" +
            $"Goal: {goalText}{sc}{warn}\n" +
            "Recent context: " + mem + "\n" +
            "Reply ONLY with JSON: {\"box\": <number or null>, \"actionType\": \"EnterText|Click|DoubleClick|Scroll|Wait|Assert|Done|Explore\", \"value\": <text|direction|null>, \"reason\": \"...\", \"confidence\": 0-100}.";
    }

    private static readonly JsonSerializerOptions ApiJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };
}
