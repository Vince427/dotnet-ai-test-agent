using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner;

/// <summary>
/// A key-free, "agent-in-the-loop" vision decider (the vision counterpart of <see cref="BridgeLlmServer"/>).
/// On every step it captures the screen, masks secret regions, draws the numbered overlay, and writes the
/// annotated PNG + the identifiers-only index to a shared folder, then waits for a reply file with the box
/// choice. An external multimodal agent — e.g. Claude Code reading the PNG on your desktop — plays the VLM
/// with NO provider API key: it reads <c>vision-req-&lt;n&gt;.png</c> + <c>vision-req-&lt;n&gt;.json</c> and
/// writes <c>vision-resp-&lt;n&gt;.json</c> ({box, actionType, value, reason, confidence}). On timeout it
/// returns a safe <c>Wait</c> so a run never hangs. Reuses the exact overlay/masking/parse path the Tier-2
/// <see cref="VisionActionDecider"/> uses, so what the agent sees is what the real VLM would see.
/// </summary>
public sealed class BridgeVisionDecider : IActionDecider
{
    private readonly string _ioDir;
    private readonly Func<byte[]> _captureScreenshot;
    private readonly SecretRedactor _redactor;
    private readonly int _timeoutMs;
    private readonly Action<string>? _log;
    private readonly object _gate = new();
    private int _count;

    public BridgeVisionDecider(
        string ioDir,
        Func<byte[]> captureScreenshot,
        SecretRedactor? redactor = null,
        int timeoutMs = 120_000,
        Action<string>? log = null)
    {
        _ioDir = Path.GetFullPath(ioDir);
        Directory.CreateDirectory(_ioDir);
        _captureScreenshot = captureScreenshot ?? throw new ArgumentNullException(nameof(captureScreenshot));
        _redactor = redactor ?? new SecretRedactor();
        _timeoutMs = timeoutMs;
        _log = log;
    }

    public async Task<AgentAction> DecideActionAsync(
        UiSnapshot snapshot, AgentGoal goal, string memoryContext, string? loopWarning = null)
    {
        // The overlay numbers every actionable, locatable element; without any, there's nothing to point at.
        var index = ScreenshotOverlay.BuildIndex(snapshot);
        if (index.Count == 0)
            return new AgentAction { ActionType = "Wait", Reason = "No actionable elements to show the vision agent." };

        byte[] png;
        try { png = _captureScreenshot(); }
        catch (Exception ex) { return new AgentAction { ActionType = "Wait", Reason = "Screenshot failed: " + ex.Message }; }

        // Mask secret regions BEFORE the image is written, then annotate (same order as run artifacts).
        // The index is identifiers-only, so it's already secret-safe — no values ever leave the machine.
        var secretRegions = ScreenshotRedaction.SecretRegions(snapshot, _redactor);
        if (secretRegions.Count > 0)
            png = UIAutomation.ScreenshotMasker.MaskRegions(png, secretRegions);
        var annotated = UIAutomation.ScreenshotAnnotator.Annotate(png, ScreenshotOverlay.ToBoxes(index));

        int n;
        lock (_gate) { n = ++_count; }

        var prompt = BuildGoalPrompt(goal, memoryContext, loopWarning);
        var requestJson = JsonSerializer.Serialize(
            new { box = (int?)null, prompt, index }, ApiJson);

        var pngPath = Path.Combine(_ioDir, $"vision-req-{n}.png");
        var reqPath = Path.Combine(_ioDir, $"vision-req-{n}.json");
        File.WriteAllBytes(pngPath, annotated);
        File.WriteAllText(reqPath, requestJson);
        _log?.Invoke($"[vision-bridge] step {n}: wrote {Path.GetFileName(pngPath)} + index — awaiting vision-resp-{n}.json …");

        var raw = await WaitForReplyAsync(n);
        if (raw == null)
            return new AgentAction { ActionType = "Wait", Reason = "Vision bridge timeout — no vision-resp received." };

        return VisionResponseParser.Parse(raw, index);
    }

    private async Task<string?> WaitForReplyAsync(int n)
    {
        var deadline = Environment.TickCount + _timeoutMs;
        var path = Path.Combine(_ioDir, $"vision-resp-{n}.json");
        while (Environment.TickCount < deadline)
        {
            if (File.Exists(path))
            {
                try
                {
                    var content = File.ReadAllText(path).Trim();
                    if (content.Length > 0)
                        return content;
                }
                catch { /* still being written; retry */ }
            }
            await Task.Delay(250);
        }
        return null;
    }

    private string BuildGoalPrompt(AgentGoal goal, string memoryContext, string? loopWarning)
    {
        // Redact to "" (never the raw original) so redaction intent can't be defeated via the prompt.
        var goalText = _redactor.RedactText(goal.Description) ?? "";
        var mem = _redactor.RedactText(memoryContext) ?? "";
        // Redact the success condition too (it can contain a secret) — same defense as PromptBuilder.
        var scRedacted = _redactor.RedactText(goal.SuccessCondition) ?? "";
        var sc = string.IsNullOrEmpty(goal.SuccessCondition) ? "" : $"\nSuccess condition: {scRedacted}";
        var warn = string.IsNullOrEmpty(loopWarning) ? "" : $"\nWARNING: {loopWarning} — try a different box/action.";
        return
            "You control a Windows desktop app via a screenshot with NUMBERED boxes over the actionable elements.\n" +
            "Each number maps to an element in the index (box -> automationId/name/controlType).\n" +
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
