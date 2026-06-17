using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DesktopAiTestAgent.AgentRunner;

public sealed class AgentLoopWorkbenchOptions
{
    public string RepoRoot { get; set; } = Directory.GetCurrentDirectory();
    public string OutputPath { get; set; } = Path.Combine(Directory.GetCurrentDirectory(), "docs", "agentloop.html");
    public List<string> PlanPaths { get; set; } = [];
    public string RunsRoot { get; set; } = Path.Combine(Directory.GetCurrentDirectory(), "runs");

    /// <summary>
    /// When &gt; 0, the generated HTML embeds a meta-refresh so the browser reloads
    /// every N seconds. Used by watch mode for hands-free near-real-time updates;
    /// left at 0 for CI / one-shot renders.
    /// </summary>
    public int AutoRefreshSeconds { get; set; }
}

public sealed class AgentLoopWorkbenchResult
{
    public string OutputPath { get; set; } = "";
    public int TestCount { get; set; }
    public int RunCount { get; set; }
}

public static class AgentLoopWorkbenchGenerator
{
    public static AgentLoopWorkbenchResult Generate(AgentLoopWorkbenchOptions options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));

        var repoRoot = Path.GetFullPath(options.RepoRoot);
        var outputPath = Path.GetFullPath(options.OutputPath);
        var runsRoot = Path.GetFullPath(options.RunsRoot);
        var planPaths = ResolvePlanPaths(repoRoot, options.PlanPaths);
        var (tests, warnings) = LoadTestsAndWarnings(planPaths);
        var runs = LoadRuns(runsRoot);
        var html = RenderHtml(repoRoot, outputPath, planPaths, tests, runs, options.AutoRefreshSeconds, warnings);

        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir))
            Directory.CreateDirectory(outputDir);

        File.WriteAllText(outputPath, html, Encoding.UTF8);

        return new AgentLoopWorkbenchResult
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
        IReadOnlyList<RunArtifact> runs,
        int autoRefreshSeconds = 0,
        IReadOnlyDictionary<string, List<string>>? warningsByTestId = null)
    {
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");
        var passed = runs.Count(r => IsResult(r, "Passed", "Succeeded"));
        var failed = runs.Count(r => IsResult(r, "Failed"));
        var blocked = runs.Count(r => IsResult(r, "Blocked"));
        var aborted = runs.Count(r => IsResult(r, "Aborted"));
        var total = runs.Count;
        var needsAttention = failed + aborted;

        var recentRuns = runs.OrderByDescending(r => r.StartedAt).Take(50).ToList();
        var dataIsland = BuildDataIsland(repoRoot, outputPath, recentRuns);

        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"utf-8\">");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        if (autoRefreshSeconds > 0)
            sb.AppendLine($"  <meta http-equiv=\"refresh\" content=\"{autoRefreshSeconds}\">");
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
        // Interactive layer styles
        sb.AppendLine("    .alert { background:#fef3f2; border:1px solid #fda29b; color:var(--bad); padding:12px 16px; border-radius:6px; margin:16px 0; font-weight:600; }");
        sb.AppendLine("    .bar { display:flex; height:10px; border-radius:999px; overflow:hidden; background:#eef2f6; margin:10px 0 4px; } .bar > i { display:block; height:100%; } .seg-ok{background:var(--ok);} .seg-bad{background:var(--bad);} .seg-warn{background:var(--warn);}");
        sb.AppendLine("    .controls { display:flex; gap:8px; align-items:center; flex-wrap:wrap; margin:8px 0 12px; } .controls input, .controls select { padding:6px 10px; border:1px solid var(--line); border-radius:6px; font:inherit; }");
        sb.AppendLine("    tr.run-row { cursor:pointer; } tr.run-row:hover { background:#f0f6ff; } tr.drill > td { background:#f9fafb; }");
        sb.AppendLine("    table.steps { margin:8px 0; } table.steps th, table.steps td { padding:6px 8px; font-size:13px; } .shots { display:flex; flex-wrap:wrap; gap:6px; margin-top:8px; } .shot img { height:96px; border:1px solid var(--line); border-radius:4px; display:block; }");
        sb.AppendLine("    .hidden { display:none !important; }");
        // Prompt-preview + warnings (V7 inc.2b)
        sb.AppendLine("    details.prompt > summary { cursor:pointer; color:#175cd3; font-size:13px; } details.prompt[open] > summary { margin-bottom:6px; }");
        sb.AppendLine("    pre.prompt { white-space:pre-wrap; word-break:break-word; max-width:460px; max-height:340px; overflow:auto; margin:0; background:#f9fafb; border:1px solid var(--line); border-radius:4px; padding:8px 10px; font:12px/1.45 Consolas, monospace; }");
        sb.AppendLine("    .notes { color:var(--warn); font-size:12.5px; line-height:1.5; } .notes .none { color:var(--muted); }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <header>");
        sb.AppendLine("    <h1>AgentLoop Workbench</h1>");
        sb.AppendLine("    <div class=\"muted\">YAML is the source of truth. This page is a local read-only view over test specs and run artifacts.</div>");
        sb.AppendLine("  </header>");
        sb.AppendLine("  <main>");
        sb.AppendLine($"    <p class=\"muted\">Generated {Html(now)} from <code>{Html(repoRoot)}</code>.</p>");

        if (needsAttention > 0)
            sb.AppendLine($"    <div class=\"alert\" data-alert=\"1\">&#9888; {needsAttention} run(s) need attention &mdash; {failed} failed, {aborted} aborted.</div>");

        sb.AppendLine("    <section class=\"grid\">");
        Metric(sb, "Tests", tests.Count.ToString());
        Metric(sb, "Runs", runs.Count.ToString());
        Metric(sb, "Passed", passed.ToString(), "ok");
        Metric(sb, "Failed", failed.ToString(), "bad");
        Metric(sb, "Blocked", blocked.ToString(), "warn");
        Metric(sb, "Aborted", aborted.ToString(), "warn");
        sb.AppendLine("    </section>");

        if (total > 0)
        {
            sb.AppendLine("    <div class=\"bar\" title=\"Pass rate\">");
            sb.AppendLine($"      <i class=\"seg-ok\" style=\"width:{Pct(passed, total)}%\"></i>");
            sb.AppendLine($"      <i class=\"seg-bad\" style=\"width:{Pct(failed, total)}%\"></i>");
            sb.AppendLine($"      <i class=\"seg-warn\" style=\"width:{Pct(blocked + aborted, total)}%\"></i>");
            sb.AppendLine("    </div>");
            sb.AppendLine($"    <p class=\"muted\">Pass rate: {Pct(passed, total)}% ({passed}/{total}).</p>");
        }

        sb.AppendLine("    <h2>Test Backlog</h2>");
        sb.AppendLine("    <p class=\"muted\">Notes are non-fatal policy advisories (same checks as <code>--validate-plan</code>). Prompt shows the exact LLM prompt this test would produce &mdash; key-free, the live UI snapshot is injected at runtime.</p>");
        sb.AppendLine("    <table>");
        sb.AppendLine("      <thead><tr><th>ID</th><th>Title</th><th>Priority</th><th>Framework</th><th>Goal</th><th>Bounds</th><th>Notes</th><th>Prompt</th></tr></thead>");
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
            sb.AppendLine($"          <td class=\"notes\">{RenderNotes(test, warningsByTestId)}</td>");
            sb.AppendLine($"          <td><details class=\"prompt\"><summary>view</summary><pre class=\"prompt\">{Html(PromptPreview.BuildForTest(test))}</pre></details></td>");
            sb.AppendLine("        </tr>");
        }
        sb.AppendLine("      </tbody>");
        sb.AppendLine("    </table>");

        sb.AppendLine("    <h2>Recent Runs</h2>");
        sb.AppendLine("    <div class=\"controls\">");
        sb.AppendLine("      <input id=\"run-search\" type=\"search\" placeholder=\"Filter runs by text…\" aria-label=\"Filter runs\">");
        sb.AppendLine("      <select id=\"run-filter\" aria-label=\"Filter by result\">");
        sb.AppendLine("        <option value=\"\">All results</option>");
        sb.AppendLine("        <option value=\"Passed\">Passed</option>");
        sb.AppendLine("        <option value=\"Succeeded\">Succeeded</option>");
        sb.AppendLine("        <option value=\"Failed\">Failed</option>");
        sb.AppendLine("        <option value=\"Aborted\">Aborted</option>");
        sb.AppendLine("        <option value=\"Blocked\">Blocked</option>");
        sb.AppendLine("      </select>");
        sb.AppendLine("      <span class=\"muted\">Click a run to expand its steps and screenshots.</span>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <table id=\"runs-table\">");
        sb.AppendLine("      <thead><tr><th>Run</th><th>Test</th><th>Result</th><th>Score</th><th>Started</th><th>Target</th></tr></thead>");
        sb.AppendLine("      <tbody>");
        foreach (var run in recentRuns)
        {
            var runId = Html(run.RunId);
            sb.AppendLine($"        <tr class=\"run-row\" data-runid=\"{runId}\" data-result=\"{Html(run.Result)}\">");
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

        // Data island: full per-run detail (steps + screenshots) for client-side drill-down.
        sb.AppendLine($"  <script id=\"agentloop-data\" type=\"application/json\">{dataIsland}</script>");
        sb.AppendLine(InteractiveScript());

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    private static string BuildDataIsland(string repoRoot, string outputPath, IReadOnlyList<RunArtifact> runs)
    {
        var projection = runs.Select(r => new
        {
            runId = r.RunId,
            testId = r.TestId ?? r.GoalIdentifier ?? "-",
            result = r.Result,
            score = r.FinalScore,
            target = r.TargetWindow ?? "-",
            traceId = r.TraceId,
            steps = r.Steps.Select(s => new
            {
                n = s.StepNumber,
                action = s.ActionType ?? "-",
                target = s.ActionTarget ?? "-",
                outcome = s.Outcome ?? "-",
                cumulative = s.CumulativeScore,
                failureCode = s.FailureCode,
                failureMessage = s.FailureMessage,
                guardStatus = s.GuardStatus,
                guardCode = s.GuardCode,
                screenshot = ScreenshotHref(outputPath, repoRoot, s.ScreenshotPath)
            }).ToList()
        }).ToList();

        // Bake the optional trace deep-link template at generation time (the static page
        // has no env at view time). Links a recorded run to its live OTLP trace (OBS-1b).
        var traceUiTemplate = Environment.GetEnvironmentVariable("AGENTLOOP_TRACE_UI_TEMPLATE") ?? "";
        var payload = new { traceUiTemplate, runs = projection };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = false });
        // Neutralize any "</script>" or "<" that could break the inline data island.
        return json.Replace("<", "\\u003c");
    }

    private static string? ScreenshotHref(string outputPath, string repoRoot, string? screenshotPath)
    {
        if (string.IsNullOrEmpty(screenshotPath))
            return null;

        try
        {
            var abs = Path.IsPathRooted(screenshotPath!)
                ? screenshotPath!
                : Path.GetFullPath(Path.Combine(repoRoot, screenshotPath!));
            var outputDir = Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? repoRoot;
            return Relative(outputDir, abs).Replace('\\', '/');
        }
        catch
        {
            return screenshotPath!.Replace('\\', '/');
        }
    }

    private static string InteractiveScript()
    {
        // Vanilla JS, no external dependencies: filter/search the runs table and
        // expand a per-run drill-down (steps + screenshots) from the data island.
        return """
  <script>
  (function () {
    var island = document.getElementById('agentloop-data');
    if (!island) return;
    var runs = {};
    var traceTpl = '';
    try {
      var data = JSON.parse(island.textContent);
      traceTpl = data.traceUiTemplate || '';
      (data.runs || []).forEach(function (r) { runs[r.runId] = r; });
    } catch (e) { return; }

    var search = document.getElementById('run-search');
    var filter = document.getElementById('run-filter');
    var rows = Array.prototype.slice.call(document.querySelectorAll('tr.run-row'));

    function esc(s) { var d = document.createElement('div'); d.textContent = (s == null ? '' : String(s)); return d.innerHTML; }
    function escAttr(s) { return esc(s).replace(/"/g, '&quot;').replace(/'/g, '&#39;'); }

    function applyFilter() {
      var q = (search && search.value ? search.value : '').toLowerCase();
      var f = (filter && filter.value) ? filter.value : '';
      rows.forEach(function (row) {
        var txt = row.textContent.toLowerCase();
        var res = row.getAttribute('data-result') || '';
        var show = (!q || txt.indexOf(q) >= 0) && (!f || res === f);
        row.classList.toggle('hidden', !show);
        var d = row.nextElementSibling;
        if (d && d.classList.contains('drill')) d.classList.toggle('hidden', !show);
      });
    }
    if (search) search.addEventListener('input', applyFilter);
    if (filter) filter.addEventListener('change', applyFilter);

    function renderDrill(run) {
      var h = '';
      if (run.traceId) {
        h += traceTpl
          ? '<p class="muted">Trace: <a href="' + escAttr(traceTpl.replace('{traceId}', run.traceId)) + '" target="_blank" rel="noreferrer">' + esc(run.traceId) + '</a></p>'
          : '<p class="muted">Trace: <code>' + esc(run.traceId) + '</code> &mdash; open in your Aspire dashboard &rarr; Traces.</p>';
      }
      h += '<table class="steps"><thead><tr><th>#</th><th>Action</th><th>Target</th><th>Outcome</th><th>Failure</th><th>Guard</th><th>Score</th></tr></thead><tbody>';
      (run.steps || []).forEach(function (s) {
        var fail = s.failureCode ? esc(s.failureCode) + (s.failureMessage ? ': ' + esc(s.failureMessage) : '') : '-';
        var guard = s.guardStatus ? esc(s.guardStatus) + (s.guardCode ? ':' + esc(s.guardCode) : '') : '-';
        h += '<tr><td>' + esc(s.n) + '</td><td>' + esc(s.action) + '</td><td>' + esc(s.target) + '</td><td>' + esc(s.outcome) + '</td><td>' + fail + '</td><td>' + guard + '</td><td>' + esc(s.cumulative) + '</td></tr>';
      });
      h += '</tbody></table>';
      var shots = (run.steps || []).filter(function (s) { return s.screenshot; });
      if (shots.length) {
        h += '<div class="shots">';
        shots.forEach(function (s) {
          h += '<a class="shot" href="' + esc(s.screenshot) + '" target="_blank" rel="noreferrer"><img src="' + esc(s.screenshot) + '" alt="step ' + esc(s.n) + '" loading="lazy"></a>';
        });
        h += '</div>';
      }
      if (!(run.steps || []).length) h += '<p class="muted">No recorded steps for this run.</p>';
      return h;
    }

    rows.forEach(function (row) {
      row.addEventListener('click', function () {
        var next = row.nextElementSibling;
        if (next && next.classList.contains('drill')) { next.parentNode.removeChild(next); return; }
        var run = runs[row.getAttribute('data-runid')];
        if (!run) return;
        var tr = document.createElement('tr');
        tr.className = 'drill';
        tr.innerHTML = '<td colspan="6">' + renderDrill(run) + '</td>';
        row.parentNode.insertBefore(tr, row.nextSibling);
      });
    });
  })();
  </script>
""";
    }

    private static int Pct(int value, int total) => total <= 0 ? 0 : (int)Math.Round(100.0 * value / total);

    private static List<string> ResolvePlanPaths(string repoRoot, List<string> requestedPlanPaths)
    {
        if (requestedPlanPaths.Count > 0)
            return requestedPlanPaths.Select(p => Path.GetFullPath(Path.IsPathRooted(p) ? p : Path.Combine(repoRoot, p))).ToList();

        return TestPlanLoader.DiscoverPlanPaths(repoRoot);
    }

    /// <summary>
    /// Loads the tests and, in the same pass, the non-fatal policy warnings per test id (V7 inc.2b)
    /// using the same <see cref="TestPlanValidator"/> the CLI's --validate-plan uses, so the static
    /// workbench surfaces the same advisories. The "{source}:{id}: " prefix is stripped for display.
    /// </summary>
    private static (List<TestDefinition> tests, Dictionary<string, List<string>> warnings) LoadTestsAndWarnings(
        IEnumerable<string> planPaths)
    {
        var tests = new List<TestDefinition>();
        var warnings = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var planPath in planPaths)
        {
            if (!File.Exists(planPath))
                continue;

            TestPlan plan;
            try { plan = TestPlanLoader.Load(planPath); }
            catch { continue; } // a broken plan should not stop the workbench rendering

            tests.AddRange(plan.Tests);

            TestPlanValidationResult validation;
            try { validation = TestPlanValidator.Validate(plan, "plan"); }
            catch { continue; }

            foreach (var t in plan.Tests)
            {
                if (string.IsNullOrWhiteSpace(t.Id))
                    continue;
                var prefix = $"plan:{t.Id}:";
                var ws = validation.Warnings
                    .Where(w => w.StartsWith(prefix, StringComparison.Ordinal))
                    .Select(TestPlanValidator.StripLocationPrefix)
                    .ToList();
                if (ws.Count > 0)
                    warnings[t.Id!] = ws;
            }
        }

        return (tests, warnings);
    }

    private static List<RunArtifact> LoadRuns(string runsRoot)
    {
        if (!Directory.Exists(runsRoot))
            return [];

        // ArtifactWriter serializes enums as strings (JsonStringEnumConverter), so the
        // reader must use the same converter or every real run fails to deserialize.
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new JsonStringEnumConverter());
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

    private static string RenderNotes(TestDefinition test, IReadOnlyDictionary<string, List<string>>? warningsByTestId)
    {
        if (test.Id != null && warningsByTestId != null &&
            warningsByTestId.TryGetValue(test.Id, out var ws) && ws.Count > 0)
            return string.Join("<br>", ws.Select(w => "&#9888; " + Html(w)));
        return "<span class=\"none\">-</span>";
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
