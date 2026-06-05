namespace DesktopAiTestAgent.Core;

/// <summary>The kind of UI Automation event a recorder observed on a control (V9.5 recording mode).</summary>
public enum UiEventKind
{
    /// <summary>A button / menu item was invoked (replays as a click).</summary>
    Invoked,
    /// <summary>An editable control's value changed (replays as text entry).</summary>
    ValueChanged,
    /// <summary>A checkbox / toggle flipped (replays as a click).</summary>
    Toggled,
    /// <summary>A list / combo selection changed (replays as a click).</summary>
    SelectionChanged
}

/// <summary>
/// A normalized, framework-agnostic UI Automation event captured during a manual session. The live
/// FlaUI/UIA source (env-bound) emits these from outside the target app; the pure
/// <c>SessionRecorder</c> maps them into <c>RecordedAction</c>s — so the recording logic is testable
/// without a desktop, and the captured artifact stays portable.
/// </summary>
public sealed class CapturedUiEvent
{
    public UiEventKind Kind { get; set; }
    public string? AutomationId { get; set; }
    public string? Name { get; set; }
    public string? ControlType { get; set; }
    /// <summary>The new value for <see cref="UiEventKind.ValueChanged"/>; null otherwise.</summary>
    public string? Value { get; set; }
}
