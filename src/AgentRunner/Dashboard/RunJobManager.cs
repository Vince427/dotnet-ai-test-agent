using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace DesktopAiTestAgent.AgentRunner.Dashboard;

/// <summary>
/// Launches agent runs by spawning the AgentRunner CLI as child processes (so the
/// dashboard reuses the exact CLI contract) and tracks their live output. Each run is an
/// independent process; launches go through a bounded queue so at most
/// <see cref="MaxConcurrency"/> run at once (UIA/desktop contention is real when many tests
/// target the same screen). Excess launches sit in <c>queued</c> status and start as slots free.
///
/// The child generates its own run id; we recover it by scanning stdout for the
/// logger's <c>session_id=&lt;runId&gt;-&lt;HHmmss&gt;</c> marker, which links a job to its
/// artifacts under <c>runs/&lt;runId&gt;/</c>.
/// </summary>
public class RunJobManager(string repoRoot) : IDisposable
{
    private static readonly Regex RunIdRegex =
        new(@"session_id=([0-9a-fA-F]{8})-\d{6}", RegexOptions.Compiled);

    private readonly string _repoRoot = repoRoot;
    private readonly ConcurrentDictionary<string, RunJob> _jobs = new();
    private readonly object _gate = new();
    private readonly Queue<PendingLaunch> _pending = new();
    private int _maxConcurrency = 2;

    /// <summary>Max runs allowed to execute at once (the rest queue). Clamped to [1, 16].</summary>
    public int MaxConcurrency
    {
        get { lock (_gate) return _maxConcurrency; }
        set
        {
            var v = value < 1 ? 1 : (value > 16 ? 16 : value);
            lock (_gate) _maxConcurrency = v;
            Pump(); // a raised cap may let queued jobs start now
        }
    }

    public RunJob Launch(string planPath, string testId, string? window) =>
        Enqueue(new RunJob
        {
            JobId = Guid.NewGuid().ToString("N")[..12],
            PlanPath = planPath,
            TestId = testId,
            Window = window,
            Status = "queued",
            StartedAt = DateTime.UtcNow
        }, () => BuildStartInfo(planPath, testId, window));

    /// <summary>
    /// Run a Symphony ticket via scripts/run-ticket-proof.ps1 — the SAME adapter CI uses,
    /// so the dashboard and CI share one path. The script invokes the CLI, whose logs carry
    /// the session id we correlate to a runId.
    /// </summary>
    public RunJob LaunchTicket(string ticketPath) =>
        Enqueue(new RunJob
        {
            JobId = Guid.NewGuid().ToString("N")[..12],
            PlanPath = ticketPath,
            TestId = Path.GetFileNameWithoutExtension(ticketPath),
            Status = "queued",
            StartedAt = DateTime.UtcNow
        }, () => BuildTicketStartInfo(ticketPath));

    private RunJob Enqueue(RunJob job, Func<ProcessStartInfo> psiFactory)
    {
        _jobs[job.JobId] = job;
        lock (_gate) _pending.Enqueue(new PendingLaunch(job, psiFactory));
        Pump();
        return job;
    }

    /// <summary>Starts queued jobs while a concurrency slot is free. Re-entrant-safe.</summary>
    private void Pump()
    {
        while (true)
        {
            PendingLaunch next;
            lock (_gate)
            {
                if (_pending.Count == 0) return;
                if (RunningCount() >= _maxConcurrency) return;
                next = _pending.Dequeue();
                next.Job.Status = "running"; // count it as running before we release the lock
            }

            try
            {
                BeginProcess(next.Job, next.PsiFactory());
            }
            catch (Exception ex)
            {
                next.Job.AppendLine("Failed to launch: " + ex.Message);
                next.Job.ExitCode = -1;
                next.Job.Status = "exited";
                // keep draining the queue
            }
        }
    }

    private int RunningCount() // call under _gate
    {
        var count = 0;
        foreach (var j in _jobs.Values)
            if (j.Status == "running") count++;
        return count;
    }

    /// <summary>
    /// Spawns the OS process for a dequeued job and wires its lifecycle. Overridable so the
    /// queue/concurrency logic can be unit-tested without spawning real processes.
    /// </summary>
    internal virtual void BeginProcess(RunJob job, ProcessStartInfo psi)
    {
        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => OnLine(job, e.Data);
        process.ErrorDataReceived += (_, e) => OnLine(job, e.Data);
        process.Exited += (_, _) =>
        {
            int? code = null;
            try { code = process.ExitCode; } catch { /* race on fast exit */ }
            process.Dispose();
            OnProcessExited(job, code);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        job.Pid = process.Id;
    }

    /// <summary>Marks a job finished and starts the next queued one. Test-visible.</summary>
    internal void OnProcessExited(RunJob job, int? exitCode)
    {
        if (exitCode.HasValue) job.ExitCode = exitCode;
        job.Status = "exited";
        Pump();
    }

    private ProcessStartInfo BuildTicketStartInfo(string ticketPath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell",
            WorkingDirectory = _repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        var script = Path.Combine(_repoRoot, "scripts", "run-ticket-proof.ps1");
        // launch_sample is read from the ticket frontmatter by the script, so we don't force it.
        var parts = new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script, "-TicketPath", ticketPath };
        psi.Arguments = string.Join(" ", parts.Select(QuoteArg));
        return psi;
    }

