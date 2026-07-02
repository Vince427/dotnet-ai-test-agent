using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

namespace DesktopAiTestAgent.UIAutomation;

/// <summary>
/// Perceptual screenshot comparison via <b>dHash</b> (difference hash). Pure and stateless:
/// resizes a PNG to a 9×8 grayscale grid, then encodes the 8×8 = 64 horizontal
/// brightness gradients as a 64-bit fingerprint. Two screenshots of the same UI state
/// produce a near-identical hash; a real visual change moves many bits. The
/// <see cref="HammingDistance"/> between two hashes is a cheap, image-free regression /
/// state-change signal that analytics and the run report can consume (P3-B1).
///
/// Grayscale uses Rec.709 luminance. Thresholds (empirical, documented for callers):
/// 0–4 = same frame, 5–10 = minor change, 11+ = a different scene.
/// </summary>
public static class ScreenshotDiffService
{
    private const int HashSize = 8; // 64-bit hash: HashSize x HashSize gradient bits.

    /// <summary>
    /// Computes the 64-bit dHash of a PNG. Throws on a null/empty/undecodable image
    /// (callers that must never lose a run should guard the call, as the orchestrator does).
    /// </summary>
    public static ulong ComputeDHash(byte[] pngBytes)
    {
        if (pngBytes is null || pngBytes.Length == 0)
            throw new ArgumentException("Screenshot bytes are null or empty.", nameof(pngBytes));

        using var input = new MemoryStream(pngBytes);
        using var source = new Bitmap(input);
        // Resize to (HashSize+1) wide x HashSize tall so each row yields HashSize
        // left-to-right comparisons.
        using var small = new Bitmap(HashSize + 1, HashSize);
        using (var g = Graphics.FromImage(small))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(source, 0, 0, HashSize + 1, HashSize);
        }

        ulong hash = 0UL;
        var bit = 0;
        for (var y = 0; y < HashSize; y++)
        {
            var left = Luminance(small.GetPixel(0, y));
            for (var x = 1; x <= HashSize; x++)
            {
                var right = Luminance(small.GetPixel(x, y));
                if (left > right)
                    hash |= 1UL << bit;
                left = right;
                bit++;
            }
        }

        return hash;
    }

    /// <summary>Number of differing bits between two dHashes (0 = identical fingerprint).</summary>
    public static int HammingDistance(ulong a, ulong b)
    {
        var x = a ^ b;
        var count = 0;
        while (x != 0)
        {
            count++;
            x &= x - 1; // clear the lowest set bit
        }
        return count;
    }

    /// <summary>Coarse, human-readable band for a Hamming distance (see class thresholds).</summary>
    public static string Classify(int hammingDistance)
    {
        if (hammingDistance < 0) throw new ArgumentOutOfRangeException(nameof(hammingDistance));
        if (hammingDistance <= 4) return "same";
        if (hammingDistance <= 10) return "minor";
        return "different";
    }

    private static int Luminance(Color c)
        // Rec.709 integer luminance, fixed-point /1024 (coefficients 218+732+74 = 1024, so pure
        // white maps to exactly 255). The absolute scale is irrelevant to the hash — both sides of
        // each comparison use the same formula — but /1024 keeps the value in the true 0–255 range.
        => (218 * c.R + 732 * c.G + 74 * c.B) / 1024;
}
