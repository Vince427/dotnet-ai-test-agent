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
    private UiSnapshot? _cachedSnapshot;
    private bool _isDirty = true;
    private string _attachedWindowTitle = "Attached Window";

    public bool AttachToWindow(string windowTitle, TimeSpan timeout)
    {
        _automation?.Dispose();
        _automation = new UIA3Automation();
        try
        {
            var desktop = _automation.GetDesktop();

            var retry = Retry.WhileNull(
                () => desktop.FindFirstDescendant(cf => cf.ByName(windowTitle))?.AsWindow(),
                timeout,
                TimeSpan.FromMilliseconds(300));

            _window = retry.Result;
            if (_window == null)
            {
                _automation?.Dispose();
                _automation = null;
                return false;
            }
            _attachedWindowTitle = SafeGet(() => _window.Title) ?? windowTitle;
            _isDirty = true;
            return true;
        }
        catch
        {
            _automation?.Dispose();
            _automation = null;
            throw;
        }
    }

    /// <summary>
    /// Captures a generic snapshot of all UI elements in the current window.
    /// </summary>
    public UiSnapshot Capture()
    {
        if (!_isDirty && _cachedSnapshot != null)
        {
            return _cachedSnapshot;
        }

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

        string? windowBounds = null;
        try
        {
            var wr = _window!.BoundingRectangle;
            windowBounds = $"{wr.X},{wr.Y},{wr.Width},{wr.Height}";
        }
        catch { /* bounds unavailable; masking will be skipped */ }

        var title = SafeGet(() => _window!.Title) ?? _attachedWindowTitle;
        _cachedSnapshot = new UiSnapshot(title, elements, statusText, windowBounds);
        _isDirty = false;
        return _cachedSnapshot;
    }

    /// <summary>
    /// Walks the entire UI tree and returns all discoverable elements.
    /// </summary>
    public List<UiElement> GetAllElements()
    {
        EnsureWindow();
        var result = new List<UiElement>();

        // 1. Gather elements from the main window
        GatherElementsFrom(_window, result);

        // 2. Fallback/append elements from the active foreground window of the same process if it is different
        try
        {
            var desktop = _automation!.GetDesktop();
            var activeWindow = desktop.FindFirstChild(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window))?.AsWindow();
            if (activeWindow != null && 
                activeWindow.Properties.ProcessId.Value == _window!.Properties.ProcessId.Value &&
                activeWindow.AutomationId != _window.AutomationId)
            {
                GatherElementsFrom(activeWindow, result);
            }
        }
        catch { }

        return result;
    }

    private void GatherElementsFrom(AutomationElement? root, List<UiElement> result)
    {
        if (root is null) return;
        try
        {
            foreach (var el in root.FindAllDescendants())
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
                        IsPassword = SafeGetBool(() => el.Properties.IsPassword.Value, false),
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

                    // Prevent duplicates
                    if (result.Exists(x => x.AutomationId == uiEl.AutomationId && x.Name == uiEl.Name && x.ControlType == uiEl.ControlType))
                        continue;

                    result.Add(uiEl);
                }
                catch
                {
                    // Skip elements that throw during inspection
                }
            }
        }
        catch
        {
            // Ignore tree-walk enumeration errors
        }
    }

    public byte[] CaptureScreenshot()
    {
        EnsureWindow();
        try
        {
            try { _window!.SetForeground(); System.Threading.Thread.Sleep(150); } catch { }
            using var ms = new MemoryStream();
            using var captureImage = FlaUI.Core.Capturing.Capture.Element(_window!);
            captureImage.Bitmap.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }
        catch
        {
            // Fallback to full screen capture using GDI+ if UIA capture fails or times out
            try
            {
                using var ms = new MemoryStream();
                var bounds = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
                using var bitmap = new System.Drawing.Bitmap(bounds.Width, bounds.Height);
                using (var g = System.Drawing.Graphics.FromImage(bitmap))
                {
                    g.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size);
                }
                bitmap.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
            catch
            {
                // Return an empty 1x1 PNG if everything fails
                using var ms = new MemoryStream();
                using var bitmap = new System.Drawing.Bitmap(1, 1);
                bitmap.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
        }
    }

    public void EnterText(string automationId, string value)
    {
        EnsureWindow();
        var el = FindElement(automationId);
        var box = el?.AsTextBox() ?? throw new InvalidOperationException("TextBox not found: " + automationId);
        try { box.Focus(); } catch { }
        box.Text = value;
        _isDirty = true;
    }

    public void Click(string automationId)
    {
        EnsureWindow();
        var el = FindElement(automationId) ?? throw new InvalidOperationException("Element not found: " + automationId);
        try { el.Focus(); } catch { }

        // Try Button.Invoke first, then fall back to mouse click
        try
        {
            el.AsButton()?.Invoke();
        }
        catch
        {
            System.Drawing.Point point;
            try
            {
                point = el.GetClickablePoint();
            }
            catch (FlaUI.Core.Exceptions.NoClickablePointException)
            {
                var rect = el.BoundingRectangle;
                point = new System.Drawing.Point((int)(rect.X + rect.Width / 2), (int)(rect.Y + rect.Height / 2));
            }
            Mouse.Click(point);
        }
        _isDirty = true;
    }

    public void DoubleClick(string automationId)
    {
        EnsureWindow();
        var el = FindElement(automationId) ?? throw new InvalidOperationException("Element not found: " + automationId);
        try { el.Focus(); } catch { }
        
        System.Drawing.Point point;
        try
        {
            point = el.GetClickablePoint();
        }
        catch (FlaUI.Core.Exceptions.NoClickablePointException)
        {
            var rect = el.BoundingRectangle;
            point = new System.Drawing.Point((int)(rect.X + rect.Width / 2), (int)(rect.Y + rect.Height / 2));
        }
        Mouse.DoubleClick(point);
        _isDirty = true;
    }

    public void Scroll(string automationId, string direction)
    {
        EnsureWindow();
        var el = FindElement(automationId) ?? throw new InvalidOperationException("Element not found: " + automationId);
        try { el.Focus(); } catch { }
        var amount = string.Equals(direction, "up", StringComparison.OrdinalIgnoreCase) ? 3 : -3;
        Mouse.Scroll(amount);
        _isDirty = true;
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
    /// Also search the active window of the same process if not found in the main window.
    /// </summary>
    private AutomationElement? FindElement(string identifier)
    {
        // Strategy 1: Search in target window
        var el = SearchInWindow(_window, identifier);
        if (el is not null) return el;

        // Strategy 2: Fallback to the active modal/foreground window if it belongs to the same process
        try
        {
            var desktop = _automation!.GetDesktop();
            var activeWindow = desktop.FindFirstChild(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window))?.AsWindow();
            if (activeWindow != null && activeWindow.Properties.ProcessId.Value == _window!.Properties.ProcessId.Value)
            {
                el = SearchInWindow(activeWindow, identifier);
                if (el is not null) return el;
            }
        }
        catch { }

        return null;
    }

    private AutomationElement? SearchInWindow(AutomationElement? win, string identifier)
    {
        if (win is null) return null;
        try
        {
            var el = win.FindFirstDescendant(cf => cf.ByAutomationId(identifier));
            if (el is not null) return el;
            
            el = win.FindFirstDescendant(cf => cf.ByName(identifier));
            return el;
        }
        catch
        {
            return null;
        }
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
