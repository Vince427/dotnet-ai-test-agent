using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner.Dashboard;

/// <summary>
/// Response from a dashboard endpoint: status code, content type, and body bytes.
/// Kept transport-agnostic so the handlers are unit-testable without a socket.
/// </summary>
public sealed class ApiResponse(int status, string contentType, byte[] body)
{
    public int Status { get; } = status;
    public string ContentType { get; } = contentType;
    public byte[] Body { get; } = body;

    public static ApiResponse Json(object value, int status = 200) =>
        new(status, "application/json; charset=utf-8",
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions)));

    public static ApiResponse Text(string text, int status, string contentType = "text/plain; charset=utf-8") =>
        new(status, contentType, Encoding.UTF8.GetBytes(text));

    public static ApiResponse Error(int status, string message) =>
        Json(new { error = message }, status);

    public static readonly JsonSerializerOptions JsonOptions = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var o = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        o.Converters.Add(new JsonStringEnumConverter());
        return o;
    }
}

/// <summary>
/// The dashboard's read/create/launch logic, independent of HTTP. <see cref="DashboardServer"/>
/// adapts HTTP requests to these methods. Everything is a view/launcher over the
/// existing CLI contract and artifacts — no new data model.
/// </summary>
public sealed class DashboardApi
{
    private readonly string _repoRoot;
    private readonly string _runsRoot;
    private readonly RunJobManager _jobs;

    public DashboardApi(string repoRoot, string runsRoot, RunJobManager jobs)
    {
        _repoRoot = repoRoot;
        _runsRoot = runsRoot;
        _jobs = jobs;
    }

    /// <summary>Catalog of tests, flat; the UI groups by suite / framework / priority / tags.</summary>
    public ApiResponse GetTests()
    {
        var tests = new List<object>();
        foreach (var planPath in TestPlanLoader.DiscoverPlanPaths(_repoRoot))
        {
            TestPlan plan;
            try { plan = TestPlanLoader.Load(planPath); }
            catch { continue; } // skip unparseable plans; validation surfaces elsewhere

            var relPlan = Relative(planPath);
            // A single-test file is safe to move wholesale; one under tests/created/ is one the
            // dashboard authored, so it is safe to re-write in place (Edit). Multi-test files are
            // "edit on disk" only — we never rewrite a file and risk clobbering sibling tests.
            var singleTest = plan.Tests.Count == 1;
            var editable = singleTest && relPlan.StartsWith("tests/created/", StringComparison.OrdinalIgnoreCase);

            // V7 inc.2: surface the same non-fatal policy warnings the CLI's --validate-plan emits,
            // so they're visible at a glance in the catalog (the plan is still valid — advisory only).
            TestPlanValidationResult validation;
            try { validation = TestPlanValidator.Validate(plan, relPlan); }
            catch { validation = new TestPlanValidationResult(); }

            foreach (var t in plan.Tests)
            {
                var label = string.IsNullOrWhiteSpace(t.Id) ? "(missing id)" : t.Id!;
                var prefix = $"{relPlan}:{label}:";
                var warnings = validation.Warnings
                    .Where(w => w.StartsWith(prefix, StringComparison.Ordinal))
                    .Select(FriendlyWarning)
                    .ToList();

                tests.Add(new
                {
                    planPath = relPlan,
                    suite = plan.Suite,
                    id = t.Id,
                    title = t.Title,
                    framework = t.Framework,
                    priority = t.Priority,
                    category = t.Category,
                    targetWindow = t.TargetWindow,
                    risk = t.Risk,
                    goal = t.Goal,
                    successCondition = t.SuccessCondition,
                    maxSteps = t.MaxSteps,
                    allowedActions = t.AllowedActions,
                    tags = t.Tags,
                    warnings,
                    editable,
                    archivable = singleTest
                });
            }
        }

        return ApiResponse.Json(new { count = tests.Count, tests });
    }

