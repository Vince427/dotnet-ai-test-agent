using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DesktopAiTestAgent.AgentRunner;

/// <summary>One selector a run's healing evidence says drifted, and its proposed replacement.</summary>
public sealed class SelectorReplacement
{
    public string Old { get; set; } = "";
    public string New { get; set; } = "";
    public int Confidence { get; set; }
}

/// <summary>The proposed application of a run's healing suggestions to a test's <c>selectors</c>.</summary>
public sealed class HealApplyPlan
{
    public string TestId { get; set; } = "";
    public List<SelectorReplacement> Replacements { get; } = [];
    public bool HasChanges => Replacements.Count > 0;
}

/// <summary>
/// V8 inc.2 — <c>--heal-apply</c>: turn a run's evidence-only selector-drift suggestions
/// (<see cref="HealingSuggestion"/> on the failed steps) into a confirmed rewrite of the test's
/// <c>selectors</c> inventory. Pure + deterministic here; the CLI does the local-only, confirmed file
/// write. The rewrite is **surgical** (only the <c>selectors:</c> line is touched, so comments and
/// every other field survive a re-emit's lossiness) and then **verified by <see cref="TestFactGuard"/>**
/// — if the edited YAML differs from the original in anything but <c>selectors</c>, the write is refused.
/// </summary>
public static class HealApplier
{
    /// <summary>
    /// Which of the test's declared <c>selectors</c> a run's healing suggestions would replace.
    /// Only selectors actually present in the test are proposed (no spurious additions); the highest
    /// confidence wins if a target drifted more than once.
    /// </summary>
    public static HealApplyPlan Plan(RunArtifact run, TestDefinition test)
    {
        if (run == null) throw new ArgumentNullException(nameof(run));
        if (test == null) throw new ArgumentNullException(nameof(test));

        var plan = new HealApplyPlan { TestId = test.Id };
        var present = new HashSet<string>(test.Selectors, StringComparer.Ordinal);

        // Best (highest-confidence) suggestion per drifted old target.
        var byOld = new Dictionary<string, HealingSuggestion>(StringComparer.Ordinal);
        foreach (var step in run.Steps)
        {
            var h = step.HealingSuggestion;
            if (h == null || string.IsNullOrEmpty(h.OldTarget) || string.IsNullOrEmpty(h.NewTarget))
                continue;
            if (!present.Contains(h.OldTarget) || h.OldTarget == h.NewTarget)
                continue;
            if (!byOld.TryGetValue(h.OldTarget, out var best) || h.Confidence > best.Confidence)
                byOld[h.OldTarget] = h;
        }

        foreach (var h in byOld.Values.OrderBy(h => h.OldTarget, StringComparer.Ordinal))
            plan.Replacements.Add(new SelectorReplacement { Old = h.OldTarget, New = h.NewTarget, Confidence = h.Confidence });

        return plan;
    }

    /// <summary>
    /// Apply the replacements to the YAML text, touching ONLY the <c>selectors</c> value (the inline
    /// <c>selectors: ["a", "b"]</c> form and the multiline <c>- a</c> form). Everything else — comments,
    /// other fields, formatting — is preserved verbatim. The caller re-parses + runs the fact-gate to
    /// confirm the edit changed nothing but the selectors before writing.
    /// </summary>
    public static string RewriteSelectorsInYaml(string yamlText, IReadOnlyList<SelectorReplacement> replacements)
    {
        if (yamlText == null) throw new ArgumentNullException(nameof(yamlText));
        if (replacements == null || replacements.Count == 0)
            return yamlText;

        var isCrlf = yamlText.Contains("\r\n");
        var lineSeparator = isCrlf ? "\r\n" : "\n";
        var lines = yamlText.Replace("\r\n", "\n").Split('\n');
        var inMultilineSelectors = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();

            // Enter/exit a multiline `selectors:` block.
            if (Regex.IsMatch(trimmed, @"^selectors\s*:\s*$"))
            {
                inMultilineSelectors = true;
                continue;
            }
            if (inMultilineSelectors)
            {
                if (trimmed.StartsWith("- ", StringComparison.Ordinal))
                {
                    lines[i] = ReplaceTokens(line, replacements);
                    continue;
                }
                inMultilineSelectors = false; // a non-item line ends the block
            }

            // Inline `selectors: [...]`.
            if (Regex.IsMatch(trimmed, @"^selectors\s*:\s*\["))
                lines[i] = ReplaceTokens(line, replacements);
        }

        return string.Join(lineSeparator, lines);
    }

    // Replace each Old with New only as a whole token (quoted or bare), so 'btn' doesn't hit 'btnLogin'.
    private static string ReplaceTokens(string line, IReadOnlyList<SelectorReplacement> replacements)
    {
        foreach (var r in replacements)
        {
            var old = Regex.Escape(r.Old);
            // bounded by quote, bracket, comma, whitespace, or line ends — never a substring of a longer id
            line = Regex.Replace(line, $@"(?<=[""'\[,\s]|^){old}(?=[""'\],\s]|$)", m => r.New);
        }
        return line;
    }
}
