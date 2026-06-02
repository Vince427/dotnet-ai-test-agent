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
/// dashboard reuses the exact CLI contract) and tracks their live output. Runs are
/// independent processes, so launching several is naturally parallel.
///
/// The child generates its own run id; we recover it by scanning stdout for the
/// logger's <c>session_id=&lt;runId&gt;-&lt;HHmmss&gt;</c> marker, which links a job to its
/// artifacts under <c>runs/&lt;runId&gt;/</c>.
/// </summary>
public sealed class RunJobManager(string repoRoot) : IDisposable
{
    private static readonly Regex RunIdRegex =
        new(@"session_id=([0-9a-fA-F]{8})-\d{6}", RegexOptions.Compiled);

    private readonly string _repoRoot = repoRoot;
    private readonly ConcurrentDictionary<string, RunJob> _jobs = new();

    public RunJob Launch(string planPath, string testId, string? window)
    {
        var job = new RunJob
        {
            JobId = Guid.NewGuid().ToString("N")[..12],
            PlanPath = planPath,
            TestId = testId,
            Window = window,
            Status = "running",
            StartedAt = DateTime.UtcNow
        };

        var psi = BuildStartInfo(planPath, testId, window);
        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        process.OutputDataReceived += (_, e) => OnLine(job, e.Data);
        process.ErrorDataReceived += (_, e) => OnLine(job, e.Data);
        process.Exited += (_, _) =>
        {
            job.Status = "exited";
            try { job.ExitCode = process.ExitCode; } catch { /* race on fast exit */ }
            process.Dispose();
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        job.Pid = process.Id;

        _jobs[job.JobId] = job;
        return job;
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

        // Reuse this exact build of the runner. On .NET it's a dll launched via the
        // shared host; on .NET Framework it's a directly-runnable exe.
        var assemblyPath = Assembly.GetExecutingAssembly().Location;
        var parts = new List<string>();
        if (assemblyPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            psi.FileName = "dotnet";
            parts.Add(assemblyPath);
        }
        else
        {
            psi.FileName = assemblyPath;
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
    private static string QuoteArg(string arg)
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
