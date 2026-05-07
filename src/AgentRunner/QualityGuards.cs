using System;
using System.Collections.Generic;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner;

public enum QualityGuardStatus
{
    Passed,
    ForceReject,
    Abort
}

public sealed class QualityGuardResult
{
    public QualityGuardStatus Status { get; set; } = QualityGuardStatus.Passed;
    public string Code { get; set; } = "guard_passed";
    public string Message { get; set; } = "Quality guard passed.";

    public static QualityGuardResult Passed() => new();

    public static QualityGuardResult ForceReject(string code, string message)
    {
        return new QualityGuardResult
        {
            Status = QualityGuardStatus.ForceReject,
            Code = code,
            Message = message
        };
    }

    public static QualityGuardResult Abort(string code, string message)
    {
        return new QualityGuardResult
        {
            Status = QualityGuardStatus.Abort,
            Code = code,
            Message = message
        };
    }
}

public sealed class QualityGuardContext
{
    public int StepNumber { get; set; }
    public IAutomationDriver Driver { get; set; } = null!;
    public UiSnapshot SnapshotBefore { get; set; } = null!;
    public AgentAction Action { get; set; } = null!;
    public AgentGoal Goal { get; set; } = null!;
}

public interface IQualityGuard
{
    QualityGuardResult Check(QualityGuardContext context);
}

public sealed class QualityGuardEngine(IReadOnlyList<IQualityGuard> guards)
{
    public static QualityGuardEngine CreateDefault()
    {
        return new QualityGuardEngine([new UiTreeQualityGuard()]);
    }

    public QualityGuardResult Check(QualityGuardContext context)
    {
        foreach (var guard in guards)
        {
            var result = guard.Check(context);
            if (result.Status != QualityGuardStatus.Passed)
                return result;
        }

        return QualityGuardResult.Passed();
    }
}

public sealed class UiTreeQualityGuard : IQualityGuard
{
    public QualityGuardResult Check(QualityGuardContext context)
    {
        try
        {
            var snapshot = context.Driver.Capture();
            if (snapshot.Elements.Count == 0)
            {
                return QualityGuardResult.ForceReject(
                    "uia_tree_empty",
                    "The UI Automation tree became empty after the action.");
            }

            return QualityGuardResult.Passed();
        }
        catch (Exception ex)
        {
            return QualityGuardResult.Abort(
                "uia_capture_failed",
                "Could not capture the UI state after the action: " + ex.Message);
        }
    }
}
