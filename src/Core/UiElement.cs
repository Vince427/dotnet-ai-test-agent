namespace DesktopAiTestAgent.Core;

/// <summary>
/// Represents a single UI element discovered in the automation tree.
/// </summary>
public sealed class UiElement
{
    public string? AutomationId { get; set; }
    public string? Name { get; set; }
    public string ControlType { get; set; } = "Unknown";
    public string? Value { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsOffscreen { get; set; }

    /// <summary>
    /// True when UIA reports this as a password control. Treated as sensitive regardless of
    /// its AutomationId/Name, so secrets are redacted/masked even with a benign id.
    /// </summary>
    public bool IsPassword { get; set; }

    public string? BoundingBox { get; set; }

    public override string ToString()
    {
        var id = !string.IsNullOrEmpty(AutomationId) ? AutomationId : Name ?? "(no id)";
        var val = !string.IsNullOrEmpty(Value) ? $" = \"{Value}\"" : "";
        var state = IsEnabled ? "" : " [disabled]";
        return $"{ControlType} | {id}{val}{state}";
    }
}
