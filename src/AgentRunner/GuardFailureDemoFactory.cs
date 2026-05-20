using System;
using System.Collections.Generic;
using System.Linq;

namespace DesktopAiTestAgent.AgentRunner;

public sealed class GuardFailureDemoWriteResult
{
    public string OutputRoot { get; set; } = "";
    public List<RunArtifact> Artifacts { get; set; } = [];
}

public static class GuardFailureDemoFactory
{
    public static IReadOnlyList<RunArtifact> CreateAll(DateTime? startedAt = null)
    {
        var start = startedAt ?? DateTime.UtcNow;
        return
        [
            MissingTarget(start),
            CrashOrClosedWindow(start.AddSeconds(1)),
            EmptyUiTree(start.AddSeconds(2)),
            UnexpectedModal(start.AddSeconds(3))
        ];
    }

    public static GuardFailureDemoWriteResult WriteAll(string outputRoot)
    {
        var writer = new ArtifactWriter(outputRoot);
        var artifacts = CreateAll().ToList();
        foreach (var artifact in artifacts)
        {
            writer.WriteReport(artifact);
            writer.WriteSummary(artifact);
        }

        return new GuardFailureDemoWriteResult
        {
            OutputRoot = outputRoot,
            Artifacts = artifacts
        };
    }

    private static RunArtifact MissingTarget(DateTime startedAt)
    {
        return CreateArtifact(
            runId: "guard-demo-missing-target",
            testId: "GUARD-MISSING-TARGET-001",
            title: "Guard demo: missing action target",
            result: "Failed",
            finalScore: -5,
            errorMessage: "The action target was not present in the latest UI snapshot.",
            startedAt,
            new RunStep
            {
                StepNumber = 1,
                UiStateSnapshot = "Sample Login App (.NET 8) (3 elements)",
                ActionType = "Click",
                ActionTarget = "btnDoesNotExist",
                Outcome = "Failed",
                FailureCode = "action_target_not_found",
                FailureMessage = "Action target 'btnDoesNotExist' was not present in the latest UI snapshot.",
                ScoreDelta = -5,
                CumulativeScore = -5
            });
    }

    private static RunArtifact CrashOrClosedWindow(DateTime startedAt)
    {
        return CreateArtifact(
            runId: "guard-demo-crash-or-closed-window",
            testId: "GUARD-CRASH-001",
            title: "Guard demo: crashed or closed target",
            result: "Aborted",
            finalScore: -55,
            errorMessage: "Could not capture the UI state after the action; the target may have crashed or closed.",
            startedAt,
            new RunStep
            {
                StepNumber = 1,
                UiStateSnapshot = "Sample Login App (.NET 8) (12 elements)",
                ActionType = "Click",
                ActionTarget = "btnCrashDemo",
                Outcome = "Failed",
                FailureCode = "uia_capture_failed",
                FailureMessage = "Could not capture the UI state after the action; the target may have crashed or closed.",
                GuardStatus = "Abort",
                GuardCode = "uia_capture_failed",
                GuardMessage = "Could not capture the UI state after the action: target window disappeared.",
                ScoreDelta = -55,
                CumulativeScore = -55
            });
    }

    private static RunArtifact EmptyUiTree(DateTime startedAt)
    {
        return CreateArtifact(
            runId: "guard-demo-empty-ui-tree",
            testId: "GUARD-EMPTY-UI-001",
            title: "Guard demo: empty UI Automation tree",
            result: "Failed",
            finalScore: -25,
            errorMessage: "The UI Automation tree became empty after the action.",
            startedAt,
            new RunStep
            {
                StepNumber = 1,
                UiStateSnapshot = "Sample Login App (.NET 8) (9 elements)",
                ActionType = "Click",
                ActionTarget = "btnRefresh",
                Outcome = "Failed",
                FailureCode = "uia_tree_empty",
                FailureMessage = "The UI Automation tree became empty after the action.",
                GuardStatus = "ForceReject",
                GuardCode = "uia_tree_empty",
                GuardMessage = "The UI Automation tree became empty after the action.",
                ScoreDelta = -25,
                CumulativeScore = -25
            });
    }

    private static RunArtifact UnexpectedModal(DateTime startedAt)
    {
        return CreateArtifact(
            runId: "guard-demo-unexpected-modal",
            testId: "GUARD-UNEXPECTED-MODAL-001",
            title: "Guard demo: unexpected modal",
            result: "Failed",
            finalScore: -25,
            errorMessage: "An unexpected modal appeared and blocked the intended workflow.",
            startedAt,
            new RunStep
            {
                StepNumber = 1,
                UiStateSnapshot = "Sample Login App (.NET 8) (16 elements)",
                ActionType = "Click",
                ActionTarget = "btnSaveProfile",
                Outcome = "Failed",
                FailureCode = "unexpected_modal_detected",
                FailureMessage = "A modal or confirmation prompt appeared but was not expected by the current scenario.",
                GuardStatus = "ForceReject",
                GuardCode = "unexpected_modal_detected",
                GuardMessage = "A modal or confirmation prompt appeared but was not expected by the current scenario.",
                ScoreDelta = -25,
                CumulativeScore = -25
            });
    }

    private static RunArtifact CreateArtifact(
        string runId,
        string testId,
        string title,
        string result,
        int finalScore,
        string errorMessage,
        DateTime startedAt,
        RunStep step)
    {
        step.Timestamp = startedAt.AddMilliseconds(250);

        return new RunArtifact
        {
            RunId = runId,
            EvidenceLevel = EvidenceLevel.Minimal,
            GoalIdentifier = testId.ToLowerInvariant(),
            GoalDescription = title,
            TestId = testId,
            TestTitle = title,
            TestPriority = "P1",
            Framework = "winforms/wpf",
            Suite = "guard-demos",
            TargetWindow = "Sample Login App (.NET 8) or WPF AI Test Target",
            StartedAt = startedAt,
            EndedAt = startedAt.AddSeconds(1),
            Result = result,
            FinalScore = finalScore,
            ErrorMessage = errorMessage,
            Steps = [step]
        };
    }
}
