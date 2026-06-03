using System;

namespace DesktopAiTestAgent.AgentRunner.Tests;

/// <summary>
/// Theory counterpart of <see cref="InteractiveUiFactAttribute"/>: a data-driven
/// interactive desktop E2E that runs only when <c>RUN_E2E_UI=1</c> is set, and
/// otherwise reports as Skipped. Used to run the same scripted flow across more than
/// one desktop framework (WinForms, WPF) from a single test body.
/// </summary>
public sealed class InteractiveUiTheoryAttribute : TheoryAttribute
{
    public InteractiveUiTheoryAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RUN_E2E_UI")))
        {
            Skip = "Interactive desktop E2E. Set RUN_E2E_UI=1 on a real Windows " +
                   "session (with the sample apps built) to run.";
        }
    }
}
