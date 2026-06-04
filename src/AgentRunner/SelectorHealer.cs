using System;
using System.Collections.Generic;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner;

/// <summary>
/// A proposed replacement for a selector (AutomationId/Name) that wasn't found in the live UI —
/// the evidence half of V8 self-healing. Recorded on the failing step; the runner NEVER applies
/// it automatically (CI must stay deterministic). A human or a later local-only `--heal-apply`
/// step decides whether to adopt it.
/// </summary>
public sealed class HealingSuggestion
{
    public string OldTarget { get; set; } = "";
    public string NewTarget { get; set; } = "";
    public string? NewName { get; set; }
    public string ControlType { get; set; } = "Unknown";

    /// <summary>0-100 similarity confidence (normalized edit distance + identifier-vs-name bonus).</summary>
    public int Confidence { get; set; }
    public string Rationale { get; set; } = "";
}

/// <summary>
/// Deterministic selector healing: when an action targets an AutomationId/Name that isn't in the
/// snapshot (selector drift), proposes the closest present element by normalized edit-distance
/// over its identifier and name. Pure and key-free — no LLM, no vision — so it is unit-testable
/// and safe to run on every failed target. Vision (`VisionActionDecider`) is the heavier,
/// optional escalation; this is the cheap always-on suggestion.
/// </summary>
public static class SelectorHealer
{
    /// <summary>Below this normalized similarity we don't guess — returns null rather than noise.</summary>
    private const double MinRatio = 0.6;

    public static HealingSuggestion? Suggest(string? failedTarget, UiSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(failedTarget) || snapshot?.Elements == null)
            return null;

        var target = Normalize(failedTarget!);
        if (target.Length == 0)
            return null;

        UiElement? best = null;
        var bestRatio = 0.0;
        var bestVia = "";

        foreach (var el in snapshot.Elements)
        {
            // Score against both the AutomationId and the Name; keep the stronger of the two.
            var (ratioId, _) = Score(target, el.AutomationId);
            var (ratioName, _) = Score(target, el.Name);

            var ratio = ratioId;
            var via = "automationId";
            if (ratioName > ratio) { ratio = ratioName; via = "name"; }

            if (ratio > bestRatio)
            {
                bestRatio = ratio;
                best = el;
                bestVia = via;
            }
        }

        if (best == null || bestRatio < MinRatio)
            return null;

        var newTarget = !string.IsNullOrEmpty(best.AutomationId) ? best.AutomationId! : (best.Name ?? "");
        var confidence = (int)Math.Round(bestRatio * 100);
        return new HealingSuggestion
        {
            OldTarget = failedTarget!,
            NewTarget = newTarget,
            NewName = best.Name,
            ControlType = best.ControlType,
            Confidence = confidence,
            Rationale = $"'{failedTarget}' not found; closest present element is '{newTarget}'" +
                        (string.IsNullOrEmpty(best.Name) ? "" : $" (\"{best.Name}\")") +
                        $" [{best.ControlType}], {confidence}% match by {bestVia}."
        };
    }

    private static (double ratio, int distance) Score(string normalizedTarget, string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            return (0, int.MaxValue);

        var c = Normalize(candidate!);
        if (c.Length == 0)
            return (0, int.MaxValue);

        var dist = Levenshtein(normalizedTarget, c);
        var maxLen = Math.Max(normalizedTarget.Length, c.Length);
        var ratio = maxLen == 0 ? 0 : 1.0 - (double)dist / maxLen;
        return (ratio, dist);
    }

    /// <summary>Lowercase + strip non-alphanumerics, so "btn_Login" ~ "btnLogin" ~ "BTNLOGIN".</summary>
    private static string Normalize(string s)
    {
        var chars = new List<char>(s.Length);
        foreach (var ch in s)
            if (char.IsLetterOrDigit(ch))
                chars.Add(char.ToLowerInvariant(ch));
        return new string(chars.ToArray());
    }

    private static int Levenshtein(string a, string b)
    {
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        var prev = new int[b.Length + 1];
        var curr = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) prev[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            curr[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(Math.Min(curr[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
            }
            (prev, curr) = (curr, prev);
        }
        return prev[b.Length];
    }
}
