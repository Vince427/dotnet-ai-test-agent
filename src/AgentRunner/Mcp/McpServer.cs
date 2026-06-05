using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DesktopAiTestAgent.AgentRunner.Mcp;

/// <summary>
/// A minimal Model Context Protocol (JSON-RPC 2.0 over stdio) server that exposes the runner's
/// **read-only, key-free** capabilities as MCP tools, so an agent host (Claude Desktop, Copilot,
/// …) can list/validate tests and read run artifacts natively. It is an *adapter over the same
/// CLI contract* — it reuses `TestPlanLoader`/`TestPlanValidator`/`RunArtifactLoader`, adds no new
/// data model, and (this increment) deliberately exposes nothing that spawns a run or needs
/// `.env`. The line dispatcher is pure and unit-tested; only the stdio loop in `Program` is I/O.
/// </summary>
public sealed class McpServer
{
    private const string ProtocolVersion = "2024-11-05";
    private const string ServerName = "agentloop";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _repoRoot;
    private readonly string _runsRoot;
    private readonly bool _allowWrite;

    /// <summary>
    /// Create the adapter. Writes are <b>off by default</b> (read-only). Pass
    /// <paramref name="allowWrite"/> = <c>true</c> (from the opt-in <c>--mcp-allow-write</c> CLI flag
    /// or the <c>AGENTLOOP_MCP_ALLOW_WRITE=1</c> env var) to enable the authoring <c>create_test</c>
    /// tool, which writes validated YAML under <c>tests/created/</c>.
    /// </summary>
    public McpServer(string repoRoot, string runsRoot, bool allowWrite = false)
    {
        _repoRoot = repoRoot;
        _runsRoot = runsRoot;
        _allowWrite = allowWrite;
    }

    /// <summary>
    /// Handles one JSON-RPC line and returns the response JSON, or <c>null</c> for a notification
    /// (no <c>id</c>) which must not be answered. Never throws — protocol problems become JSON-RPC
    /// errors, tool problems become an <c>isError</c> result.
    /// </summary>
    public string? HandleLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        JsonElement root;
        try { using var doc = JsonDocument.Parse(line); root = doc.RootElement.Clone(); }
        catch { return Serialize(Error(null, -32700, "Parse error")); }

        var method = root.TryGetProperty("method", out var m) ? m.GetString() : null;
        var hasId = root.TryGetProperty("id", out var idEl) && idEl.ValueKind is not JsonValueKind.Null;
        object? id = hasId ? IdValue(idEl) : null;

        // Notifications (no id, e.g. notifications/initialized) get no response.
        if (!hasId)
            return null;
        if (string.IsNullOrEmpty(method))
            return Serialize(Error(id, -32600, "Invalid Request: missing method"));

        var prms = root.TryGetProperty("params", out var p) ? p : default;