    /// <summary>Run history (newest first), summary shape for the list view.</summary>
    public ApiResponse GetRuns()
    {
        var runs = RunArtifactLoader.LoadFromDirectory(_runsRoot)
            .OrderByDescending(r => r.StartedAt)
            .Select(r => new
            {
                runId = r.RunId,
                testId = r.TestId,
                testTitle = r.TestTitle,
                suite = r.Suite,
                framework = r.Framework,
                targetWindow = r.TargetWindow,
                result = r.Result,
                finalScore = r.FinalScore,
                startedAt = r.StartedAt,
                endedAt = r.EndedAt,
                steps = r.Steps.Count,
                traceId = r.TraceId
            })
            .ToList();

        return ApiResponse.Json(new { count = runs.Count, runs });
    }

    /// <summary>Full detail for one run (its report.json), including steps and trace id.</summary>
    public ApiResponse GetRun(string runId)
    {
        if (!IsSafeSegment(runId))
            return ApiResponse.Error(400, "Invalid run id.");

        var reportPath = Path.Combine(_runsRoot, runId, "report.json");
        if (!File.Exists(reportPath))
            return ApiResponse.Error(404, "Run not found.");

        // Pass the stored report through verbatim (already camelCase + enums-as-strings).
        return new ApiResponse(200, "application/json; charset=utf-8", File.ReadAllBytes(reportPath));
    }

    /// <summary>Currently-tracked launch jobs and their captured progress.</summary>
    public ApiResponse GetJobs() => ApiResponse.Json(new { jobs = _jobs.Snapshot() });

    public ApiResponse GetJob(string jobId)
    {
        var job = _jobs.Get(jobId);
        return job == null ? ApiResponse.Error(404, "Job not found.") : ApiResponse.Json(job);
    }

    /// <summary>
    /// Create a test ("ticket"): build YAML from the posted fields, validate it, and only
    /// then persist it under tests/created/&lt;id&gt;.yaml. YAML stays the source of truth.
    /// </summary>
    public ApiResponse CreateTest(string body)
    {
        CreateTestRequest req;
        try { req = JsonSerializer.Deserialize<CreateTestRequest>(body, ApiResponse.JsonOptions) ?? new(); }
        catch (Exception ex) { return ApiResponse.Error(400, "Invalid JSON: " + ex.Message); }

        if (string.IsNullOrWhiteSpace(req.Id) || !IsSafeSegment(req.Id!))
            return ApiResponse.Error(400, "A safe test id is required (letters, digits, '-', '_').");
        if (string.IsNullOrWhiteSpace(req.Goal))
            return ApiResponse.Error(400, "A goal is required.");

        var yaml = BuildYaml(req);

        // Validate by parsing + running the same validator the CLI uses.
        TestPlan plan;
        try { plan = TestPlanLoader.Parse(yaml, req.Id); }
        catch (Exception ex) { return ApiResponse.Error(422, "Generated YAML is invalid: " + ex.Message); }

        var validation = TestPlanValidator.Validate(plan, req.Id!);
        if (!validation.IsValid)
            return ApiResponse.Json(new { error = "Validation failed.", errors = validation.Errors }, 422);

        var dir = Path.Combine(_repoRoot, "tests", "created");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, req.Id + ".yaml");
        File.WriteAllText(path, yaml);

        // Also emit a Symphony-style ticket (.md) referencing this plan, so the SAME
        // contract that CI runs via scripts/run-ticket-proof.ps1 is produced here.
        var ticketDir = Path.Combine(_repoRoot, "tickets", "created");
        Directory.CreateDirectory(ticketDir);
        var ticketPath = Path.Combine(ticketDir, req.Id + ".md");
        File.WriteAllText(ticketPath, BuildTicketMarkdown(req, Relative(path)));

