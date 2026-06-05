using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner;

/// <summary>
/// Redacts secret-like values before they enter prompts, memory, logs, or text artifacts.
/// </summary>
public sealed class SecretRedactor
{
    public const string RedactedValue = "[REDACTED]";

    private static readonly string[] DefaultSensitivePatterns =
    [
        "password",
        "secret",
        "token",
        "apikey",
        "api_key",
        "cvv",
        "ssn"
    ];

    private static readonly Regex SensitiveAssignmentRegex = new(
        "(?i)\\b(?<key>password|secret|token|api[_\\s-]?key|apikey|cvv|ssn)\\b(?<separator>\\s*[:=]\\s*)(?<value>\"[^\"]*\"|'[^']*'|[^\\s,;]+)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly string[] _normalizedSensitivePatterns;

    public SecretRedactor(IEnumerable<string>? sensitivePatterns = null)
    {
        var patterns = sensitivePatterns ?? DefaultSensitivePatterns;
        var normalized = new List<string>();
        foreach (var pattern in patterns)
        {
            var normalizedPattern = NormalizeIdentifier(pattern);
            if (!string.IsNullOrWhiteSpace(normalizedPattern) &&
                !normalized.Contains(normalizedPattern, StringComparer.OrdinalIgnoreCase))
            {
                normalized.Add(normalizedPattern);
            }
        }

        _normalizedSensitivePatterns = normalized.ToArray();
    }

    /// <summary>
    /// True if an element is sensitive: a UIA password control (any id), or an
    /// AutomationId/Name matching a sensitive pattern.
    /// </summary>
    public bool IsSensitiveElement(UiElement element) =>
        element.IsPassword ||
        IsSensitiveIdentifier(element.AutomationId) ||
        IsSensitiveIdentifier(element.Name);

    public bool IsSensitiveIdentifier(string? identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return false;

        var normalizedIdentifier = NormalizeIdentifier(identifier!);
        foreach (var pattern in _normalizedSensitivePatterns)
        {
            if (normalizedIdentifier.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    public string? RedactValueForIdentifier(string? identifier, string? value)
    {
        if (value is null)
            return null;

        return IsSensitiveIdentifier(identifier)
            ? RedactedValue
            : RedactText(value);
    }

    public string? RedactActionValue(AgentAction action)
    {
        return RedactValueForIdentifier(action.AutomationId, action.Value);
    }

    /// <summary>
    /// Redacts a captured value given the source control's identifier AND its UIA <c>IsPassword</c>
    /// flag. <c>IsPassword</c> is the primary signal (matching <see cref="IsSensitiveElement"/>): a
    /// masked field is redacted even when its AutomationId/Name carries no sensitive keyword (e.g.
    /// <c>pin</c>, an unlabeled password box). Otherwise falls back to identifier-based redaction.
    /// Used by the live recorder so a typed password never reaches <c>session.json</c>.
    /// </summary>
    public string? RedactValue(string? identifier, bool isPassword, string? value)
    {
        if (value is null)
            return null;

        return isPassword || IsSensitiveIdentifier(identifier)
            ? RedactedValue
            : RedactText(value);
    }

    public string? RedactText(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        return SensitiveAssignmentRegex.Replace(
            text,
            match => match.Groups["key"].Value + match.Groups["separator"].Value + RedactedValue);
    }

    public UiSnapshot RedactSnapshot(UiSnapshot snapshot)
    {
        var elements = new List<UiElement>(snapshot.Elements.Count);
        foreach (var element in snapshot.Elements)
        {
            elements.Add(new UiElement
            {
                AutomationId = element.AutomationId,
                Name = element.Name,
                ControlType = element.ControlType,
                Value = RedactElementValue(element),
                IsEnabled = element.IsEnabled,
                IsOffscreen = element.IsOffscreen,
                IsPassword = element.IsPassword,
                BoundingBox = element.BoundingBox
            });
        }

        return new UiSnapshot(
            RedactText(snapshot.WindowTitle) ?? snapshot.WindowTitle,
            elements,
            RedactText(snapshot.StatusText));
    }

    public string RedactSnapshotForPrompt(UiSnapshot snapshot)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Window: {RedactText(snapshot.WindowTitle)}");
        if (!string.IsNullOrEmpty(snapshot.StatusText))
            sb.AppendLine($"Status: {RedactText(snapshot.StatusText)}");
        sb.AppendLine("Elements:");
        foreach (var element in snapshot.Elements)
        {
            sb.AppendLine($"  - {FormatElementForPrompt(element)}");
        }

        return sb.ToString();
    }

    private string? RedactElementValue(UiElement element)
    {
        if (element.Value is null)
            return null;

        if (IsSensitiveElement(element))
            return RedactedValue;

        return RedactText(element.Value);
    }

    private string FormatElementForPrompt(UiElement element)
    {
        var id = !string.IsNullOrEmpty(element.AutomationId)
            ? element.AutomationId
            : element.Name ?? "(no id)";
        var value = RedactElementValue(element);
        var val = !string.IsNullOrEmpty(value) ? $" = \"{value}\"" : "";
        var state = element.IsEnabled ? "" : " [disabled]";
        return $"{element.ControlType} | {id}{val}{state}";
    }

    private static string NormalizeIdentifier(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch))
                sb.Append(char.ToLowerInvariant(ch));
        }

        return sb.ToString();
    }
}
