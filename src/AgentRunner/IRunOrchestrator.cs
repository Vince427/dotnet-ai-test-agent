using System.Threading.Tasks;

namespace DesktopAiTestAgent.AgentRunner;

/// <summary>
/// Drives one runtime test run: attach to the target window, then loop
/// observe → decide → act → score → record until the goal is reached, the run
/// is aborted, or max steps are exhausted.
///
/// Returns the process exit code. The produced <see cref="RunArtifact"/> is
/// exposed via <see cref="LastArtifact"/> so callers (and tests) can inspect the
/// outcome without re-reading <c>report.json</c> from disk.
/// </summary>
public interface IRunOrchestrator
{
    Task<int> RunAsync(RunnerOptions options);

    RunArtifact? LastArtifact { get; }
}
