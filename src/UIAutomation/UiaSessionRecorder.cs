using System;
using DesktopAiTestAgent.Core;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Identifiers;
using FlaUI.Core.Tools;
using FlaUI.UIA3;

namespace DesktopAiTestAgent.UIAutomation;

/// <summary>
/// Live UIA event source (V9.5 recording mode, increment 2b — the env-bound half). Attaches to a
/// window by title via FlaUI/UIA3, subscribes to UI Automation events on its subtree, and translates
/// each into a framework-agnostic <see cref="CapturedUiEvent"/> that it hands to the pure
/// <c>SessionRecorder</c> sink (this assembly does not reference the recorder type — the caller wires
/// the sink so the mapping/smoothing stays unit-testable without a desktop):
/// <list type="bullet">
///   <item>InvokePattern Invoked → <see cref="UiEventKind.Invoked"/> (replays as a click).</item>
///   <item>ValuePattern Value property change → <see cref="UiEventKind.ValueChanged"/> (carries the new value).</item>
///   <item>TogglePattern ToggleState property change → <see cref="UiEventKind.Toggled"/>.</item>
///   <item>SelectionItem ElementSelected → <see cref="UiEventKind.SelectionChanged"/>.</item>
/// </list>
/// CRITICAL secret-safety: a typed value is redacted <em>at capture</em> (via the injected
/// <see cref="ValueRedactor"/>, normally <c>SecretRedactor.RedactValueForIdentifier</c>) before it ever
/// enters a <see cref="CapturedUiEvent"/> — so a password never lands in <c>session.json</c> on disk.
/// This is env-bound and can only be exercised on a real interactive desktop.
/// </summary>
public sealed class UiaSessionRecorder : IDisposable
{
    /// <summary>Redacts a captured value given the source control's identifier (AutomationId/Name)
    /// and its UIA IsPassword flag. Returns the value to store; returns the redacted placeholder for
    /// password controls or sensitive identifiers.</summary>
    public delegate string? ValueRedactor(string? identifier, bool isPassword, string? value);

    private readonly Action<CapturedUiEvent> _sink;
    private readonly ValueRedactor _redact;
    private readonly Action<string>? _diagnostics;

    private UIA3Automation? _automation;
    private Window? _window;

    /// <param name="sink">Receives each captured event (typically <c>SessionRecorder.Observe</c>).</param>
    /// <param name="redact">Value redactor applied at capture (typically
    /// <c>SecretRedactor.RedactValueForIdentifier</c>). When null, values are passed through unchanged —
    /// callers in the runner always pass the real redactor.</param>
    /// <param name="diagnostics">Optional sink for human-readable diagnostics (write to stderr).</param>
    public UiaSessionRecorder(
        Action<CapturedUiEvent> sink,
        ValueRedactor? redact = null,
        Action<string>? diagnostics = null)
    {
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _redact = redact ?? ((_, __, v) => v);
        _diagnostics = diagnostics;
    }

    /// <summary>The window title once attached (null until <see cref="Attach"/> succeeds).</summary>
    public string? WindowTitle { get; private set; }

    /// <summary>
    /// Attaches to the window whose Name matches <paramref name="windowTitle"/> and subscribes to its
    /// automation events. Returns false if the window can't be found within the timeout.
    /// </summary>
    public bool Attach(string windowTitle, TimeSpan timeout)
    {
        _automation = new UIA3Automation();
        var desktop = _automation.GetDesktop();

        var retry = Retry.WhileNull(
            () => desktop.FindFirstDescendant(cf => cf.ByName(windowTitle))?.AsWindow(),
            timeout,
            TimeSpan.FromMilliseconds(300));

        _window = retry.Result;
        if (_window == null)
            return false;

        WindowTitle = SafeGet(() => _window.Title) ?? windowTitle;
        Subscribe(_automation, _window);
        return true;
    }

    private void Subscribe(AutomationBase automation, Window window)
    {
        var events = automation.EventLibrary;
        var props = automation.PropertyLibrary;

        // Invoke (buttons / menu items) → Invoked.
        TrySubscribe("Invoke", () => window.RegisterAutomationEvent(
            events.Invoke.InvokedEvent,
            TreeScope.Descendants,
            (src, _) => Emit(src, UiEventKind.Invoked, null)));

        // Selection (list / combo item) → SelectionChanged.
        TrySubscribe("SelectionItem", () => window.RegisterAutomationEvent(
            events.SelectionItem.ElementSelectedEvent,
            TreeScope.Descendants,
            (src, _) => Emit(src, UiEventKind.SelectionChanged, null)));

        // Property changes: Value (edits) and ToggleState (checkboxes/toggles). UIA has no dedicated
        // "toggled"/"value changed" automation event — both surface as property-changed notifications.
        TrySubscribe("PropertyChanged(Value,ToggleState)", () => window.RegisterPropertyChangedEvent(
            TreeScope.Descendants,
            (src, propId, newValue) => OnPropertyChanged(props, src, propId, newValue),
            props.Value.Value,
            props.Toggle.ToggleState));
    }

    private void OnPropertyChanged(IPropertyLibrary props, AutomationElement src, PropertyId propId, object? newValue)
    {
        if (Equals(propId, props.Value.Value))
            Emit(src, UiEventKind.ValueChanged, newValue?.ToString());
        else if (Equals(propId, props.Toggle.ToggleState))
            Emit(src, UiEventKind.Toggled, null);
    }

    private void Emit(AutomationElement src, UiEventKind kind, string? rawValue)
    {
        try
        {
            var automationId = SafeGet(() => src.Properties.AutomationId.ValueOrDefault);
            var name = SafeGet(() => src.Properties.Name.ValueOrDefault);
            var controlType = SafeGet(() => src.Properties.ControlType.ValueOrDefault.ToString());
            // IsPassword is the primary secret signal (a masked field may have a non-keyword id).
            var isPassword = SafeGetBool(() => src.Properties.IsPassword.ValueOrDefault);

            // Secret-safety: redact the value at capture — keyed by IsPassword first, then the source
            // identifier — BEFORE it can enter the CapturedUiEvent / session.json. Never store raw text.
            string? safeValue = kind == UiEventKind.ValueChanged
                ? _redact(string.IsNullOrEmpty(automationId) ? name : automationId, isPassword, rawValue)
                : null;

            _sink(new CapturedUiEvent
            {
                Kind = kind,
                AutomationId = automationId,
                Name = name,
                ControlType = controlType,
                Value = safeValue
            });
        }
        catch (Exception ex)
        {
            // A disposed/stale element can throw while we read it; never let a single noisy event
            // tear down the recording session.
            _diagnostics?.Invoke($"Skipped a {kind} event: {ex.Message}");
        }
    }

    private void TrySubscribe(string label, Func<object> register)
    {
        try
        {
            register();
        }
        catch (Exception ex)
        {
            // Some frameworks/controls don't raise every event; degrade gracefully rather than abort.
            _diagnostics?.Invoke($"Could not subscribe to {label} events: {ex.Message}");
        }
    }

    private static string? SafeGet(Func<string?> getter)
    {
        try { return getter(); } catch { return null; }
    }

    private static bool SafeGetBool(Func<bool> getter)
    {
        try { return getter(); } catch { return false; }
    }

    public void Dispose()
    {
        // Disposing the automation tears down all registered handlers.
        _automation?.Dispose();
        _automation = null;
        _window = null;
    }
}
