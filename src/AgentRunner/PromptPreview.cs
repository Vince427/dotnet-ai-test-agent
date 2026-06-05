using System.Collections.Generic;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner;

/// <summary>
/// Renders the exact prompt the LLM would receive for a test (V7 prompt preview), without running
/// anything. Key-free — it reuses <see cref="PromptBuilder"/>, so the preview can't drift from the
/// real runtime prompt. The live UI snapshot is injected at runtime; here it is a labelled
/// placeholder so the static framing (goal, category, allowed actions, success condition, workflow
/// policy) is fully visible for review before a run.
/// </summary>
public static class PromptPreview
{
    public static string BuildForTest(TestDefinition test, SecretRedactor? redactor = null, string? promptTemplate = null)
    {
        var r = redactor ?? new SecretRedactor();
        var goal = test.ToAgentGoal();
        var snapshot = new UiSnapshot(
            string.IsNullOrWhiteSpace(test.TargetWindow) ? "(target window resolved at runtime)" : test.TargetWindow!,
            new List<UiElement>
            {
                new() { ControlType = "Text", Name = "(the live UI elements are captured at runtime)" }
            },
            statusText: "(captured live at runtime)");

        return new PromptBuilder(r, promptTemplate).Build(snapshot, goal, memoryContext: "(empty at the start of a run)");
    }
}
