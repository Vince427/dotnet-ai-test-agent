using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using DesktopAiTestAgent.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.UIA3;

namespace DesktopAiTestAgent.UIAutomation;

/// <summary>
/// FlaUI-based automation driver that can discover and control any WinForms UI.
/// V1.3: generic tree walking, screenshots, scroll, double-click.
/// </summary>
public sealed class FlaUiDesktopDriver : IAutomationDriver, IDisposable
{
    private UIA3Automation? _automation;
    private Window? _window;

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

    /// <summary>
    /// Captures a generic snapshot of all UI elements in the current window.
    /// </summary>
    public UiSnapshot Capture()
    {
        EnsureWindow();
        var elements = GetAllElements();

        // Try to find a status label for backward compat
        string? statusText = null;
        foreach (var el in elements)
        {
            if ((el.ControlType == "Text" || el.ControlType == "Edit") &&
                !string.IsNullOrEmpty(el.AutomationId) &&
                el.AutomationId!.IndexOf("status", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                statusText = el.Value ?? el.Name;
                break;
            }
        }

        return new UiSnapshot(_window!.Title, elements, statusText);
    }

    /// <summary>
    /// Walks the entire UI tree and returns all discoverable elements.
    /// </summary>
    public List<UiElement> GetAllElements()
    {
        EnsureWindow();
        var result = new List<UiElement>();

        foreach (var el in _window!.FindAllDescendants())
        {
            try
            {
                var uiEl = new UiElement
                {
                    AutomationId = SafeGet(() => el.AutomationId),
                    Name = SafeGet(() => el.Name),
                    ControlType = SafeGet(() => el.ControlType.ToString()) ?? "Unknown",
                    IsEnabled = SafeGetBool(() => el.IsEnabled, true),
                    IsOffscreen = SafeGetBool(() => el.IsOffscreen, false),
                };

                // Try to read value for text-like controls
                var ctName = uiEl.ControlType;
                if (ctName == "Edit" || ctName == "Document")
                {
                    try { uiEl.Value = el.AsTextBox()?.Text; } catch { }
                }
                else if (ctName == "Text")
                {
                    try { uiEl.Value = el.AsLabel()?.Text; } catch { }
                }
                else if (ctName == "ComboBox")
                {
                    try { uiEl.Value = el.AsComboBox()?.SelectedItem?.Text; } catch { }
                }

                // Bounding box
                try
                {
                    var rect = el.BoundingRectangle;
                    uiEl.BoundingBox = $"{rect.X},{rect.Y},{rect.Width},{rect.Height}";
                }
                catch { }

                // Skip empty elements with no useful info
                if (string.IsNullOrEmpty(uiEl.AutomationId) &&
                    string.IsNullOrEmpty(uiEl.Name) &&
                    string.IsNullOrEmpty(uiEl.Value))
                    continue;

                result.Add(uiEl);
            }
            catch
            {
                // Skip elements that throw during inspection
            }
        }

        return result;
    }

    public byte[] CaptureScreenshot()
    {
        EnsureWindow();
        using var ms = new MemoryStream();
        var captureImage = FlaUI.Core.Capturing.Capture.Element(_window!);
        captureImage.Bitmap.Save(ms, ImageFormat.Png);
        captureImage.Dispose();
        return ms.ToArray();
    }

    public void EnterText(string automationId, string value)
    {
        EnsureWindow();
        var el = FindElement(automationId);
        var box = el?.AsTextBox() ?? throw new InvalidOperationException("TextBox not found: " + automationId);
        box.Focus();
        box.Text = value;
    }

    public void Click(string automationId)
    {
        EnsureWindow();
        var el = FindElement(automationId) ?? throw new InvalidOperationException("Element not found: " + automationId);
        el.Focus();

        // Try Button.Invoke first, then fall back to mouse click
        try
        {
            el.AsButton()?.Invoke();
        }
        catch
        {
            Mouse.Click(el.GetClickablePoint());
        }
    }

    public void DoubleClick(string automationId)
    {
        EnsureWindow();
        var el = FindElement(automationId) ?? throw new InvalidOperationException("Element not found: " + automationId);
        el.Focus();
        Mouse.DoubleClick(el.GetClickablePoint());
    }

    public void Scroll(string automationId, string direction)
    {
        EnsureWindow();
        var el = FindElement(automationId) ?? throw new InvalidOperationException("Element not found: " + automationId);
        el.Focus();
        var amount = string.Equals(direction, "up", StringComparison.OrdinalIgnoreCase) ? 3 : -3;
        Mouse.Scroll(amount);
    }

    public string ReadText(string automationId)
    {
        EnsureWindow();
        var el = FindElement(automationId) ?? throw new InvalidOperationException("Element not found: " + automationId);

        // Try label first, then textbox
        try { return el.AsLabel().Text; } catch { }
        try { return el.AsTextBox().Text; } catch { }
        return el.Name;
    }

    /// <summary>
    /// Multi-strategy element resolution: try AutomationId first, then Name.
    /// </summary>
    private AutomationElement? FindElement(string identifier)
    {
        // Strategy 1: by AutomationId
        var el = _window!.FindFirstDescendant(cf => cf.ByAutomationId(identifier));
        if (el is not null) return el;

        // Strategy 2: by Name
        el = _window!.FindFirstDescendant(cf => cf.ByName(identifier));
        return el;
    }

    private void EnsureWindow()
    {
        if (_window is null) throw new InvalidOperationException("Window is not attached.");
    }

    private static string? SafeGet(Func<string> getter)
    {
        try { return getter(); } catch { return null; }
    }

    private static bool SafeGetBool(Func<bool> getter, bool defaultValue)
    {
        try { return getter(); } catch { return defaultValue; }
    }

    public void Dispose()
    {
        _automation?.Dispose();
    }
}