        try
        {
            return method switch
            {
                "initialize" => Serialize(Result(id, InitializeResult())),
                "ping" => Serialize(Result(id, new { })),
                "tools/list" => Serialize(Result(id, new { tools = ToolDefinitions() })),
                "tools/call" => Serialize(Result(id, CallTool(prms))),
                _ => Serialize(Error(id, -32601, "Method not found: " + method))
            };
        }
        catch (McpToolException ex)
        {
            return Serialize(Result(id, ToolError(ex.Message)));
        }
        catch (Exception ex)
        {
            return Serialize(Error(id, -32603, "Internal error: " + ex.Message));
        }
    }

    private static object InitializeResult() => new
    {
        protocolVersion = ProtocolVersion,
        capabilities = new { tools = new { } },
        serverInfo = new { name = ServerName, version = "0.1.0" }
    };

    private object[] ToolDefinitions()
    {
        var tools = new List<object>(ReadOnlyToolDefinitions());
        // Authoring tool is advertised only when writes are opted in (--mcp-allow-write /
        // AGENTLOOP_MCP_ALLOW_WRITE=1). Read-only hosts never see a write tool.
        if (_allowWrite)
            tools.Add(Tool("create_test",
                "Author a new test: build YAML (via the same emitter the dashboard uses), validate it with the CLI validator, and write tests/created/<id>.yaml. Opt-in; disabled unless the server was started with writes enabled.",
                CreateTestSchema()));
        return tools.ToArray();
    }

    private static object[] ReadOnlyToolDefinitions() => new object[]
    {
        Tool("list_tests", "List the AgentLoop tests discovered under tests/ (id, title, framework, priority, category, suite, tags).", EmptySchema()),
        Tool("validate_plan",
            "Validate test YAML with the same checker the CLI uses. Pass a repo-relative 'path' to validate one plan, or omit it to validate all discovered plans.",
            ObjSchema(("path", "string", "Repo-relative path to a single plan YAML (optional).", false))),
        Tool("list_runs", "List recorded runs from runs/ (runId, testId, result, score, started/ended, step count).", EmptySchema()),
        Tool("get_run", "Get one run's full report.json by runId.",
            ObjSchema(("runId", "string", "The run id (the runs/<runId>/ folder name).", true))),
        Tool("show_prompt", "Preview the exact prompt the LLM would receive for a test (key-free; no run).",
            ObjSchema(("testId", "string", "The test id to preview.", true),
                      ("path", "string", "Repo-relative plan YAML to narrow the search (optional).", false)))
    };

    private object CallTool(JsonElement prms)
    {
        if (prms.ValueKind != JsonValueKind.Object || !prms.TryGetProperty("name", out var nameEl))
            throw new McpToolException("tools/call requires a 'name'.");
        var name = nameEl.GetString();
        var args = prms.TryGetProperty("arguments", out var a) ? a : default;

        object data = name switch
        {
            "list_tests" => ListTests(),
            "validate_plan" => ValidatePlan(args),
            "list_runs" => ListRuns(),
            "get_run" => GetRun(args),
            "show_prompt" => ShowPrompt(args),
            "create_test" => CreateTest(args),
            _ => throw new McpToolException("Unknown tool: " + name)
        };

        return new { content = new[] { new { type = "text", text = Serialize(data) } }, isError = false };
    }

    // --- Tools (read-only, key-free; reuse the same loaders as the CLI/dashboard) ---

    private object ListTests()
    {
        var tests = new List<object>();
        foreach (var planPath in TestPlanLoader.DiscoverPlanPaths(_repoRoot))
        {
            TestPlan plan;
            try { plan = TestPlanLoader.Load(planPath); } catch { continue; }
            foreach (var t in plan.Tests)
                tests.Add(new
                {
                    planPath = Relative(planPath),
                    suite = plan.Suite,
                    id = t.Id,
                    title = t.Title,
                    framework = t.Framework,
                    priority = t.Priority,
                    category = t.Category.ToString(),
                    tags = t.Tags
                });
        }
        return new { count = tests.Count, tests };
    }

    private object ValidatePlan(JsonElement args)
    {
        string? relPath = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("path", out var pe)
            ? pe.GetString() : null;

        var paths = string.IsNullOrWhiteSpace(relPath)
            ? TestPlanLoader.DiscoverPlanPaths(_repoRoot)
            : new List<string> { ResolveUnderRepo(relPath!) ?? throw new McpToolException("path is outside the repository or not found.") };

        var errors = new List<string>();
        var warnings = new List<string>();
        var planResults = new List<object>();
        var testCount = 0;
        foreach (var path in paths)
        {
            TestPlan plan;
            try { plan = TestPlanLoader.Load(path); }
            catch (Exception ex) { errors.Add($"{Relative(path)}: {ex.Message}"); continue; }
            testCount += plan.Tests.Count;
            var result = TestPlanValidator.Validate(plan, Relative(path));
            planResults.Add(new { path = Relative(path), valid = result.IsValid, errors = result.Errors, warnings = result.Warnings });
            errors.AddRange(result.Errors);
            warnings.AddRange(result.Warnings);
        }

        return new { valid = errors.Count == 0, planCount = planResults.Count, testCount, errorCount = errors.Count, warningCount = warnings.Count, plans = planResults, errors, warnings };
    }

    private object ShowPrompt(JsonElement args)
    {
        var testId = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("testId", out var te)
            ? te.GetString() : null;
        if (string.IsNullOrWhiteSpace(testId))
            throw new McpToolException("show_prompt requires a 'testId'.");

        string? relPath = args.TryGetProperty("path", out var pe) ? pe.GetString() : null;
        var paths = string.IsNullOrWhiteSpace(relPath)
            ? TestPlanLoader.DiscoverPlanPaths(_repoRoot)
            : new List<string> { ResolveUnderRepo(relPath!) ?? throw new McpToolException("path is outside the repository or not found.") };

        TestDefinition? test = null;
        foreach (var path in paths)
        {
            try { test = TestPlanLoader.Load(path).FindById(testId!); } catch { continue; }
            if (test != null) break;
        }
        if (test == null)
            throw new McpToolException("Test not found: " + testId);

        return new { testId, prompt = PromptPreview.BuildForTest(test) };
    }

    // --- Authoring (opt-in write; --mcp-allow-write / AGENTLOOP_MCP_ALLOW_WRITE=1) ---

    /// <summary>
    /// Author a test: build YAML via the SAME emitter the dashboard uses
    /// (<see cref="Dashboard.DashboardApi.BuildYaml"/>, authoring_agent "mcp"), validate it with the
    /// CLI validator, and on success write <c>tests/created/&lt;id&gt;.yaml</c>. Writes are off unless
    /// the server was started with writes enabled; otherwise this is a clear tool error. The id is
    /// guarded with the same safe-segment check the dashboard uses, so it can't escape the folder.
    /// </summary>
    private object CreateTest(JsonElement args)
    {
        if (!_allowWrite)
            throw new McpToolException(
                "create_test is disabled: writes are off by default. Enable with --mcp-allow-write or AGENTLOOP_MCP_ALLOW_WRITE=1.");

        if (args.ValueKind != JsonValueKind.Object)
            throw new McpToolException("create_test requires arguments (at least 'id' and 'goal').");

        string? Str(string name) => args.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.String
            ? e.GetString() : null;
        List<string>? StrList(string name)
        {
            if (!args.TryGetProperty(name, out var e) || e.ValueKind != JsonValueKind.Array) return null;
            var list = new List<string>();
            foreach (var item in e.EnumerateArray())
                if (item.ValueKind == JsonValueKind.String && item.GetString() is { } s && s.Length > 0)
                    list.Add(s);
            return list.Count > 0 ? list : null;
        }

        var id = Str("id");
        if (string.IsNullOrWhiteSpace(id) || !Dashboard.DashboardApi.IsSafeSegment(id))
            throw new McpToolException("A safe test id is required (letters, digits, '-', '_').");
        if (string.IsNullOrWhiteSpace(Str("goal")))
            throw new McpToolException("A goal is required.");

        int? maxSteps = args.TryGetProperty("maxSteps", out var ms) && ms.ValueKind == JsonValueKind.Number
            && ms.TryGetInt32(out var n) ? n : null;

        var req = new Dashboard.DashboardApi.CreateTestRequest
        {
            Id = id,
            Goal = Str("goal"),
            Framework = Str("framework"),
            Title = Str("title"),
            TargetWindow = Str("targetWindow"),
            Category = Str("category"),
            SuccessCondition = Str("successCondition"),
            MaxSteps = maxSteps,
            AllowedActions = StrList("allowedActions"),
            Tags = StrList("tags"),
            Suite = Str("suite"),
            Priority = Str("priority"),
            // One emitter, one provenance marker: the MCP adapter authored this.
            AuthoringAgent = "mcp"
        };

        var yaml = Dashboard.DashboardApi.BuildYaml(req);

        TestPlan plan;
        try { plan = TestPlanLoader.Parse(yaml, id); }
        catch (Exception ex) { throw new McpToolException("Generated YAML is invalid: " + ex.Message); }

        var validation = TestPlanValidator.Validate(plan, id!);
        if (!validation.IsValid)
            throw new McpToolException("Validation failed: " + string.Join("; ", validation.Errors));

        var dir = Path.Combine(_repoRoot, "tests", "created");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, id + ".yaml");
        File.WriteAllText(path, yaml);

        return new
        {
            ok = true,
            id,
            planPath = Relative(path),
            warnings = validation.Warnings.Select(TestPlanValidator.StripLocationPrefix).ToList()
        };
    }

    private object ListRuns()
    {
        var runs = RunArtifactLoader.LoadFromDirectory(_runsRoot)
            .OrderByDescending(r => r.StartedAt)
            .Select(r => new
            {
                runId = r.RunId,
                testId = r.TestId,
                result = r.Result,
                finalScore = r.FinalScore,
                startedAt = r.StartedAt,
                endedAt = r.EndedAt,
                steps = r.Steps.Count
            })
            .ToList();
        return new { count = runs.Count, runs };
    }

    private object GetRun(JsonElement args)
    {
        var runId = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("runId", out var re)
            ? re.GetString() : null;
        if (string.IsNullOrWhiteSpace(runId) || !IsSafeSegment(runId!))
            throw new McpToolException("A safe runId is required.");

        var reportPath = Path.Combine(_runsRoot, runId!, "report.json");
        if (!File.Exists(reportPath))
            throw new McpToolException("Run not found: " + runId);

        // The report is already camelCase JSON — pass it through as the tool's text content.
        using var doc = JsonDocument.Parse(File.ReadAllText(reportPath));
        return doc.RootElement.Clone();
    }

    // --- JSON-RPC + schema helpers ---

    private static object Tool(string name, string description, object inputSchema) =>
        new { name, description, inputSchema };

    private static object EmptySchema() => new { type = "object", properties = new { } };

    /// <summary>Input schema for the opt-in <c>create_test</c> authoring tool (scalars + string arrays).</summary>
    private static object CreateTestSchema()
    {
        static object Str(string desc) => new { type = "string", description = desc };
        static object StrArray(string desc) => new { type = "array", items = new { type = "string" }, description = desc };
        var props = new Dictionary<string, object>
        {
            ["id"] = Str("Test id (letters, digits, '-', '_'); also the file name under tests/created/."),
            ["goal"] = Str("Plain-language goal for the agent to accomplish."),
            ["framework"] = Str("Target UI framework (e.g. winforms, wpf, avalonia, maui)."),
            ["title"] = Str("Human-readable test title."),
            ["targetWindow"] = Str("Target window title to attach to."),
            ["category"] = Str("Smoke | Monkey | Audit | Scenario (default Scenario)."),
            ["successCondition"] = Str("Status text that proves success."),
            ["maxSteps"] = new { type = "integer", description = "Step budget (default 8)." },
            ["allowedActions"] = StrArray("Allow-list of action verbs (e.g. EnterText, Click, Done)."),
            ["tags"] = StrArray("Free-form tags."),
            ["suite"] = Str("Suite name (default 'created')."),
            ["priority"] = Str("Priority label (optional).")
        };
        return new { type = "object", properties = props, required = new[] { "id", "goal" } };
    }

    private static object ObjSchema(params (string Name, string Type, string Desc, bool Required)[] fields)
    {
        var props = new Dictionary<string, object>();
        var required = new List<string>();
        foreach (var f in fields)
        {
            props[f.Name] = new { type = f.Type, description = f.Desc };
            if (f.Required) required.Add(f.Name);
        }
        return required.Count > 0
            ? (object)new { type = "object", properties = props, required }
            : new { type = "object", properties = props };
    }

    private static object ToolError(string message) =>
        new { content = new[] { new { type = "text", text = message } }, isError = true };

    private static object Result(object? id, object result) => new { jsonrpc = "2.0", id, result };
    private static object Error(object? id, int code, string message) =>
        new { jsonrpc = "2.0", id, error = new { code, message } };

    private static object? IdValue(JsonElement idEl) => idEl.ValueKind switch
    {
        JsonValueKind.Number => idEl.TryGetInt64(out var n) ? n : (object)idEl.GetDouble(),
        JsonValueKind.String => idEl.GetString(),
        _ => null
    };

    private static string Serialize(object value) => JsonSerializer.Serialize(value, Json);

    private string Relative(string fullPath)
    {
        var prefix = Path.GetFullPath(_repoRoot) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(fullPath);
        return full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? full[prefix.Length..].Replace(Path.DirectorySeparatorChar, '/')
            : fullPath;
    }

    private string? ResolveUnderRepo(string relative)
    {
        var root = Path.GetFullPath(_repoRoot) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(_repoRoot, relative));
        return full.StartsWith(root, StringComparison.OrdinalIgnoreCase) && File.Exists(full) ? full : null;
    }

    private static bool IsSafeSegment(string s)
    {
        foreach (var c in s)
            if (!char.IsLetterOrDigit(c) && c != '-' && c != '_')
                return false;
        return s.Length > 0;
    }
}

/// <summary>A tool-level failure (bad args, not found): surfaced as an MCP <c>isError</c> result.</summary>
public sealed class McpToolException(string message) : Exception(message);
