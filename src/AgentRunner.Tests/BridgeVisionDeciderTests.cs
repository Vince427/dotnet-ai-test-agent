using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DesktopAiTestAgent.AgentRunner;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner.Tests;

/// <summary>
/// V3 vision bridge (key-free, agent-in-the-loop): the decider writes an annotated screenshot + the
/// identifiers-only index to a folder and waits for the external agent's box-choice reply. Pure file
/// protocol — no desktop, no API key.
/// </summary>
public sealed class BridgeVisionDeciderTests : IDisposable
{
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "vbridge-" + Guid.NewGuid().ToString("N"));

    public BridgeVisionDeciderTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, recursive: true); } catch { } }

    private static UiSnapshot BoxedSnapshot() => new("App", new List<UiElement>
    {
        new() { AutomationId = "realBtn", ControlType = "Button", BoundingBox = "130,97,80,30" }
    }, windowBounds: "100,50,760,820");

    private static byte[] WhitePng(int w, int h)
    {
        using var bmp = new System.Drawing.Bitmap(w, h);
        using (var g = System.Drawing.Graphics.FromImage(bmp)) g.Clear(System.Drawing.Color.White);
        using var ms = new MemoryStream();
        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        return ms.ToArray();
    }

    [Fact]
    public async Task Decide_WritesAnnotatedRequest_AndMapsTheReplyBoxToTheElement()
    {
        // Pre-place the agent's reply (step counter starts at 1) so the wait resolves immediately.
        File.WriteAllText(Path.Combine(_dir, "vision-resp-1.json"),
            """{"box":1,"actionType":"Click","reason":"the visible button"}""");

        var decider = new BridgeVisionDecider(_dir, () => WhitePng(900, 900), timeoutMs: 5000);
        var action = await decider.DecideActionAsync(BoxedSnapshot(), new AgentGoal { Description = "click it" }, "");

        // The box choice mapped back to the real element.
        Assert.Equal("Click", action.ActionType);
        Assert.Equal("realBtn", action.AutomationId);

        // The request artifacts were written for the agent to read.
        Assert.True(File.Exists(Path.Combine(_dir, "vision-req-1.png")));
        var reqJson = File.ReadAllText(Path.Combine(_dir, "vision-req-1.json"));
        Assert.Contains("prompt", reqJson);
        Assert.Contains("realBtn", reqJson);   // the identifiers-only index
        Assert.Contains("click it", reqJson);  // the goal in the prompt
    }

    [Fact]
    public async Task Decide_MasksSecretsInThePng_AndKeepsValuesOutOfTheIndexOnDisk()
    {
        File.WriteAllText(Path.Combine(_dir, "vision-resp-1.json"), """{"box":1,"actionType":"EnterText"}""");

        var snapshot = new UiSnapshot("App", new List<UiElement>
        {
            new() { AutomationId = "txtPassword", ControlType = "Edit", Value = "hunter2-secret",
                    BoundingBox = "10,10,60,60" }
        }, windowBounds: "0,0,100,100");

        var decider = new BridgeVisionDecider(_dir, () => WhitePng(100, 100), timeoutMs: 5000);
        await decider.DecideActionAsync(snapshot, new AgentGoal { Description = "type" }, "");

        // The on-disk index never carries the typed value.
        Assert.DoesNotContain("hunter2-secret", File.ReadAllText(Path.Combine(_dir, "vision-req-1.json")));
        // The on-disk PNG has the secret region painted black (masked at capture, before writing).
        using var bmp = new System.Drawing.Bitmap(Path.Combine(_dir, "vision-req-1.png"));
        Assert.Equal(System.Drawing.Color.Black.ToArgb(), bmp.GetPixel(50, 50).ToArgb());
    }

    [Fact]
    public async Task Decide_RedactsASecretInTheSuccessConditionBeforeWritingThePrompt()
    {
        File.WriteAllText(Path.Combine(_dir, "vision-resp-1.json"), """{"box":1,"actionType":"Click"}""");
        var goal = new AgentGoal { Description = "do it", SuccessCondition = "logged in; token=supersecret123" };

        var decider = new BridgeVisionDecider(_dir, () => WhitePng(900, 900), timeoutMs: 5000);
        await decider.DecideActionAsync(BoxedSnapshot(), goal, "");

        var reqJson = File.ReadAllText(Path.Combine(_dir, "vision-req-1.json"));
        Assert.DoesNotContain("supersecret123", reqJson); // the success-condition secret is redacted
        Assert.Contains("[REDACTED]", reqJson);
    }

    [Fact]
    public async Task Decide_ReturnsWaitOnTimeoutWhenNoReplyArrives()
    {
        var decider = new BridgeVisionDecider(_dir, () => WhitePng(200, 200), timeoutMs: 400);
        var action = await decider.DecideActionAsync(BoxedSnapshot(), new AgentGoal { Description = "x" }, "");

        Assert.Equal("Wait", action.ActionType);
        Assert.Contains("timeout", action.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Decide_ReturnsWaitWhenThereAreNoActionableElements()
    {
        var empty = new UiSnapshot("App", new List<UiElement>(), windowBounds: "0,0,100,100");
        var decider = new BridgeVisionDecider(_dir, () => WhitePng(100, 100), timeoutMs: 400);

        var action = await decider.DecideActionAsync(empty, new AgentGoal { Description = "x" }, "");
        Assert.Equal("Wait", action.ActionType);
        // Nothing was written — there was nothing to show.
        Assert.False(File.Exists(Path.Combine(_dir, "vision-req-1.png")));
    }
}
