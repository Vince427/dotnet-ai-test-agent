using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using DesktopAiTestAgent.UIAutomation;

namespace DesktopAiTestAgent.AgentRunner.Tests;

public sealed class ScreenshotDiffServiceTests
{
    [Fact]
    public void ComputeDHash_IdenticalBytes_ZeroHammingDistance()
    {
        var png = HalfSplitPng(leftBlack: true);

        var a = ScreenshotDiffService.ComputeDHash(png);
        var b = ScreenshotDiffService.ComputeDHash(png);

        Assert.Equal(a, b); // deterministic
        Assert.Equal(0, ScreenshotDiffService.HammingDistance(a, b));
    }

    [Fact]
    public void ComputeDHash_MirroredGradient_LargeDistance()
    {
        // A flat brightness change (black vs white) would BOTH hash to 0 (dHash is a
        // gradient, not a brightness, signal). Mirroring the horizontal gradient inverts
        // most comparison bits, so the distance is large — a genuine "different scene".
        var left = ScreenshotDiffService.ComputeDHash(HalfSplitPng(leftBlack: true));
        var right = ScreenshotDiffService.ComputeDHash(HalfSplitPng(leftBlack: false));

        var distance = ScreenshotDiffService.HammingDistance(left, right);
        Assert.True(distance > 10, $"expected a large distance for a mirrored gradient, got {distance}");
        Assert.Equal("different", ScreenshotDiffService.Classify(distance));
    }

    [Theory]
    [InlineData(0UL, 0UL, 0)]
    [InlineData(0UL, ulong.MaxValue, 64)]
    [InlineData(0b1011UL, 0b0001UL, 2)]
    public void HammingDistance_KnownValues(ulong a, ulong b, int expected)
        => Assert.Equal(expected, ScreenshotDiffService.HammingDistance(a, b));

    [Theory]
    [InlineData(0, "same")]
    [InlineData(4, "same")]
    [InlineData(5, "minor")]
    [InlineData(10, "minor")]
    [InlineData(11, "different")]
    [InlineData(64, "different")]
    public void Classify_Bands(int distance, string expected)
        => Assert.Equal(expected, ScreenshotDiffService.Classify(distance));

    [Fact]
    public void ComputeDHash_NullOrEmpty_Throws()
    {
        Assert.Throws<ArgumentException>(() => ScreenshotDiffService.ComputeDHash(null!));
        Assert.Throws<ArgumentException>(() => ScreenshotDiffService.ComputeDHash([]));
    }

    // A 64x64 PNG split left/right into black and white halves (a strong horizontal gradient).
    private static byte[] HalfSplitPng(bool leftBlack)
    {
        using var bmp = new Bitmap(64, 64);
        for (var y = 0; y < 64; y++)
            for (var x = 0; x < 64; x++)
            {
                var isLeft = x < 32;
                var black = leftBlack ? isLeft : !isLeft;
                bmp.SetPixel(x, y, black ? Color.Black : Color.White);
            }

        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }
}
