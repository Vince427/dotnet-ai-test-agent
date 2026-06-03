using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using DesktopAiTestAgent.AgentRunner;
using DesktopAiTestAgent.Core;
using DesktopAiTestAgent.UIAutomation;

namespace DesktopAiTestAgent.AgentRunner.Tests;

/// <summary>
/// Covers secret-field screenshot masking (V3-A): the deterministic region mapping
/// (screen → screenshot pixels via the window origin) and the pixel-level masker.
/// </summary>
public sealed class ScreenshotMaskingTests
{
    [Fact]
    public void SecretRegions_MapsSensitiveElementIntoImageSpace()
    {
        var snapshot = new UiSnapshot("Login", new List<UiElement>
        {
            new() { AutomationId = "txtUsername", BoundingBox = "130,62,220,24" },
            new() { AutomationId = "txtPassword", BoundingBox = "130,97,220,24" }
        }, statusText: null, windowBounds: "100,50,760,820");

        var regions = ScreenshotRedaction.SecretRegions(snapshot, new SecretRedactor());

        // Only the password field is sensitive; mapped relative to the window origin.
        Assert.Single(regions);
        Assert.Equal((30, 47, 220, 24), regions[0]);
    }

    [Fact]
    public void SecretRegions_EmptyWhenNoWindowBounds()
    {
        var snapshot = new UiSnapshot("Login", new List<UiElement>
        {
            new() { AutomationId = "txtPassword", BoundingBox = "130,97,220,24" }
        });

        Assert.Empty(ScreenshotRedaction.SecretRegions(snapshot, new SecretRedactor()));
    }

    [Fact]
    public void MaskRegions_PaintsRegionOpaqueAndLeavesRest()
    {
        var png = WhitePng(100, 100);

        var masked = ScreenshotMasker.MaskRegions(png, new List<(int, int, int, int)> { (10, 10, 20, 20) });

        using var ms = new MemoryStream(masked);
        using var bmp = new Bitmap(ms);
        Assert.Equal(Color.Black.ToArgb(), bmp.GetPixel(15, 15).ToArgb()); // inside the region
        Assert.Equal(Color.White.ToArgb(), bmp.GetPixel(60, 60).ToArgb()); // outside untouched
    }

    [Fact]
    public void MaskRegions_ClampsOutOfBoundsWithoutThrowing()
    {
        var png = WhitePng(40, 40);
        var masked = ScreenshotMasker.MaskRegions(png, new List<(int, int, int, int)> { (30, 30, 100, 100) });
        Assert.NotEmpty(masked); // clamped to image, no exception
    }

    [Fact]
    public void MaskRegions_NoRegions_ReturnsInputUnchanged()
    {
        var png = WhitePng(10, 10);
        Assert.Same(png, ScreenshotMasker.MaskRegions(png, new List<(int, int, int, int)>()));
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
