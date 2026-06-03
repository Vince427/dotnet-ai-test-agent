namespace DesktopAiTestAgent.Core;

using System.Collections.Generic;
using System.Text;

/// <summary>
/// A point-in-time snapshot of the entire UI state, containing all discovered elements.
/// Replaces the previous hardcoded login-specific snapshot.
/// </summary>
public sealed class UiSnapshot(string windowTitle, List<UiElement> elements, string? statusText = null, string? windowBounds = null)
{
    public string WindowTitle { get; } = windowTitle;
    public List<UiElement> Elements { get; } = elements;
    public string? StatusText { get; } = statusText;

    /// <summary>
    /// The target window's screen rectangle as "X,Y,W,H" (same coordinate space as each
    /// <see cref="UiElement.BoundingBox"/>). Used to map element rects into screenshot
    /// pixels for secret-field masking. Null when unknown.
    /// </summary>
    public string? WindowBounds { get; } = windowBounds;

    /// <summary>
    /// Produces a compact text representation of all UI elements for LLM prompts.
    /// </summary>
    public string ToPromptText()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Window: {WindowTitle}");
        if (!string.IsNullOrEmpty(StatusText))
            sb.AppendLine($"Status: {StatusText}");
        sb.AppendLine("Elements:");
        foreach (var el in Elements)
        {
            sb.AppendLine($"  - {el}");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Tries to find a status-like label in the elements (common pattern for WinForms apps).
    /// </summary>
    public string? FindStatusText()
    {
        if (!string.IsNullOrEmpty(StatusText))
            return StatusText;

        foreach (var el in Elements)
        {
            if (el.ControlType == "Text" || el.ControlType == "Label")
            {
                var id = el.AutomationId ?? el.Name ?? "";
                if (id.IndexOf("status", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    id.IndexOf("lblStatus", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return el.Value ?? el.Name;
                }
            }
        }
        return null;
    }
}
