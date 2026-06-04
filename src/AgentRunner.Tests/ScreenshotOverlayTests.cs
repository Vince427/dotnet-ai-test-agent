using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using DesktopAiTestAgent.AgentRunner;
using DesktopAiTestAgent.Core;
using DesktopAiTestAgent.UIAutomation;

namespace DesktopAiTestAgent.AgentRunner.Tests;

/// <summary>
/// Covers the V3 Tier-2 overlay artifact contract: the deterministic snapshot → numbered,
/// image-relative box index (`ScreenshotOverlay`) and the pixel-level annotator
/// (`ScreenshotAnnotator`). No LLM/screen needed — the prerequisite for the VLM decider.
/// </summary>
public sealed class ScreenshotOverlayTests
{
    [Fact]
    public void BuildIndex_NumbersVisibleElementsAndMapsToImageSpace()
    {
        var snapshot = new UiSnapshot("Login", new List<UiElement>
        {
            new() { AutomationId = "txtUser", ControlType = "Edit", BoundingBox = "130,62,220,24" },
            new() { AutomationId = "btnGo", ControlType = "Button", Name = "Go", BoundingBox = "130,97,120,30" }
        }, statusText: null, windowBounds: "100,50,760,820");

        var index = ScreenshotOverlay.BuildIndex(snapshot);

        Assert.Equal(2, index.Count);
        // Numbered 1..n in element order, mapped relative to the window origin (100,50).
        Assert.Equal(1, index[0].N);
        Assert.Equal("txtUser", index[0].AutomationId);
        Assert.Equal("30,12,220,24", index[0].BoundingBox);
        Assert.Equal((30, 12, 220, 24), (index[0].X, index[0].Y, index[0].W, index[0].H));
        Assert.Equal(2, index[1].N);
        Assert.Equal("btnGo", index[1].AutomationId);
        Assert.Equal("30,47,120,30", index[1].BoundingBox);
    }

    [Fact]
    public void BuildIndex_SkipsOffscreenAndUnlocatableWithoutConsumingNumbers()
    {
        var snapshot = new UiSnapshot("App", new List<UiElement>
        {
            new() { AutomationId = "visible1", BoundingBox = "130,62,40,20" },
            new() { AutomationId = "offscreen", BoundingBox = "130,80,40,20", IsOffscreen = true },
            new() { AutomationId = "noBounds", BoundingBox = null },
            new() { AutomationId = "visible2", BoundingBox = "130,140,40,20" }
        }, windowBounds: "100,50,760,820");

        var index = ScreenshotOverlay.BuildIndex(snapshot);

        Assert.Equal(2, index.Count);
        Assert.Equal("visible1", index[0].AutomationId);
        Assert.Equal(1, index[0].N);
        Assert.Equal("visible2", index[1].AutomationId);
        Assert.Equal(2, index[1].N); // numbering is contiguous over kept elements only
    }

    [Fact]
    public void BuildIndex_CarriesIdentifiersAndPasswordFlagButNoValue()
    {
        var snapshot = new UiSnapshot("Login", new List<UiElement>
        {
            new() { AutomationId = "txt1", ControlType = "Edit", IsPassword = true, IsEnabled = false,
                    Value = "hunter2-secret", BoundingBox = "130,97,220,24" }
        }, windowBounds: "100,50,760,820");

        var index = ScreenshotOverlay.BuildIndex(snapshot);

        Assert.Single(index);
        Assert.True(index[0].IsPassword);
        Assert.False(index[0].IsEnabled);
        // The index is identifiers-only — the secret value must never be serialized into it.
        var json = System.Text.Json.JsonSerializer.Serialize(index);
        Assert.DoesNotContain("hunter2-secret", json);
    }

    [Fact]
    public void BuildIndex_EmptyWhenNoWindowBounds()
    {
        var snapshot = new UiSnapshot("App", new List<UiElement>
        {
            new() { AutomationId = "x", BoundingBox = "130,97,220,24" }
        });

        Assert.Empty(ScreenshotOverlay.BuildIndex(snapshot));
    }

    [Fact]
    public void Annotate_DrawsInsideBoxesAndLeavesTheRestUntouched()
    {
        var png = WhitePng(120, 120);

        var annotated = ScreenshotAnnotator.Annotate(png, new List<(int, int, int, int, int)> { (1, 20, 20, 40, 40) });

        using var ms = new MemoryStream(annotated);
        using var bmp = new Bitmap(ms);
        // The rectangle's left edge (x≈20) is painted; a far corner stays white.
        Assert.NotEqual(Color.White.ToArgb(), bmp.GetPixel(20, 40).ToArgb());
        Assert.Equal(Color.White.ToArgb(), bmp.GetPixel(110, 110).ToArgb());
    }

    [Fact]
    public void Annotate_NeverThrowsAwayTheScreenshotOnBadInput()
    {
        var notAPng = new byte[] { 1, 2, 3, 4 };
        Assert.Same(notAPng, ScreenshotAnnotator.Annotate(notAPng, new List<(int, int, int, int, int)> { (1, 0, 0, 2, 2) }));
    }

    [Fact]
    public void Annotate_NoBoxes_ReturnsInputUnchanged()
    {
        var png = WhitePng(10, 10);
        Assert.Same(png, ScreenshotAnnotator.Annotate(png, new List<(int, int, int, int, int)>()));
    }

    private static byte[] WhitePng(int w, int h)
    {
        using var bmp = new Bitmap(w, h);
        using (var g = Graphics.FromImage(bmp)) g.Clear(Color.White);
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }
}
