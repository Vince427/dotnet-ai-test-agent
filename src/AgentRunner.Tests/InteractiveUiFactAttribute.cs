using System;

namespace DesktopAiTestAgent.AgentRunner.Tests;

/// <summary>
/// A <see cref="FactAttribute"/> that runs only when <c>RUN_E2E_UI=1</c> is set.
///
/// FlaUI / UI Automation requires a real interactive desktop session (a logged-in
/// Windows user, not a headless service), so the full-stack desktop E2E cannot run
/// in arbitrary CI. By default these tests report as <em>Skipped</em> (honest — not
/// a false green) and are opt-in for a local machine or an interactive Windows runner.
/// </summary>
public sealed class InteractiveUiFactAttribute : FactAttribute
{
    public InteractiveUiFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RUN_E2E_UI")))
        {
            Skip = "Interactive desktop E2E. Set RUN_E2E_UI=1 on a real Windows " +
                   "session (with the WinForms sample built) to run.";
        }
    }
}
