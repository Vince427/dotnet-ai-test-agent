using System.Threading.Tasks;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner;

/// <summary>
/// Abstraction over the "decide" step of the agent loop: given the current UI
/// state, the goal, and accumulated memory, produce the next action.
///
/// Extracting this lets <see cref="RunOrchestrator"/> run against a scripted
/// decider in tests (no LLM key, no network), while production uses
/// <see cref="LlmService"/>.
/// </summary>
public interface IActionDecider
{
    /// <summary>
    /// Decides the next action for the agent given the observed UI snapshot.
    /// </summary>
    Task<AgentAction> DecideActionAsync(
        UiSnapshot snapshot,
        AgentGoal goal,
        string memoryContext,
        string? loopWarning = null);
}