    public RunJob? Get(string jobId) => _jobs.TryGetValue(jobId, out var j) ? j : null;

    public IReadOnlyList<RunJob> Snapshot() =>
        _jobs.Values.OrderByDescending(j => j.StartedAt).ToList();

    private void OnLine(RunJob job, string? line)
    {
        if (line == null) return;
        job.AppendLine(line);

        if (job.RunId == null)
        {
            var match = RunIdRegex.Match(line);
            if (match.Success)
                job.RunId = match.Groups[1].Value;
        }
    }

    private ProcessStartInfo BuildStartInfo(string planPath, string testId, string? window)
    {
        var psi = new ProcessStartInfo
        {
            WorkingDirectory = _repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Reuse this exact build of the runner. On .NET it's a dll launched via the shared host; on
        // .NET Framework it's a directly-runnable exe. In a single-file publish, Assembly.Location is
        // empty — fall back to the running host process (the single-file exe), which re-enters the CLI.
#pragma warning disable IL3000 // Location is empty under single-file; the empty case is handled below.
        var assemblyPath = Assembly.GetExecutingAssembly().Location;
#pragma warning restore IL3000
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(assemblyPath) && assemblyPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            psi.FileName = "dotnet";
            parts.Add(assemblyPath);
        }
        else
        {
            psi.FileName = !string.IsNullOrEmpty(assemblyPath)
                ? assemblyPath
                : Process.GetCurrentProcess().MainModule?.FileName
                  ?? throw new InvalidOperationException("Cannot resolve the runner executable to launch.");
        }

        parts.Add("--plan");
        parts.Add(planPath);
        parts.Add("--test-id");
        parts.Add(testId);
        if (!string.IsNullOrWhiteSpace(window))
        {
            parts.Add("--window");
            parts.Add(window!);
        }

        // ProcessStartInfo.ArgumentList is unavailable on net48, so quote into Arguments.
        psi.Arguments = string.Join(" ", parts.Select(QuoteArg));
        return psi;
    }

    /// <summary>Windows command-line quoting for a single argument.</summary>
    internal static string QuoteArg(string arg)
    {
        if (arg.Length > 0 && arg.IndexOfAny([' ', '\t', '"']) < 0)
            return arg;

        var sb = new StringBuilder("\"");
        for (var i = 0; i < arg.Length; i++)
        {
            var backslashes = 0;
            while (i < arg.Length && arg[i] == '\\') { backslashes++; i++; }

            if (i == arg.Length) { sb.Append('\\', backslashes * 2); break; }
            if (arg[i] == '"') { sb.Append('\\', backslashes * 2 + 1).Append('"'); }
            else { sb.Append('\\', backslashes).Append(arg[i]); }
        }
        sb.Append('"');
        return sb.ToString();
    }

    public void Dispose()
    {
        // Best-effort: leave running child processes alone (they own their own runs);
        // we only drop our references.
        _jobs.Clear();
    }
}

/// <summary>A job waiting in the queue, with the factory that builds its process when a slot frees.</summary>
internal sealed class PendingLaunch(RunJob job, Func<ProcessStartInfo> psiFactory)
{
    public RunJob Job { get; } = job;
    public Func<ProcessStartInfo> PsiFactory { get; } = psiFactory;
}

/// <summary>A tracked launch job. Serialized to the dashboard as-is (camelCase).</summary>
public sealed class RunJob
{
    private readonly object _gate = new();
    private readonly List<string> _lines = new();

    public string JobId { get; set; } = "";
    public string PlanPath { get; set; } = "";
    public string TestId { get; set; } = "";
    public string? Window { get; set; }
    public int Pid { get; set; }
    public string? RunId { get; set; }
    public string Status { get; set; } = "running";
    public int? ExitCode { get; set; }
    public DateTime StartedAt { get; set; }

    /// <summary>Captured stdout/stderr lines (most recent tail returned to the UI).</summary>
    public IReadOnlyList<string> Logs
    {
        get { lock (_gate) return _lines.ToList(); }
    }

    internal void AppendLine(string line)
    {
        lock (_gate)
        {
            _lines.Add(line);
            // Keep memory bounded for long-running jobs.
            if (_lines.Count > 2000)
                _lines.RemoveRange(0, _lines.Count - 2000);
        }
    }
}
