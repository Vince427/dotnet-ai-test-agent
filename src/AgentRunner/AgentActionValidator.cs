using System;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner;

public sealed class AgentActionValidationResult
{
    private AgentActionValidationResult(bool isValid, string? code, string? message)
    {
        IsValid = isValid;
        Code = code;
        Message = message;
    }

    public bool IsValid { get; }
    public string? Code { get; }
    public string? Message { get; }

    public static AgentActionValidationResult Valid() => new(true, null, null);

    public static AgentActionValidationResult Invalid(string code, string message) => new(false, code, message);
}

public static class AgentActionValidator
{
    public static AgentActionValidationResult ValidateTargetExists(AgentAction action, UiSnapshot snapshot)
    {
        if (!RequiresTarget(action.ActionType))
            return AgentActionValidationResult.Valid();

        var target = action.AutomationId;
        if (string.IsNullOrWhiteSpace(target))
        {
            return AgentActionValidationResult.Invalid(
                "missing_action_target",
                $"Action '{action.ActionType}' requires an automationId or Name target.");
        }

        var validatedTarget = target!;
        foreach (var element in snapshot.Elements)
        {
            if (MatchesIdentifier(element.AutomationId, validatedTarget) ||
                MatchesIdentifier(element.Name, validatedTarget))
            {
                return AgentActionValidationResult.Valid();
            }
        }

        return AgentActionValidationResult.Invalid(
            "action_target_not_found",
            $"Action target '{validatedTarget}' was not present in the latest UI snapshot.");
    }

    private static bool RequiresTarget(string? actionType) => ActionVocabulary.RequiresTarget(actionType);

    private static bool MatchesIdentifier(string? candidate, string target)
    {
        return !string.IsNullOrWhiteSpace(candidate) &&
               string.Equals(candidate, target, StringComparison.OrdinalIgnoreCase);
    }
}
