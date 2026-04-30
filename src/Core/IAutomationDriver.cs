namespace DesktopAiTestAgent.Core;

using System;
using System.Collections.Generic;

/// <summary>
/// Abstraction over UI automation frameworks (FlaUI, etc.).
/// Extended with generic discovery and screenshot methods.
/// </summary>
public interface IAutomationDriver
{
    bool AttachToWindow(string windowTitle, TimeSpan timeout);
    UiSnapshot Capture();
    void EnterText(string automationId, string value);
    void Click(string automationId);
    string ReadText(string automationId);

    // --- V1.3 additions ---

    /// <summary>Returns all UI elements visible in the current window.</summary>
    List<UiElement> GetAllElements();

    /// <summary>Captures a screenshot of the current window as PNG bytes.</summary>
    byte[] CaptureScreenshot();

    /// <summary>Scrolls the element identified by automationId in the given direction.</summary>
    void Scroll(string automationId, string direction);

    /// <summary>Double-clicks the element identified by automationId.</summary>
    void DoubleClick(string automationId);
}
