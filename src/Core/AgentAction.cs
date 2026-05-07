namespace DesktopAiTestAgent.Core;

/// <summary>
/// An action decided by the AI agent to perform on the UI.
/// Extended with additional action types for richer interaction.
/// </summary>
public class AgentAction
{
    /// <summary>
    /// The type of action: EnterText, Click, DoubleClick, Scroll, Wait, Assert, Done, Explore.
    /// </summary>
    public string? ActionType { get; set; }

    /// <summary>The AutomationId or Name of the target element.</summary>
    public string? AutomationId { get; set; }

    /// <summary>The value to enter (for EnterText) or direction (for Scroll: up/down).</summary>
    public string? Value { get; set; }

    /// <summary>The agent's reasoning for choosing this action.</summary>
    public string? Reason { get; set; }

    /// <summary>Confidence score (0-100) the agent has in this action.</summary>
    public int? Confidence { get; set; }
}
