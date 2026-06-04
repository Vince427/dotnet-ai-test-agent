using System.Diagnostics;
using DesktopAiTestAgent.AgentRunner.Dashboard;

namespace DesktopAiTestAgent.AgentRunner.Tests;

/// <summary>
/// Deterministic coverage of the dashboard run queue (bounded concurrency) without spawning
/// real processes: a fake job manager overrides the process-start seam, so we test the
/// scheduling decisions only.
/// </summary>
public sealed class RunJobQueueTests
{
    /// <summary>Records launches and pretends they started, instead of spawning a real CLI.</summary>
    private sealed class FakeJobManager(string root) : RunJobManager(root)
    {
        internal override void BeginProcess(RunJob job, ProcessStartInfo psi)
        {
            job.Pid = 4242; // Status is already "running" (set by Pump); we just don't spawn.
        }
    }

    [Fact]
    public void BoundedQueue_RunsUpToMax_ThenStartsQueuedOnExit()
    {
        var m = new FakeJobManager("C:\\repo") { MaxConcurrency = 2 };

        var a = m.Launch("p", "A", null);
        var b = m.Launch("p", "B", null);
        var c = m.Launch("p", "C", null);

        Assert.Equal("running", a.Status);
        Assert.Equal("running", b.Status);
        Assert.Equal("queued", c.Status); // over the cap → waits

        m.OnProcessExited(a, 0);

        Assert.Equal("exited", a.Status);
        Assert.Equal(0, a.ExitCode);
        Assert.Equal("running", c.Status); // the freed slot pumped the queue
    }

    [Fact]
    public void RaisingMaxConcurrency_StartsQueuedJobsImmediately()
    {
        var m = new FakeJobManager("C:\\repo") { MaxConcurrency = 1 };
        var a = m.Launch("p", "A", null);
        var b = m.Launch("p", "B", null);

        Assert.Equal("running", a.Status);
        Assert.Equal("queued", b.Status);

        m.MaxConcurrency = 2; // a slot opens → b starts

        Assert.Equal("running", b.Status);
    }

    [Fact]
    public void MaxConcurrency_IsClampedToSaneRange()
    {
        var m = new FakeJobManager("C:\\repo");
        m.MaxConcurrency = 0;
        Assert.Equal(1, m.MaxConcurrency);
        m.MaxConcurrency = 999;
        Assert.Equal(16, m.MaxConcurrency);
    }
}
