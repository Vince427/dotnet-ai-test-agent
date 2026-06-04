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
    /// Returns the FIRST status region — use <see cref="StatusContains"/> to test a success
    /// condition, which scans every status region (multi-region apps).
    /// </summary>
    public string? FindStatusText()
    {
        if (!string.IsNullOrEmpty(StatusText))
            return StatusText;

        foreach (var label in EnumerateStatusLabels())
            return label;
        return null;
    }

    /// <summary>
    /// True when <paramref name="text"/> appears in <em>any</em> status region (the snapshot's
    /// <see cref="StatusText"/> or any status-like label). Unlike <see cref="FindStatusText"/>,
    /// this does not stop at the first label — so a success condition that lands in a non-first
    /// status region (e.g. a separate action-result label, distinct from the login status) is
    /// still detected. See DISCOVERY_LOG 2026-06-02.
    /// </summary>
    public bool StatusContains(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;
        if (!string.IsNullOrEmpty(StatusText) &&
            StatusText!.IndexOf(text, System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        foreach (var label in EnumerateStatusLabels())
        {
            if (label.IndexOf(text, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }

    /// <summary>Yields the text of every status-like label, in element order.</summary>
    private IEnumerable<string> EnumerateStatusLabels()
    {
        foreach (var el in Elements)
        {
            if (el.ControlType != "Text" && el.ControlType != "Label")
                continue;
            var id = el.AutomationId ?? el.Name ?? "";
            if (id.IndexOf("status", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                id.IndexOf("lblStatus", System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            var text = el.Value ?? el.Name;
            if (!string.IsNullOrEmpty(text))
                yield return text!;
        }
    }
}
