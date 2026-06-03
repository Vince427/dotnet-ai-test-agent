using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace DesktopAiTestAgent.UIAutomation;

/// <summary>
/// Draws opaque rectangles over regions of a PNG screenshot. Pure image operation with
/// no knowledge of <em>what</em> is secret — callers decide the regions (e.g. the
/// bounding boxes of password fields). Used to redact sensitive UI at capture time so the
/// secret never reaches the artifact on disk, mirroring how text goes through the
/// SecretRedactor.
/// </summary>
public static class ScreenshotMasker
{
    /// <summary>
    /// Returns a new PNG with the given image-relative rectangles filled opaque. Regions
    /// are clamped to the image bounds; empty input or a decode failure returns the bytes
    /// unchanged (masking must never throw away a screenshot).
    /// </summary>
    public static byte[] MaskRegions(byte[] pngBytes, IReadOnlyList<(int X, int Y, int W, int H)> regions)
    {
        if (pngBytes is null || pngBytes.Length == 0 || regions is null || regions.Count == 0)
            return pngBytes ?? [];

        try
        {
            using var input = new MemoryStream(pngBytes);
            using var bitmap = new Bitmap(input);
            using (var g = Graphics.FromImage(bitmap))
            using (var brush = new SolidBrush(Color.Black))
            {
                foreach (var (x, y, w, h) in regions)
                {
                    if (w <= 0 || h <= 0) continue;
                    var cx = Math.Max(0, x);
                    var cy = Math.Max(0, y);
                    var cw = Math.Min(w - (cx - x), bitmap.Width - cx);
                    var ch = Math.Min(h - (cy - y), bitmap.Height - cy);
                    if (cw <= 0 || ch <= 0) continue;
                    g.FillRectangle(brush, new Rectangle(cx, cy, cw, ch));
                }
            }

            using var output = new MemoryStream();
            bitmap.Save(output, ImageFormat.Png);
            return output.ToArray();
        }
        catch
        {
            return pngBytes; // never lose the screenshot over a masking failure
        }
    }
}
