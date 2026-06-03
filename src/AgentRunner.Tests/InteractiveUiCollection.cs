namespace DesktopAiTestAgent.AgentRunner.Tests;

/// <summary>
/// Serializes all interactive desktop E2E tests. They share a single interactive
/// session (one foreground, one UIA COM apartment, and sample windows with the same
/// title), so running two of them in parallel races on the desktop and throws COM
/// E_FAIL. xUnit runs everything in one collection on a single thread, so assigning
/// every UI test class to this collection forces them to run one at a time.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class InteractiveUiCollection
{
    public const string Name = "InteractiveUi";
}
