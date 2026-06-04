using System.Collections.Generic;
using System.Threading.Tasks;
using DesktopAiTestAgent.AgentRunner;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner.Tests;

/// <summary>
/// V3 Tier-2 vision fallback, key-free: the deterministic box→element mapping
/// (<c>VisionResponseParser</c>) and the escalation policy (<c>VisionActionDecider</c> only calls
/// the VLM when Tier-1's UIA target can't be resolved). The VLM itself is a scripted fake.
/// </summary>
public sealed class VisionDeciderTests
{
    // ---- VisionResponseParser (pure) ----

    private static List<OverlayBox> Index() => new()
    {
        new OverlayBox { N = 1, AutomationId = "btnOk", ControlType = "Button" },
        new OverlayBox { N = 2, Name = "Cancel", ControlType = "Button" }
    };

    [Fact]
    public void Parser_MapsChosenBoxToElementIdentifier()
    {
        var a = VisionResponseParser.Parse("""{"box":1,"actionType":"Click","reason":"ok","confidence":90}""", Index());
        Assert.Equal("Click", a.ActionType);
        Assert.Equal("btnOk", a.AutomationId);
    }

    [Fact]
    public void Parser_FallsBackToNameWhenNoAutomationId()
    {
        var a = VisionResponseParser.Parse("""{"box":2,"actionType":"Click"}""", Index());
        Assert.Equal("Cancel", a.AutomationId);
    }

    [Fact]
    public void Parser_UnknownBox_DegradesToWait()
    {
        var a = VisionResponseParser.Parse("""{"box":9,"actionType":"Click"}""", Index());
        Assert.Equal("Wait", a.ActionType);
        Assert.Contains("box 9", a.Reason);
    }

    [Fact]
    public void Parser_BoxlessActionIsAllowed()
    {
        var a = VisionResponseParser.Parse("""{"actionType":"Done","reason":"goal met"}""", Index());
        Assert.Equal("Done", a.ActionType);
        Assert.Null(a.AutomationId);
    }

    [Fact]
    public void Parser_StripsFencesAndHandlesGarbage()
    {
        var fenced = VisionResponseParser.Parse("```json\n{\"box\":1,\"actionType\":\"Click\"}\n```", Index());
        Assert.Equal("btnOk", fenced.AutomationId);

        var garbage = VisionResponseParser.Parse("not json at all", Index());
        Assert.Equal("Wait", garbage.ActionType);
    }

    // ---- VisionActionDecider (escalation policy) ----

    private sealed class FixedDecider(AgentAction a) : IActionDecider
    {
        public Task<AgentAction> DecideActionAsync(UiSnapshot s, AgentGoal g, string m, string? w = null) => Task.FromResult(a);
    }

    private sealed class ScriptedVision(string json) : IVisionClient
    {
        public bool Called { get; private set; }
        public byte[]? LastPng { get; private set; }
        public string? LastIndexJson { get; private set; }
        public Task<string> AskAsync(byte[] png, string indexJson, string prompt)
        {
            Called = true; LastPng = png; LastIndexJson = indexJson; return Task.FromResult(json);
        }
    }

    private static UiSnapshot SnapshotWithBoxedElement() => new("App", new List<UiElement>
    {
        new() { AutomationId = "realBtn", ControlType = "Button", BoundingBox = "130,97,80,30" }
    }, windowBounds: "100,50,760,820");

    private static AgentGoal Goal() => new() { Description = "do it" };

    [Fact]
    public async Task Decider_UsesTier1WhenItsTargetResolves_NoVlmCall()
    {
        var snapshot = SnapshotWithBoxedElement();
        var tier1 = new FixedDecider(new AgentAction { ActionType = "Click", AutomationId = "realBtn" });
        var vlm = new ScriptedVision("""{"box":1,"actionType":"Click"}""");

        var decider = new VisionActionDecider(tier1, vlm, () => new byte[] { 1 });
        var a = await decider.DecideActionAsync(snapshot, Goal(), "");

        Assert.Equal("realBtn", a.AutomationId);
        Assert.False(vlm.Called); // Tier-1 resolved → vision never invoked
    }

    [Fact]
    public async Task Decider_EscalatesToVisionWhenTier1TargetIsUnresolvable()
    {
        var snapshot = SnapshotWithBoxedElement(); // only "realBtn" exists / has a box
        var tier1 = new FixedDecider(new AgentAction { ActionType = "Click", AutomationId = "ghostThatDoesNotExist" });
        var vlm = new ScriptedVision("""{"box":1,"actionType":"Click","reason":"the visible button"}""");

        var decider = new VisionActionDecider(tier1, vlm, () => new byte[] { 1, 2, 3 });
        var a = await decider.DecideActionAsync(snapshot, Goal(), "");

        Assert.True(vlm.Called);            // UIA ambiguous → escalated
        Assert.Equal("Click", a.ActionType);
        Assert.Equal("realBtn", a.AutomationId); // box 1 mapped back to the real element
    }

    [Fact]
    public async Task Decider_MasksSecretRegionsAndKeepsValuesOutOfTheIndexBeforeSendingToVlm()
    {
        // A password field, visible (so it gets an overlay box) and sensitive (so it's masked).
        var snapshot = new UiSnapshot("App", new List<UiElement>
        {
            new() { AutomationId = "txtPassword", ControlType = "Edit", Value = "hunter2-secret",
                    BoundingBox = "10,10,60,60" }
        }, windowBounds: "0,0,100,100");
        var tier1 = new FixedDecider(new AgentAction { ActionType = "Click", AutomationId = "ghost" });
        var vlm = new ScriptedVision("""{"box":1,"actionType":"EnterText"}""");

        // Capture returns an all-white 100x100 PNG; the secret region must come back painted.
        var decider = new VisionActionDecider(tier1, vlm, () => WhitePng(100, 100));
        await decider.DecideActionAsync(snapshot, Goal(), "");

        Assert.True(vlm.Called);
        Assert.DoesNotContain("hunter2-secret", vlm.LastIndexJson); // index is identifiers-only
        using var ms = new System.IO.MemoryStream(vlm.LastPng!);
        using var bmp = new System.Drawing.Bitmap(ms);
        // A pixel well inside the password region (away from the box outline/number badge) is masked.
        Assert.Equal(System.Drawing.Color.Black.ToArgb(), bmp.GetPixel(50, 50).ToArgb());
    }

    private static byte[] WhitePng(int w, int h)
    {
        using var bmp = new System.Drawing.Bitmap(w, h);
        using (var g = System.Drawing.Graphics.FromImage(bmp)) g.Clear(System.Drawing.Color.White);
        using var ms = new System.IO.MemoryStream();
        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        return ms.ToArray();
    }

    [Fact]
    public async Task Decider_NoWindowBounds_KeepsTier1ResultWithoutCallingVision()
    {
        var snapshot = new UiSnapshot("App", new List<UiElement>
        {
            new() { AutomationId = "x", BoundingBox = "1,2,3,4" }
        }); // no windowBounds → overlay can't map → no escalation
        var tier1 = new FixedDecider(new AgentAction { ActionType = "Click", AutomationId = "ghost" });
        var vlm = new ScriptedVision("""{"box":1,"actionType":"Click"}""");

        var decider = new VisionActionDecider(tier1, vlm, () => new byte[] { 1 });
        var a = await decider.DecideActionAsync(snapshot, Goal(), "");

        Assert.False(vlm.Called);
        Assert.Equal("ghost", a.AutomationId); // unchanged Tier-1 action
    }
}
