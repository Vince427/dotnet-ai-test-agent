using System.Collections.Generic;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner;

/// <summary>
/// One numbered element in the Tier-2 overlay index. <see cref="N"/> is the label drawn on the
/// annotated screenshot; the rect is image-relative (screenshot pixels). Carries only stable
/// identifiers — never a control's <c>Value</c> — so the index is safe to write even for a
/// password field (its box is shown, its secret is not).
/// </summary>
internal sealed class OverlayBox
{
    public int N { get; set; }
    public string? AutomationId { get; set; }
    public string? Name { get; set; }
    public string ControlType { get; set; } = "Unknown";
    public bool IsEnabled { get; set; } = true;
    public bool IsPassword { get; set; }

    /// <summary>Image-relative rectangle as "X,Y,W,H" (screenshot pixels).</summary>
    public string BoundingBox { get; set; } = "";

    // Parsed image-relative rect, for the annotator. Not serialized into the index JSON.
    [System.Text.Json.Serialization.JsonIgnore] public int X { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public int Y { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public int W { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public int H { get; set; }
}

/// <summary>
/// Builds the deterministic Tier-2 overlay index for a snapshot: numbers each visible, locatable
/// element and maps its screen-space <see cref="UiElement.BoundingBox"/> into screenshot pixels
/// via <see cref="UiSnapshot.WindowBounds"/> (the screenshot origin) — exactly like
/// <c>ScreenshotRedaction</c>. The numbered boxes feed <c>ScreenshotAnnotator</c>; the index
/// (number → identifiers) is the artifact a VLM consumes to answer "pick box N". Pure and
/// image-library-free, so it is unit-testable without an LLM or a screen.
/// </summary>
internal static class ScreenshotOverlay
{
    public static List<OverlayBox> BuildIndex(UiSnapshot snapshot)
    {
        var boxes = new List<OverlayBox>();
        if (snapshot is null || !TryParseRect(snapshot.WindowBounds, out var win))
            return boxes; // no origin → cannot map; overlay is best-effort, like masking

        var n = 0;
        foreach (var el in snapshot.Elements)
        {
            if (el.IsOffscreen)
                continue;
            if (!TryParseRect(el.BoundingBox, out var r))
                continue;

            n++;
            var ix = r.X - win.X;
            var iy = r.Y - win.Y;
            boxes.Add(new OverlayBox
            {
                N = n,
                AutomationId = el.AutomationId,
                Name = el.Name,
                ControlType = el.ControlType,
                IsEnabled = el.IsEnabled,
                IsPassword = el.IsPassword,
                X = ix,
                Y = iy,
                W = r.W,
                H = r.H,
                BoundingBox = $"{ix},{iy},{r.W},{r.H}"
            });
        }

        return boxes;
    }

    /// <summary>The (label, rect) tuples for <c>ScreenshotAnnotator.Annotate</c>.</summary>
    public static List<(int Label, int X, int Y, int W, int H)> ToBoxes(IReadOnlyList<OverlayBox> index)
    {
        var result = new List<(int, int, int, int, int)>();
        foreach (var b in index)
            result.Add((b.N, b.X, b.Y, b.W, b.H));
        return result;
    }

    private static bool TryParseRect(string? value, out (int X, int Y, int W, int H) rect)
    {
        rect = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var parts = value!.Split(',');
        if (parts.Length != 4) return false;
        if (int.TryParse(parts[0], out var x) && int.TryParse(parts[1], out var y) &&
            int.TryParse(parts[2], out var w) && int.TryParse(parts[3], out var h))
        {
            rect = (x, y, w, h);
            return true;
        }
        return false;
    }
}
