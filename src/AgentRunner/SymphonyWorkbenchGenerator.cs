using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;

namespace DesktopAiTestAgent.AgentRunner;

public sealed class SymphonyWorkbenchOptions
{
    public string RepoRoot { get; set; } = Directory.GetCurrentDirectory();
    public string OutputPath { get; set; } = Path.Combine(Directory.GetCurrentDirectory(), "docs", "symphony.html");
    public List<string> PlanPaths { get; set; } = [];
    public string RunsRoot { get; set; } = Path.Combine(Directory.GetCurrentDirectory(), "runs");
}

public sealed class SymphonyWorkbenchResult
{
    public string OutputPath { get; set; } = "";
    public int TestCount { get; set; }
    public int RunCount { get; set; }
}

public static class SymphonyWorkbenchGenerator
{
    public static SymphonyWorkbenchResult Generate(SymphonyWorkbenchOptions options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));

        var repoRoot = Path.GetFullPath(options.RepoRoot);
        var outputPath = Path.GetFullPath(options.OutputPath);
        var runsRoot = Path.GetFullPath(options.RunsRoot);
        var planPaths = ResolvePlanPaths(repoRoot, options.PlanPaths);
        var tests = LoadTests(planPaths);
        var runs = LoadRuns(runsRoot);
        var html = RenderHtml(repoRoot, outputPath, planPaths, tests, runs);

        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir))
            Directory.CreateDirectory(outputDir);

        File.WriteAllText(outputPath, html, Encoding.UTF8);

        return new SymphonyWorkbenchResult
        {
            OutputPath = outputPath,
            TestCount = tests.Count,
            RunCount = runs.Count
        };
    }

    public static string RenderHtml(
        string repoRoot,
        string outputPath,
        IReadOnlyList<string> planPaths,
        IReadOnlyList<TestDefinition> tests,
        IReadOnlyList<RunArtifact> runs)
    {
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");
        var passed = runs.Count(r => IsResult(r, "Passed", "Succeeded"));
        var failed = runs.Count(r => IsResult(r, "Failed"));
        var blocked = runs.Count(r => IsResult(r, "Blocked"));
        var aborted = runs.Count(r => IsResult(r, "Aborted"));

        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"utf-8\">");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.AppendLine("  <title>Desktop AI Test Agent - AgentLoop Workbench</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine("    :root { color-scheme: light; --ink:#17202a; --muted:#667085; --line:#d0d7de; --bg:#f6f8fa; --panel:#ffffff; --accent:#0f766e; --bad:#b42318; --warn:#b54708; --ok:#067647; }");
        sb.AppendLine("    * { box-sizing: border-box; } body { margin:0; font:14px/1.45 Segoe UI, Arial, sans-serif; color:var(--ink); background:var(--bg); }");
        sb.AppendLine("    header { padding:24px 32px; background:#101828; color:white; } h1 { margin:0 0 4px; font-size:24px; } h2 { margin:28px 0 12px; font-size:18px; }");
        sb.AppendLine("    main { padding:24px 32px 40px; max-width:1280px; margin:0 auto; } .muted { color:var(--muted); }");
        sb.AppendLine("    .grid { display:grid; gap:12px; grid-template-columns:repeat(auto-fit,minmax(160px,1fr)); } .metric { background:var(--panel); border:1px solid var(--line); border-radius:6px; padding:14px; } .metric strong { display:block; font-size:24px; }");
        sb.AppendLine("    table { width:100%; border-collapse:collapse; background:var(--panel); border:1px solid var(--line); border-radius:6px; overflow:hidden; } th, td { padding:10px 12px; border-bottom:1px solid var(--line); text-align:left; vertical-align:top; } th { background:#eef2f6; font-weight:600; } tr:last-child td { border-bottom:0; }");
        sb.AppendLine("    code, .pill { font-family:Consolas, monospace; } .pill { display:inline-block; padding:2px 7px; border-radius:999px; background:#eef2f6; margin:0 4px 4px 0; white-space:nowrap; }");
        sb.AppendLine("    .ok { color:var(--ok); font-weight:600; } .bad { color:var(--bad); font-weight:600; } .warn { color:var(--warn); font-weight:600; } a { color:#175cd3; text-decoration:none; } a:hover { text-decoration:underline; }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <header>");
        sb.AppendLine("    <h1>AgentLoop Workbench</h1>");
        sb.AppendLine("    <div class=\"muted\">YAML is the source of truth. This page is a local read-only view over test specs and run artifacts.</div>");
        sb.AppendLine("  </header>");
        sb.AppendLine("  <main>");
        sb.AppendLine($"    <p class=\"muted\">Generated {Html(now)} from <code>{Html(repoRoot)}</code>.</p>");
        sb.AppendLine("    <section class=\"grid\">");
        Metric(sb, "Tests", tests.Count.ToString());
        Metric(sb, "Runs", runs.Count.ToString());
        Metric(sb, "Passed", passed.ToString(), "ok");
        Metric(sb, "Failed", failed.ToString(), "bad");
        Metric(sb, "Blocked", blocked.ToString(), "warn");
        Metric(sb, "Aborted", aborted.ToString(), "warn");
        sb.AppendLine("    </section>");

        sb.AppendLine("    <h2>Test Backlog</h2>");
        sb.AppendLine("    <table>");
        sb.AppendLine("      <thead><tr><th>ID</th><th>Title</th><th>Priority</th><th>Framework</th><th>Goal</th><th>Bounds</th></tr></thead>");
        sb.AppendLine("      <tbody>");
        foreach (var test in tests.OrderBy(t => t.Priority).ThenBy(t => t.Id))
        {
            sb.AppendLine("        <tr>");
            sb.AppendLine($"          <td><code>{Html(test.Id)}</code></td>");
            sb.AppendLine($"          <td>{Html(test.Title ?? "-")}</td>");
            sb.AppendLine($"          <td>{Html(test.Priority ?? "-")}</td>");
            sb.AppendLine($"          <td>{Html(test.Framework ?? "-")}</td>");
            sb.AppendLine($"          <td>{Html(test.Goal)}<br><span class=\"muted\">Success: {Html(test.SuccessCondition ?? "-")}</span></td>");
            sb.AppendLine($"          <td>max_steps={test.MaxSteps}<br>{RenderPills(test.AllowedActions)}</td>");
            sb.AppendLine("        </tr>");
        }
        sb.AppendLine("      </tbody>");
        sb.AppendLine("    </table>");

        sb.AppendLine("    <h2>Recent Runs</h2>");
        sb.AppendLine("    <table>");
        sb.AppendLine("      <thead><tr><th>Run</th><th>Test</th><th>Result</th><th>Score</th><th>Started</th><th>Target</th></tr></thead>");
        sb.AppendLine("      <tbody>");
        foreach (var run in runs.OrderByDescending(r => r.StartedAt).Take(50))
        {
            var runId = Html(run.RunId);
            sb.AppendLine("        <tr>");
            sb.AppendLine($"          <td><code>{runId}</code></td>");
            sb.AppendLine($"          <td>{Html(run.TestId ?? run.GoalIdentifier ?? "-")}</td>");
            sb.AppendLine($"          <td class=\"{ResultClass(run.Result)}\">{Html(run.Result)}</td>");
            sb.AppendLine($"          <td>{run.FinalScore}</td>");
            sb.AppendLine($"          <td>{Html(run.StartedAt.ToString("yyyy-MM-dd HH:mm:ss"))}</td>");
            sb.AppendLine($"          <td>{Html(run.TargetWindow ?? "-")}</td>");
            sb.AppendLine("        </tr>");
        }
        sb.AppendLine("      </tbody>");
        sb.AppendLine("    </table>");

        sb.AppendLine("    <h2>Loaded Plans</h2>");
        sb.AppendLine("    <ul>");
        foreach (var planPath in planPaths)
            sb.AppendLine($"      <li><code>{Html(Relative(repoRoot, planPath))}</code></li>");
        sb.AppendLine("    </ul>");
        sb.AppendLine("  </main>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    private static List<string> ResolvePlanPaths(string repoRoot, List<string> requestedPlanPaths)
    {
        if (requestedPlanPaths.Count > 0)
            return requestedPlanPaths.Select(p => Path.GetFullPath(Path.IsPathRooted(p) ? p : Path.Combine(repoRoot, p))).ToList();

        return TestPlanLoader.DiscoverPlanPaths(repoRoot);
    }

    private static List<TestDefinition> LoadTests(IEnumerable<string> planPaths)
    {
        var tests = new List<TestDefinition>();
        foreach (var planPath in planPaths)
        {
            if (!File.Exists(planPath))
                continue;

            tests.AddRange(TestPlanLoader.Load(planPath).Tests);
        }

        return tests;
    }

    private static List<RunArtifact> LoadRuns(string runsRoot)
    {
        if (!Directory.Exists(runsRoot))
            return [];

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var runs = new List<RunArtifact>();
        foreach (var reportPath in Directory.GetFiles(runsRoot, "report.json", SearchOption.AllDirectories))
        {
            try
            {
                var run = JsonSerializer.Deserialize<RunArtifact>(File.ReadAllText(reportPath), options);
                if (run != null)
                    runs.Add(run);
            }
            catch
            {
                // A broken historical run should not prevent rendering the workbench.
            }
        }

        return runs;
    }

    private static void Metric(StringBuilder sb, string label, string value, string? cssClass = null)
    {
        sb.AppendLine($"      <div class=\"metric\"><strong class=\"{Html(cssClass ?? "")}\">{Html(value)}</strong><span>{Html(label)}</span></div>");
    }

    private static bool IsResult(RunArtifact artifact, params string[] expected)
    {
        foreach (var value in expected)
        {
            if (string.Equals(artifact.Result, value, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string ResultClass(string result)
    {
        if (string.Equals(result, "Passed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(result, "Succeeded", StringComparison.OrdinalIgnoreCase))
            return "ok";
        if (string.Equals(result, "Failed", StringComparison.OrdinalIgnoreCase))
            return "bad";
        if (string.Equals(result, "Blocked", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(result, "Aborted", StringComparison.OrdinalIgnoreCase))
            return "warn";
        return "";
    }

    private static string RenderPills(IReadOnlyList<string> values)
    {
        if (values.Count == 0)
            return "<span class=\"muted\">all actions</span>";

        return string.Join("", values.Select(v => $"<span class=\"pill\">{Html(v)}</span>"));
    }

    private static string Relative(string root, string path)
    {
        try
        {
            var rootUri = new Uri(AppendDirectorySeparator(Path.GetFullPath(root)));
            var pathUri = new Uri(Path.GetFullPath(path));
            return Uri.UnescapeDataString(rootUri.MakeRelativeUri(pathUri).ToString())
                .Replace('/', Path.DirectorySeparatorChar);
        }
        catch
        {
            return path;
        }
    }

    private static string AppendDirectorySeparator(string path)
    {
        if (path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ||
            path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
        {
            return path;
        }

        return path + Path.DirectorySeparatorChar;
    }

    private static string Html(string? value) => WebUtility.HtmlEncode(value ?? "");
}
