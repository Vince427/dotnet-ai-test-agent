using System.Collections.Generic;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner;

/// <summary>
/// Computes the screenshot-relative rectangles that must be masked because they show a
/// secret field. Maps each sensitive element's screen-space <see cref="UiElement.BoundingBox"/>
/// into image pixels using the snapshot's <see cref="UiSnapshot.WindowBounds"/> (the
/// screenshot origin). Deterministic and image-library-free, so it is unit-testable.
/// </summary>
internal static class ScreenshotRedaction
{
    public static List<(int X, int Y, int W, int H)> SecretRegions(UiSnapshot snapshot, SecretRedactor redactor)
    {
        var regions = new List<(int, int, int, int)>();
        if (!TryParseRect(snapshot.WindowBounds, out var win))
            return regions; // no origin → cannot map; skip (masking is best-effort)

        foreach (var element in snapshot.Elements)
        {
            if (!redactor.IsSensitiveIdentifier(element.AutomationId) &&
                !redactor.IsSensitiveIdentifier(element.Name))
                continue;
            if (!TryParseRect(element.BoundingBox, out var el))
                continue;

            // Element screen rect → image-relative (screenshot starts at the window origin).
            regions.Add((el.X - win.X, el.Y - win.Y, el.W, el.H));
        }

        return regions;
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