        return ApiResponse.Json(new
        {
            ok = true, id = req.Id, planPath = Relative(path), ticketPath = Relative(ticketPath), yaml,
            // V7 inc.2: the plan is valid, but echo any non-fatal advisories so the author sees them.
            warnings = validation.Warnings.Select(FriendlyWarning).ToList()
        });
    }

    /// <summary>
    /// V7 inc.2: render the exact prompt the LLM would receive for a test — key-free, no run. This
    /// is the dashboard surface for the CLI's <c>--show-prompt</c> / MCP <c>show_prompt</c>; it reuses
    /// <see cref="PromptPreview"/> (and thus <c>PromptBuilder</c>), so the preview can't drift from the
    /// runtime prompt. Secrets are redacted by the <see cref="SecretRedactor"/> baked into the preview.
    /// </summary>
    public ApiResponse GetPrompt(string planPath, string testId)
    {
        if (string.IsNullOrWhiteSpace(planPath) || string.IsNullOrWhiteSpace(testId))
            return ApiResponse.Error(400, "planPath and testId are required.");

        var resolved = ResolveUnderRepo(planPath);
        if (resolved == null || !File.Exists(resolved))
            return ApiResponse.Error(400, "planPath not found under the repository.");

        TestPlan plan;
        try { plan = TestPlanLoader.Load(resolved); }
        catch (Exception ex) { return ApiResponse.Error(422, "Plan is invalid: " + ex.Message); }

        var test = plan.Tests.FirstOrDefault(t => string.Equals(t.Id, testId, StringComparison.OrdinalIgnoreCase));
        if (test == null)
            return ApiResponse.Error(404, $"Test '{testId}' not found in {Relative(resolved)}.");

        var prompt = PromptPreview.BuildForTest(test, new SecretRedactor());
        return ApiResponse.Json(new { testId = test.Id, planPath = Relative(resolved), prompt });
    }

    /// <summary>
    /// Strip the "{source}:{id}: " location prefix a validator message carries, leaving just the
    /// human-readable advisory for the UI. The path/id have no ": " (colon+space), so the first
    /// ": " marks the start of the message.
    /// </summary>
    private static string FriendlyWarning(string warning)
    {
        var i = warning.IndexOf(": ", StringComparison.Ordinal);
        return i >= 0 ? warning[(i + 2)..] : warning;
    }

    /// <summary>List the Symphony tickets under tickets/ (frontmatter parsed).</summary>
    public ApiResponse GetTickets()
    {
        var ticketsRoot = Path.Combine(_repoRoot, "tickets");
        var tickets = new List<object>();
        if (Directory.Exists(ticketsRoot))
        {
            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(ticketsRoot, "*.md", SearchOption.AllDirectories)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase); }
            catch { files = []; }
            foreach (var f in files)
            {
                var fm = ParseFrontMatter(f);
                string? G(string k) => fm.TryGetValue(k, out var v) ? v : null;
                tickets.Add(new
                {
                    path = Relative(f),
                    ticketId = G("ticket_id"),
                    title = G("title"),
                    framework = G("framework"),
                    plan = G("plan"),
                    testId = G("test_id"),
                    targetWindow = G("target_window"),
                    evidenceLevel = G("evidence_level")
                });
            }
        }
        return ApiResponse.Json(new { count = tickets.Count, tickets });
    }

    /// <summary>Raw markdown of one ticket under tickets/ (path-safe).</summary>
    public ApiResponse GetTicket(string relPath)
    {
        if (string.IsNullOrWhiteSpace(relPath) || Path.GetExtension(relPath) is not (".md" or ".markdown"))
            return ApiResponse.Error(400, "A ticket .md path is required.");
        var full = ResolveUnderRoot(_repoRoot, relPath);
        if (full == null || !IsUnderRoot(full, "tickets") || !File.Exists(full))
            return ApiResponse.Error(404, "Ticket not found under tickets/.");
        return ApiResponse.Text(File.ReadAllText(full), 200, "text/markdown; charset=utf-8");
    }

    /// <summary>
    /// Run a ticket through the SAME adapter CI uses (scripts/run-ticket-proof.ps1):
    /// validate → optional sample launch → CLI run → artifacts. Spawned and tracked
    /// like any job so it streams into the Live view.
    /// </summary>
    public ApiResponse RunTicket(string body)
    {
        string? rel;
        try { rel = JsonSerializer.Deserialize<TicketRunRequest>(body, ApiResponse.JsonOptions)?.TicketPath; }
        catch (Exception ex) { return ApiResponse.Error(400, "Invalid JSON: " + ex.Message); }

        if (string.IsNullOrWhiteSpace(rel) || Path.GetExtension(rel) is not (".md" or ".markdown"))
            return ApiResponse.Error(400, "ticketPath (.md) is required.");
        var full = ResolveUnderRoot(_repoRoot, rel!);
        if (full == null || !IsUnderRoot(full, "tickets") || !File.Exists(full))
            return ApiResponse.Error(400, "Ticket not found under tickets/.");

        try { return ApiResponse.Json(_jobs.LaunchTicket(full), 202); }
        catch (Exception ex) { return ApiResponse.Error(500, "Failed to launch ticket: " + ex.Message); }
    }

    public sealed class TicketRunRequest { public string? TicketPath { get; set; } }

    /// <summary>Flatten a value to a single safe frontmatter line (strip control chars/newlines).</summary>
    private static string Scalar(string? value) =>
        value is null ? "" : new string(value.Where(c => !char.IsControl(c)).ToArray()).Trim();

    /// <summary>Minimal flat `key: value` front-matter reader (matches run-ticket-proof.ps1).</summary>
    private static Dictionary<string, string> ParseFrontMatter(string path)
    {
        var fm = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var lines = File.ReadAllLines(path);
            if (lines.Length == 0 || lines[0].Trim() != "---") return fm;
            for (var i = 1; i < lines.Length; i++)
            {
                if (lines[i].Trim() == "---") break;
                var m = System.Text.RegularExpressions.Regex.Match(lines[i], @"^([A-Za-z0-9_-]+)\s*:\s*(.*)$");
                if (m.Success && m.Groups[2].Value.Trim().Length > 0)
                    fm[m.Groups[1].Value] = m.Groups[2].Value.Trim();
            }
        }
        catch { /* unreadable → empty */ }
        return fm;
    }

    /// <summary>Emit a Symphony ticket markdown referencing the just-created plan.</summary>
    internal static string BuildTicketMarkdown(CreateTestRequest req, string planRelPath)
    {
        var ev = req.EvidenceLevel is "minimal" or "standard" or "full" ? req.EvidenceLevel : "standard";
        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"ticket_id: TICKET-{req.Id}");
        // Scalar() strips control chars/newlines so a crafted value can't inject extra
        // frontmatter lines (e.g. a forged plan:) that the ticket runner would then parse.
        if (!string.IsNullOrWhiteSpace(req.Title)) sb.AppendLine($"title: {Scalar(req.Title)}");
        if (!string.IsNullOrWhiteSpace(req.Framework)) sb.AppendLine($"framework: {Scalar(req.Framework)}");
        sb.AppendLine($"launch_sample: {(req.LaunchSample ? "true" : "false")}");
        sb.AppendLine($"plan: {planRelPath}");
        sb.AppendLine($"test_id: {req.Id}");
        if (!string.IsNullOrWhiteSpace(req.TargetWindow)) sb.AppendLine($"target_window: {Scalar(req.TargetWindow)}");
        sb.AppendLine($"evidence_level: {ev}");
        sb.AppendLine("authoring_agent: dashboard");
        sb.AppendLine("expected_artifacts:");
        sb.AppendLine("  - report.json");
        sb.AppendLine("  - summary.md");
        if (ev != "minimal") sb.AppendLine("  - screenshots");
        if (ev == "full") sb.AppendLine("  - ui-tree");
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine($"# {(string.IsNullOrWhiteSpace(req.Title) ? req.Id : Scalar(req.Title))}");
        sb.AppendLine();
        sb.AppendLine("## Goal");
        sb.AppendLine();
        sb.AppendLine(req.Goal);
        sb.AppendLine();
        sb.AppendLine("## Agent Work");
        sb.AppendLine();
        sb.AppendLine($"- Run the plan `{planRelPath}` test `{req.Id}` via the portable CLI contract.");
        sb.AppendLine("- Keep the target app non-intrusive (no agent packages or test-only code paths).");
        sb.AppendLine("- Prefer YAML-only edits; trace authoring back to this ticket id.");
        return sb.ToString();
    }

    /// <summary>Launch a run for a test by spawning the CLI (parallel-friendly).</summary>
    public ApiResponse LaunchRun(string body)
    {
        LaunchRequest req;
        try { req = JsonSerializer.Deserialize<LaunchRequest>(body, ApiResponse.JsonOptions) ?? new(); }
        catch (Exception ex) { return ApiResponse.Error(400, "Invalid JSON: " + ex.Message); }

        if (string.IsNullOrWhiteSpace(req.PlanPath) || string.IsNullOrWhiteSpace(req.TestId))
            return ApiResponse.Error(400, "planPath and testId are required.");

        // Resolve the plan path safely under the repo (no traversal outside).
        var resolved = ResolveUnderRepo(req.PlanPath!);
        if (resolved == null || !File.Exists(resolved))
            return ApiResponse.Error(400, "planPath not found under the repository.");

        try
        {
            var job = _jobs.Launch(resolved, req.TestId!, req.Window);
            return ApiResponse.Json(job, 202);
        }
        catch (Exception ex)
        {
            return ApiResponse.Error(500, "Failed to launch: " + ex.Message);
        }
    }

    /// <summary>
    /// Archive a test by moving its YAML under tests/archived/ (excluded from the catalog, the
    /// CLI, and CI). Restricted to single-test files so we never hide a test's siblings; multi-
    /// test files stay "edit on disk". Reversible — the file is moved, not deleted, and shows in
    /// Git. The dashboard never hard-deletes source-of-truth YAML.
    /// </summary>
    public ApiResponse ArchiveTest(string body)
    {
        ArchiveRequest req;
        try { req = JsonSerializer.Deserialize<ArchiveRequest>(body, ApiResponse.JsonOptions) ?? new(); }
        catch (Exception ex) { return ApiResponse.Error(400, "Invalid JSON: " + ex.Message); }

        if (string.IsNullOrWhiteSpace(req.PlanPath))
            return ApiResponse.Error(400, "planPath is required.");

        var resolved = ResolveUnderRepo(req.PlanPath!);
        if (resolved == null || !File.Exists(resolved))
            return ApiResponse.Error(400, "planPath not found under the repository.");
        if (!IsUnderRoot(resolved, "tests"))
            return ApiResponse.Error(400, "Only tests under tests/ can be archived.");
        if (Path.GetExtension(resolved) is not (".yaml" or ".yml"))
            return ApiResponse.Error(400, "Only a .yaml/.yml test file can be archived.");

        var testsDir = Path.GetFullPath(Path.Combine(_repoRoot, "tests"));
        var archivedRoot = Path.Combine(testsDir, "archived");
        if (resolved.StartsWith(archivedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return ApiResponse.Error(409, "Already archived.");

        TestPlan plan;
        try { plan = TestPlanLoader.Load(resolved); }
        catch (Exception ex) { return ApiResponse.Error(422, "Plan is invalid: " + ex.Message); }
        if (plan.Tests.Count != 1)
            return ApiResponse.Error(409, "Only single-test files can be archived from the dashboard (edit multi-test files on disk).");

        var rel = resolved[testsDir.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var dest = Path.Combine(archivedRoot, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        if (File.Exists(dest)) File.Delete(dest); // re-archiving replaces the prior copy (net48 has no Move overwrite)
        File.Move(resolved, dest);

        return ApiResponse.Json(new { ok = true, archivedPath = Relative(dest) });
    }

    /// <summary>List archived tests (under tests/archived/) so the dashboard can offer Restore.</summary>
    public ApiResponse GetArchived()
    {
        var archivedRoot = Path.Combine(_repoRoot, "tests", "archived");
        var items = new List<object>();
        if (Directory.Exists(archivedRoot))
        {
            var files = Directory.EnumerateFiles(archivedRoot, "*.yaml", SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles(archivedRoot, "*.yml", SearchOption.AllDirectories))
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase);
            foreach (var f in files)
            {
                TestPlan plan;
                try { plan = TestPlanLoader.Load(f); } catch { continue; }
                foreach (var t in plan.Tests)
                    items.Add(new { planPath = Relative(f), id = t.Id, title = t.Title, framework = t.Framework, category = t.Category, suite = plan.Suite });
            }
        }
        return ApiResponse.Json(new { count = items.Count, tests = items });
    }

    /// <summary>
    /// Restore an archived test: move its YAML from tests/archived/ back to its original tests/
    /// path. The reverse of <see cref="ArchiveTest"/> — both are just file moves, visible in Git.
    /// </summary>
    public ApiResponse UnarchiveTest(string body)
    {
        ArchiveRequest req;
        try { req = JsonSerializer.Deserialize<ArchiveRequest>(body, ApiResponse.JsonOptions) ?? new(); }
        catch (Exception ex) { return ApiResponse.Error(400, "Invalid JSON: " + ex.Message); }

        if (string.IsNullOrWhiteSpace(req.PlanPath))
            return ApiResponse.Error(400, "planPath is required.");

        var resolved = ResolveUnderRepo(req.PlanPath!);
        if (resolved == null || !File.Exists(resolved))
            return ApiResponse.Error(400, "planPath not found under the repository.");

        var archivedRoot = Path.GetFullPath(Path.Combine(_repoRoot, "tests", "archived"));
        if (!resolved.StartsWith(archivedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return ApiResponse.Error(400, "Only tests under tests/archived/ can be restored.");
        if (Path.GetExtension(resolved) is not (".yaml" or ".yml"))
            return ApiResponse.Error(400, "Only a .yaml/.yml test file can be restored.");

        var rel = resolved[(archivedRoot.Length + 1)..]; // path relative to tests/archived/
        var dest = Path.Combine(_repoRoot, "tests", rel);
        if (File.Exists(dest))
            return ApiResponse.Error(409, "A test already exists at the original path; resolve it on disk.");

        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        File.Move(resolved, dest);

        return ApiResponse.Json(new { ok = true, planPath = Relative(dest) });
    }

    /// <summary>Current max concurrent runs (the rest queue).</summary>
    public int MaxConcurrency => _jobs.MaxConcurrency;

    /// <summary>Set the run queue's max concurrency (clamped to [1, 16] by the job manager).</summary>
    public ApiResponse SetConcurrency(string body)
    {
        ConcurrencyRequest req;
        try { req = JsonSerializer.Deserialize<ConcurrencyRequest>(body, ApiResponse.JsonOptions) ?? new(); }
        catch (Exception ex) { return ApiResponse.Error(400, "Invalid JSON: " + ex.Message); }

        if (req.Max < 1)
            return ApiResponse.Error(400, "max must be >= 1.");

        _jobs.MaxConcurrency = req.Max;
        return ApiResponse.Json(new { ok = true, maxConcurrency = _jobs.MaxConcurrency });
    }

    /// <summary>List screenshot step files for a run (paths the UI then fetches).</summary>
    public ApiResponse GetScreenshotList(string runId)
    {
        if (!IsSafeSegment(runId))
            return ApiResponse.Error(400, "Invalid run id.");

        var dir = Path.Combine(_runsRoot, runId, "screenshots");
        if (!Directory.Exists(dir))
            return ApiResponse.Json(new { runId, screenshots = Array.Empty<string>() });

        var shots = Directory.EnumerateFiles(dir, "step_*.png")
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .Select(Path.GetFileName)
            .ToList();

        return ApiResponse.Json(new { runId, screenshots = shots });
    }

    /// <summary>Serve one screenshot PNG, strictly from under the runs root (no traversal).</summary>
    public ApiResponse GetScreenshot(string runId, string file)
    {
        if (!IsSafeSegment(runId) || !IsSafeSegment(file) || !file.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            return ApiResponse.Error(400, "Invalid screenshot path.");

        var full = Path.GetFullPath(Path.Combine(_runsRoot, runId, "screenshots", file));
        var runsFull = Path.GetFullPath(_runsRoot);
        if (!full.StartsWith(runsFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(full))
            return ApiResponse.Error(404, "Screenshot not found.");

        return new ApiResponse(200, "image/png", File.ReadAllBytes(full));
    }

    /// <summary>Text/config extensions the dashboard will preview (never executables/binaries).</summary>
    private static readonly HashSet<string> TextExts = new(StringComparer.OrdinalIgnoreCase)
        { ".yaml", ".yml", ".md", ".json", ".txt", ".props", ".xml", ".template", ".csv", ".html" };

    /// <summary>
    /// File tree the dashboard reflects: everything under tests/ and runs/ (the YAML
    /// source-of-truth and the run artifacts) plus a couple of root config files. Lets a
    /// user see exactly what to edit by hand — in their editor or a CI script — with no UI.
    /// </summary>
    public ApiResponse GetFiles()
    {
        const int cap = 2000;
        var files = new List<object>();

        void Add(string fullPath)
        {
            if (files.Count >= cap) return;
            long size; try { size = new FileInfo(fullPath).Length; } catch { return; }
            var ext = Path.GetExtension(fullPath).ToLowerInvariant();
            files.Add(new { path = Relative(fullPath), size, ext, editable = TextExts.Contains(ext) });
        }

        foreach (var rel in new[] { "tests", "runs" })
        {
            var dir = Path.Combine(_repoRoot, rel);
            if (!Directory.Exists(dir)) continue;
            IEnumerable<string> paths;
            try { paths = Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase); }
            catch { continue; }
            foreach (var p in paths) { if (files.Count >= cap) break; Add(p); }
        }

        foreach (var rootFile in new[] { "WORKFLOW.md", ".env.template" })
        {
            var abs = Path.Combine(_repoRoot, rootFile);
            if (File.Exists(abs)) Add(abs);
        }

        return ApiResponse.Json(new { count = files.Count, capped = files.Count >= cap, files });
    }

    /// <summary>
    /// Preview one text/config file under the repo. Path-traversal guarded, extension
    /// allow-listed, size-capped, and it explicitly refuses real secrets files (.env).
    /// </summary>
    public ApiResponse GetFile(string relPath)
    {
        if (string.IsNullOrWhiteSpace(relPath))
            return ApiResponse.Error(400, "A path is required.");

        var name = Path.GetFileName(relPath);
        if (name.StartsWith(".env", StringComparison.OrdinalIgnoreCase) &&
            !name.Equals(".env.template", StringComparison.OrdinalIgnoreCase))
            return ApiResponse.Error(403, "Refusing to serve a secrets file.");

        if (!TextExts.Contains(Path.GetExtension(relPath)))
            return ApiResponse.Error(415, "Only text/config files can be previewed.");

        var full = ResolveUnderRoot(_repoRoot, relPath);
        if (full == null || !File.Exists(full))
            return ApiResponse.Error(404, "File not found.");

        // Confine to exactly what GetFiles advertises: the tests/ + runs/ trees, plus the
        // two named root config files. Prevents previewing unrelated repo files over the API.
        if (!IsUnderRoot(full, "tests") && !IsUnderRoot(full, "runs") &&
            !IsNamedRootFile(full, "WORKFLOW.md") && !IsNamedRootFile(full, ".env.template"))
            return ApiResponse.Error(403, "Only files under tests/ and runs/ (and WORKFLOW.md / .env.template) are previewable.");

        if (new FileInfo(full).Length > 512 * 1024)
            return ApiResponse.Error(413, "File too large to preview (512 KB cap).");

        return ApiResponse.Text(File.ReadAllText(full), 200);
    }

    // --- helpers ---

    private string Relative(string fullPath)
    {
        var prefix = Path.GetFullPath(_repoRoot) + Path.DirectorySeparatorChar;
        var pathFull = Path.GetFullPath(fullPath);
        return pathFull.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? pathFull[prefix.Length..].Replace(Path.DirectorySeparatorChar, '/')
            : fullPath;
    }

    private string? ResolveUnderRepo(string relativeOrAbsolute) =>
        ResolveUnderRoot(_repoRoot, relativeOrAbsolute);

    /// <summary>
    /// Resolves an input path and returns it only if it stays at or under <paramref name="root"/>.
    /// Uses a trailing-separator containment check so a sibling like <c>&lt;root&gt;-evil</c> is
    /// rejected (not just a plain prefix match). Returns null when the path escapes the root.
    /// </summary>
    internal static string? ResolveUnderRoot(string root, string relativeOrAbsolute)
    {
        var combined = Path.IsPathRooted(relativeOrAbsolute)
            ? relativeOrAbsolute
            : Path.Combine(root, relativeOrAbsolute);
        var full = Path.GetFullPath(combined);
        var rootFull = Path.GetFullPath(root);
        if (string.Equals(full, rootFull, StringComparison.OrdinalIgnoreCase))
            return full;
        return full.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            ? full
            : null;
    }

    private bool IsUnderRoot(string fullPath, string relRoot)
    {
        var prefix = Path.GetFullPath(Path.Combine(_repoRoot, relRoot)) + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsNamedRootFile(string fullPath, string fileName) =>
        string.Equals(fullPath, Path.GetFullPath(Path.Combine(_repoRoot, fileName)), StringComparison.OrdinalIgnoreCase);

    /// <summary>One path segment, no separators or traversal.</summary>
    internal static bool IsSafeSegment(string? s) =>
        !string.IsNullOrEmpty(s) &&
        s!.IndexOfAny(['/', '\\']) < 0 &&
        !s.Contains("..") &&
        s.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    /// <summary>Emit a plan YAML for one test in the shape <see cref="TestPlanLoader"/> parses.</summary>
    internal static string BuildYaml(CreateTestRequest req)
    {
        var sb = new StringBuilder();
        sb.Append("suite: ").AppendLine(Quote(string.IsNullOrWhiteSpace(req.Suite) ? "created" : req.Suite!));
        sb.AppendLine();
        sb.AppendLine("tests:");
        sb.Append("  ").Append(req.Id).AppendLine(":");
        AppendScalar(sb, "title", req.Title);
        AppendScalar(sb, "priority", req.Priority);
        AppendScalar(sb, "framework", req.Framework);
        AppendScalar(sb, "target_window", req.TargetWindow);
        AppendScalar(sb, "risk", req.Risk);
        sb.AppendLine("    authoring_agent: \"dashboard\"");
        // Category shapes the agent persona/prompt; whitelist to the known taxonomy, default Scenario.
        var category = req.Category switch
        {
            "Smoke" or "Monkey" or "Audit" or "Scenario" => req.Category,
            _ => "Scenario"
        };
        sb.AppendLine($"    category: \"{category}\"");
        AppendScalar(sb, "goal", req.Goal);
        AppendScalar(sb, "success_condition", req.SuccessCondition);
        sb.Append("    max_steps: ").AppendLine((req.MaxSteps is > 0 ? req.MaxSteps.Value : 8).ToString());
        AppendInlineList(sb, "allowed_actions", req.AllowedActions);
        AppendInlineList(sb, "tags", req.Tags);
        return sb.ToString();
    }

    private static void AppendScalar(StringBuilder sb, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            sb.Append("    ").Append(key).Append(": ").AppendLine(Quote(value!));
    }

    private static void AppendInlineList(StringBuilder sb, string key, List<string>? values)
    {
        if (values is { Count: > 0 })
            sb.Append("    ").Append(key).Append(": [").Append(string.Join(", ", values.Select(Quote))).AppendLine("]");
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "'") + "\"";

    public sealed class CreateTestRequest
    {
        public string? Suite { get; set; }
        public string? Id { get; set; }
        public string? Title { get; set; }
        public string? Priority { get; set; }
        public string? Framework { get; set; }
        public string? TargetWindow { get; set; }
        public string? Risk { get; set; }
        public string? Category { get; set; }   // Smoke | Monkey | Audit | Scenario (default)
        public string? Goal { get; set; }
        public string? SuccessCondition { get; set; }
        public int? MaxSteps { get; set; }
        public List<string>? AllowedActions { get; set; }
        public List<string>? Tags { get; set; }
        public string? EvidenceLevel { get; set; }   // minimal | standard | full (ticket)
        public bool LaunchSample { get; set; }        // ticket: start the built-in sample around the run
    }

    public sealed class LaunchRequest
    {
        public string? PlanPath { get; set; }
        public string? TestId { get; set; }
        public string? Window { get; set; }
    }

    public sealed class ArchiveRequest
    {
        public string? PlanPath { get; set; }
    }

    public sealed class ConcurrencyRequest
    {
        public int Max { get; set; }
    }
}
