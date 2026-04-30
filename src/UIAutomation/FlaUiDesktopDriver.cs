using System;
using DesktopAiTestAgent.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using FlaUI.UIA3;

namespace DesktopAiTestAgent.UIAutomation;

public sealed class FlaUiDesktopDriver : IAutomationDriver, IDisposable
{
    private UIA3Automation _automation;
    private Window _window;

    public bool AttachToWindow(string windowTitle, TimeSpan timeout)
    {
        _automation = new UIA3Automation();
        var desktop = _automation.GetDesktop();

        var retry = Retry.WhileNull(
            () => desktop.FindFirstDescendant(cf => cf.ByName(windowTitle))?.AsWindow(),
            timeout,
            TimeSpan.FromMilliseconds(300));

        _window = retry.Result;
        return _window != null;
    }

    public UiSnapshot Capture()
    {
        EnsureWindow();

        var username = _window.FindFirstDescendant(cf => cf.ByAutomationId("txtUsername"));
        var password = _window.FindFirstDescendant(cf => cf.ByAutomationId("txtPassword"));
        var login = _window.FindFirstDescendant(cf => cf.ByAutomationId("btnLogin"));
        var status = _window.FindFirstDescendant(cf => cf.ByAutomationId("lblStatus"));

        return new UiSnapshot(
            _window.Title,
            username.AutomationId,
            password.AutomationId,
            login.AutomationId,
            status.AutomationId,
            status.AsLabel().Text);
    }

    public void EnterText(string automationId, string value)
    {
        EnsureWindow();
        var box = _window.FindFirstDescendant(cf => cf.ByAutomationId(automationId))?.AsTextBox();
        if (box == null) throw new InvalidOperationException("TextBox not found: " + automationId);
        box.Focus();
        box.Text = value;
    }

    public void Click(string automationId)
    {
        EnsureWindow();
        var button = _window.FindFirstDescendant(cf => cf.ByAutomationId(automationId))?.AsButton();
        if (button == null) throw new InvalidOperationException("Button not found: " + automationId);
        button.Focus();
        button.Invoke();
    }

    public string ReadText(string automationId)
    {
        EnsureWindow();
        var label = _window.FindFirstDescendant(cf => cf.ByAutomationId(automationId))?.AsLabel();
        if (label == null) throw new InvalidOperationException("Label not found: " + automationId);
        return label.Text;
    }

    private void EnsureWindow()
    {
        if (_window == null) throw new InvalidOperationException("Window is not attached.");
    }

    public void Dispose()
    {
        if (_automation != null)
        {
            _automation.Dispose();
        }
    }
}
