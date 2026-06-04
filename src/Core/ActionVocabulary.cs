using System;
using System.Collections.Generic;

namespace DesktopAiTestAgent.Core;

/// <summary>
/// Single source of truth for the agent loop's action verbs. Before this existed the
/// vocabulary was duplicated across four places that could silently drift apart: the act
/// dispatch (<c>RunOrchestrator</c>/<c>ActionExecutor</c>), the prompt's default "Allowed
/// actions" line (<c>PromptBuilder</c>), plan validation (<c>TestPlanValidator</c>), and
/// agent-action target validation (<c>AgentActionValidator</c>). They all derive from
/// <see cref="All"/> now.
/// </summary>
public static class ActionVocabulary
{
    public const string EnterText = "EnterText";
    public const string Click = "Click";
    public const string DoubleClick = "DoubleClick";
    public const string Scroll = "Scroll";
    public const string Wait = "Wait";
    public const string Assert = "Assert";
    public const string Done = "Done";
    public const string Explore = "Explore";

    /// <summary>Every verb the loop understands, in the order shown to the model.</summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        EnterText, Click, DoubleClick, Scroll, Wait, Assert, Done, Explore
    };

    /// <summary>Verbs that act on a specific control and therefore need a target id/name.</summary>
    private static readonly HashSet<string> TargetRequired =
        new(new[] { EnterText, Click, DoubleClick, Scroll, Assert }, StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> KnownSet =
        new(All, StringComparer.OrdinalIgnoreCase);

    /// <summary>Case-insensitive membership of the known verbs (used by plan validation).</summary>
    public static bool IsKnown(string? actionType) =>
        !string.IsNullOrWhiteSpace(actionType) && KnownSet.Contains(actionType!);

    /// <summary>True when the verb operates on a specific control.</summary>
    public static bool RequiresTarget(string? actionType) =>
        !string.IsNullOrWhiteSpace(actionType) && TargetRequired.Contains(actionType!);

    /// <summary>Case-insensitive verb comparison helper, so call sites stop repeating it.</summary>
    public static bool Is(string? actionType, string verb) =>
        string.Equals(actionType, verb, StringComparison.OrdinalIgnoreCase);

    /// <summary>The default "Allowed actions: …" list shown when a test lists none.</summary>
    public static string DefaultAllowedList => string.Join(", ", All);
}
