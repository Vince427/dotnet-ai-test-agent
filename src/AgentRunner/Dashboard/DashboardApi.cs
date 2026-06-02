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

            foreach (var t in plan.Tests)
            {
                tests.Add(new
                {
                    planPath = Relative(planPath),
                    suite = plan.Suite,
                    id = t.Id,
                    title = t.Title,
                    framework = t.Framework,
                    priority = t.Priority,
                    targetWindow = t.TargetWindow,
                    risk = t.Risk,
                    goal = t.Goal,
                    successCondition = t.SuccessCondition,
                    maxSteps = t.MaxSteps,
                    allowedActions = t.AllowedActions,
                    tags = t.Tags
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

        return ApiResponse.Json(new { ok = true, planPath = Relative(path), id = req.Id, yaml });
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

    // --- helpers ---

    private string Relative(string fullPath)
    {
        var rootFull = Path.GetFullPath(_repoRoot);
        var pathFull = Path.GetFullPath(fullPath);
        return pathFull.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase)
            ? pathFull[rootFull.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Replace(Path.DirectorySeparatorChar, '/')
            : fullPath;
    }

    private string? ResolveUnderRepo(string relativeOrAbsolute)
    {
        var combined = Path.IsPathRooted(relativeOrAbsolute)
            ? relativeOrAbsolute
            : Path.Combine(_repoRoot, relativeOrAbsolute);
        var full = Path.GetFullPath(combined);
        var rootFull = Path.GetFullPath(_repoRoot);
        return full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase) ? full : null;
    }

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
        sb.AppendLine("    category: \"Scenario\"");
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
        public string? Goal { get; set; }
        public string? SuccessCondition { get; set; }
        public int? MaxSteps { get; set; }
        public List<string>? AllowedActions { get; set; }
        public List<string>? Tags { get; set; }
    }

    public sealed class LaunchRequest
    {
        public string? PlanPath { get; set; }
        public string? TestId { get; set; }
        public string? Window { get; set; }
    }
}
