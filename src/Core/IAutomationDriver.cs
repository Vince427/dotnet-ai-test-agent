namespace DesktopAiTestAgent.Core;

using System;

public interface IAutomationDriver
{
    bool AttachToWindow(string windowTitle, TimeSpan timeout);
    UiSnapshot Capture();
    void EnterText(string automationId, string value);
    void Click(string automationId);
    string ReadText(string automationId);
}
