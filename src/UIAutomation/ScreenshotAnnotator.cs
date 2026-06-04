using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace DesktopAiTestAgent.UIAutomation;

/// <summary>
/// Draws numbered bounding boxes over a PNG screenshot — the visible half of the V3 Tier-2
/// overlay contract. Pure image operation with no UIA/secret knowledge: the caller supplies
/// already-mapped image-relative rectangles and their labels (see <c>ScreenshotOverlay</c>).
/// Sibling of <see cref="ScreenshotMasker"/>; like it, it must never throw away a screenshot —
/// any failure returns the input bytes unchanged.
/// </summary>
public static class ScreenshotAnnotator
{
    /// <summary>
    /// Returns a new PNG with a labelled rectangle drawn for each box. Boxes are clamped to the
    /// image; empty input or a decode/draw failure returns the bytes unchanged.
    /// </summary>
    public static byte[] Annotate(byte[] pngBytes, IReadOnlyList<(int Label, int X, int Y, int W, int H)> boxes)
    {
        if (pngBytes is null || pngBytes.Length == 0 || boxes is null || boxes.Count == 0)
            return pngBytes ?? [];

        try
        {
            using var input = new MemoryStream(pngBytes);
            using var bitmap = new Bitmap(input);
            using (var g = Graphics.FromImage(bitmap))
            using (var boxPen = new Pen(Color.FromArgb(255, 0, 200, 0), 2f))
            using (var badgeBrush = new SolidBrush(Color.FromArgb(220, 0, 120, 0)))
            using (var textBrush = new SolidBrush(Color.White))
            using (var font = new Font(FontFamily.GenericSansSerif, 9f, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                foreach (var (label, x, y, w, h) in boxes)
                {
                    if (w <= 0 || h <= 0) continue;
                    var cx = Math.Max(0, x);
                    var cy = Math.Max(0, y);
                    var cw = Math.Min(w - (cx - x), bitmap.Width - cx);
                    var ch = Math.Min(h - (cy - y), bitmap.Height - cy);
                    if (cw <= 0 || ch <= 0) continue;

                    g.DrawRectangle(boxPen, new Rectangle(cx, cy, cw, ch));

                    // Number badge in the top-left corner of the box (kept inside the image).
                    var text = label.ToString();
                    var badgeW = 12 + text.Length * 7;
                    var badgeH = 14;
                    var bx = Math.Min(cx, bitmap.Width - badgeW);
                    var by = Math.Min(cy, bitmap.Height - badgeH);
                    if (bx < 0) bx = 0;
                    if (by < 0) by = 0;
                    g.FillRectangle(badgeBrush, new Rectangle(bx, by, badgeW, badgeH));
                    g.DrawString(text, font, textBrush, bx + 2, by + 1);
                }
            }

            using var output = new MemoryStream();
            bitmap.Save(output, ImageFormat.Png);
            return output.ToArray();
        }
        catch
        {
            return pngBytes; // never lose the screenshot over an annotation failure
        }
    }
}
