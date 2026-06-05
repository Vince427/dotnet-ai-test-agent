using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using DesktopAiTestAgent.AgentRunner.Dashboard;
using DesktopAiTestAgent.Core;
using DesktopAiTestAgent.UIAutomation;

namespace DesktopAiTestAgent.AgentRunner;

/// <summary>
/// AgentLoop agent orchestrator.
/// Implements: observe → decide → act → score → record loop
/// with retry, loop detection, scoring, structured logging, and run artifacts.
/// </summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        // --- Load Config ---
        var manualOnlyRequested =
            HasArgument(args, "--render-ui") ||
            HasArgument(args, "--validate-plan") ||
            HasArgument(args, "--list-tests") ||
            HasArgument(args, "--write-guard-demos") ||
            HasArgument(args, "--to-junit") ||
            HasArgument(args, "--dashboard") ||
            HasArgument(args, "--bridge-llm") ||
            HasArgument(args, "--mcp") ||
            HasArgument(args, "--show-prompt") ||
            HasArgument(args, "--compose-recording") ||
            HasArgument(args, "--analytics") ||
            HasArgument(args, "--heal-apply") ||
            HasArgument(args, "--record");
        var jsonManualOutputRequested =
            manualOnlyRequested &&
            HasOptionValue(args, "--format", "json");
        // --compose-recording prints the YAML draft to stdout when no --out is given, so keep stdout clean.
        var composeToStdout =
            HasArgument(args, "--compose-recording") && !HasArgument(args, "--out");
        // --record prints the captured session JSON to stdout when no --out is given; keep stdout clean.
        var recordToStdout =
            HasArgument(args, "--record") && !HasArgument(args, "--out");
        // --vision-bridge is a runtime loop but key-free (the external agent is the VLM), so it needs no .env.
        var keyFreeRuntime = HasArgument(args, "--vision-bridge") || HasArgument(args, "--replay");
        var config = WorkflowConfig.Load(
            loadDotEnv: !manualOnlyRequested && !keyFreeRuntime,
            // Keep stdout clean for commands whose stdout IS the payload: --mcp (JSON-RPC),
            // --show-prompt (the prompt text), --compose-recording (the YAML), --record (the session
            // JSON), and any --format json command.
            logConfig: !jsonManualOutputRequested
                       && !HasArgument(args, "--mcp")
                       && !HasArgument(args, "--show-prompt")
                       && !composeToStdout
                       && !recordToStdout);
        RunnerOptions options;
        try
        {
            options = RunnerOptions.Parse(args, config);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine("Invalid arguments: " + ex.Message);
            return 2;
        }

        if (options.RenderUiOnly)
        {
            var repoRoot = config.WorkflowDirectory ?? Directory.GetCurrentDirectory();

            AgentLoopWorkbenchOptions BuildWorkbenchOptions(int autoRefresh) => new()
            {
                RepoRoot = repoRoot,
                OutputPath = options.UiOutputPath!,
                RunsRoot = config.WorkspaceRoot,
                AutoRefreshSeconds = autoRefresh,
                PlanPaths = string.IsNullOrWhiteSpace(options.PlanPath)
                    ? []
                    : new List<string> { options.PlanPath! }
            };

            if (!options.Watch)
            {
                var result = AgentLoopWorkbenchGenerator.Generate(BuildWorkbenchOptions(0));
                Console.WriteLine($"AgentLoop Workbench written to {result.OutputPath} tests={result.TestCount} runs={result.RunCount}");
                return 0;
            }

            // Watch mode: regenerate on any change under the runs root, and embed a
            // browser auto-refresh so the page updates hands-free. No server, no .env.
            const int refreshSeconds = 3;
            var first = AgentLoopWorkbenchGenerator.Generate(BuildWorkbenchOptions(refreshSeconds));
            Console.WriteLine($"AgentLoop Workbench (watch) written to {first.OutputPath} tests={first.TestCount} runs={first.RunCount}");
            Console.WriteLine($"Watching {config.WorkspaceRoot} for changes. Open the file in a browser; it auto-refreshes every {refreshSeconds}s. Press Ctrl+C to stop.");

            Directory.CreateDirectory(config.WorkspaceRoot);
            using var watcher = new FileSystemWatcher(config.WorkspaceRoot)
            {
                IncludeSubdirectories = true,
                EnableRaisingEvents = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
            };

            var pending = 1; // render once at startup already done; arm for subsequent changes
            void Arm(object? _, FileSystemEventArgs __) => System.Threading.Interlocked.Exchange(ref pending, 1);
            watcher.Created += Arm;
            watcher.Changed += Arm;
            watcher.Deleted += Arm;
            watcher.Renamed += (_, __) => System.Threading.Interlocked.Exchange(ref pending, 1);

            using var stop = new System.Threading.ManualResetEventSlim(false);
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Set(); };

            while (!stop.IsSet)
            {
                stop.Wait(500);
                if (System.Threading.Interlocked.Exchange(ref pending, 0) == 1 && !stop.IsSet)
                {
                    try
                    {
                        var r = AgentLoopWorkbenchGenerator.Generate(BuildWorkbenchOptions(refreshSeconds));
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] workbench regenerated: runs={r.RunCount}");
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("Workbench regeneration failed: " + ex.Message);
                    }
                }
            }

            Console.WriteLine("Watch stopped.");
            return 0;
        }

        if (options.ValidatePlanOnly)
            return ValidatePlans(config, options);

        if (options.ListTestsOnly)
            return ListTests(config, options);

        if (options.WriteGuardDemosOnly)
            return WriteGuardDemos(options);

        if (options.ToJUnitOnly)
            return ToJUnit(config, options);

        if (options.DashboardOnly)
            return RunDashboard(config, options);

        if (options.BridgeLlmOnly)
            return RunBridgeLlm(config, options);

        if (options.McpOnly)
            return RunMcp(config, options);

        if (options.ShowPromptOnly)
            return ShowPrompt(config, options);

        if (options.ComposeRecordingOnly)
            return ComposeRecording(config, options);

        if (options.AnalyticsOnly)
            return Analytics(config, options);

        if (options.HealApplyOnly)
            return HealApply(config, options);

        if (options.RecordSessionOnly)
            return RecordSession(config, options);

        // --- Runtime agent loop ---
        // Wire the real driver + LLM decider, then hand off to the orchestrator.
        // The driver is IDisposable, so Program owns its lifetime.
        // OpenTelemetry export is opt-in (OTEL_EXPORTER_OTLP_ENDPOINT); null/no-op
        // when unset, keeping runs dependency-free. Disposed last so it flushes.
        using var telemetry = RunnerTelemetry.TryStartExport(config);
        var secretRedactor = new SecretRedactor();
        using var driver = new FlaUiDesktopDriver();

        IActionDecider decider;
        if (!string.IsNullOrWhiteSpace(options.ReplaySessionPath))
        {
            // --replay: key-free deterministic replay of a recorded session. The loop replays the
            // recorded actions instead of asking an LLM; a drifted target fails visibly and is recorded
            // as a SelectorHealer suggestion (then --heal-apply can fix it). No .env, no model.
            RecordedSession? session;
            try
            {
                var path = Path.IsPathRooted(options.ReplaySessionPath!)
                    ? options.ReplaySessionPath!
                    : Path.GetFullPath(Path.Combine(config.WorkflowDirectory ?? Directory.GetCurrentDirectory(), options.ReplaySessionPath!));
                session = JsonSerializer.Deserialize<RecordedSession>(
                    File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Could not read the replay session JSON: " + ex.Message);
                return 2;
            }
            if (session == null || session.Actions.Count == 0)
            {
                Console.Error.WriteLine("Replay session has no actions.");
                return 2;
            }
            decider = new ReplayActionDecider(session.Actions);
            Console.WriteLine($"Deterministic replay enabled (key-free): replaying {session.Actions.Count} recorded action(s), no LLM.");
        }
        else if (!string.IsNullOrWhiteSpace(options.VisionBridgeDir))
        {
            // --vision-bridge: key-free, agent-in-the-loop vision. Every step writes an annotated
            // screenshot + index to the folder; an external VLM (e.g. Claude Code on this desktop)
            // reads it and writes the box choice. No LLM, no .env — the bridge IS the decider.
            decider = new BridgeVisionDecider(
                options.VisionBridgeDir!,
                () => driver.CaptureScreenshot(),
                secretRedactor,
                log: Console.Error.WriteLine);
            Console.WriteLine($"Vision bridge enabled (key-free): writing annotated screenshots + index to '{options.VisionBridgeDir}' and awaiting vision-resp-N.json from an external agent.");
        }
        else
        {
            var llmService = new LlmService(config, secretRedactor);

            // --vision: wrap the decider in the V3 Tier-2 fallback. It uses the LLM only for Tier-1
            // (text) and escalates to a multimodal VLM (annotated screenshot + overlay index) when the
            // Tier-1 UIA target can't be resolved. The driver supplies the screenshot on demand.
            decider = llmService;
            if (options.Vision)
            {
                decider = new VisionActionDecider(
                    llmService,
                    new OpenAiVisionClient(config),
                    () => driver.CaptureScreenshot(),
                    secretRedactor);
                Console.WriteLine("Vision fallback enabled (V3 Tier-2): escalates to the VLM when UIA resolution is ambiguous.");
            }
        }

        var orchestrator = new RunOrchestrator(driver, decider, config);
        return await orchestrator.RunAsync(options);
    }

    // Manual command: print the prompt the LLM would receive for a test (--show-prompt), without
    // running anything. Key-free (reuses PromptBuilder via PromptPreview). Needs --test-id; honors
    // --plan to narrow the search, else scans tests/. Text by default, JSON with --format json.
    private static int ShowPrompt(WorkflowConfig config, RunnerOptions options)
    {
        var testId = options.TestId;
        if (string.IsNullOrWhiteSpace(testId))
        {
            Console.Error.WriteLine("--show-prompt requires --test-id.");
            return 2;
        }

        var repoRoot = config.WorkflowDirectory ?? Directory.GetCurrentDirectory();
        var paths = string.IsNullOrWhiteSpace(options.PlanPath)
            ? TestPlanLoader.DiscoverPlanPaths(repoRoot)
            : new List<string> { options.PlanPath! };

        TestDefinition? test = null;
        foreach (var path in paths)
        {
            try { test = TestPlanLoader.Load(path).FindById(testId!); }
            catch { continue; }
            if (test != null) break;
        }

        if (test == null)
        {
            Console.Error.WriteLine($"Test '{testId}' was not found under tests/.");
            return 2;
        }

        var prompt = PromptPreview.BuildForTest(test, new SecretRedactor(), config.PromptTemplate);

        if (options.OutputFormat == CommandOutputFormat.Json)
        {
            var json = JsonSerializer.Serialize(
                new { kind = "promptPreview", testId, prompt },
                new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine(json);
        }
        else
        {
            Console.WriteLine(prompt);
        }
        return 0;
    }

    // Manual command: compose a recorded-session JSON into a validated YAML test draft
    // (--compose-recording <session.json> [--out <draft.yaml>], V9.5 recording mode). Key-free, no
    // app needed: capture is the env-bound step that produces the JSON; this is the pure transform.
    // Stdout is the YAML when no --out is given (diagnostics go to stderr).
    private static int ComposeRecording(WorkflowConfig config, RunnerOptions options)
    {
        var input = options.RecordingInputPath;
        if (string.IsNullOrWhiteSpace(input))
        {
            Console.Error.WriteLine("--compose-recording requires a path to a recorded-session JSON.");
            return 2;
        }

        var baseDir = config.WorkflowDirectory ?? Directory.GetCurrentDirectory();
        var inputPath = Path.IsPathRooted(input!) ? input! : Path.GetFullPath(Path.Combine(baseDir, input!));
        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"Recorded-session file not found: {inputPath}");
            return 2;
        }

        RecordedSession? session;
        try
        {
            session = JsonSerializer.Deserialize<RecordedSession>(
                File.ReadAllText(inputPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Invalid recorded-session JSON: " + ex.Message);
            return 2;
        }

        if (session == null)
        {
            Console.Error.WriteLine("Recorded-session JSON was empty.");
            return 2;
        }

        var result = RecordingComposer.Compose(session, redactor: new SecretRedactor());
        if (!result.IsValid)
        {
            Console.Error.WriteLine("Composed draft did not validate:");
            foreach (var e in result.Errors) Console.Error.WriteLine("  ERROR " + e);
            return 1;
        }
        foreach (var w in result.Warnings) Console.Error.WriteLine("  WARN " + w);

        if (!string.IsNullOrWhiteSpace(options.RecordingOutputPath))
        {
            var outPath = options.RecordingOutputPath!;
            var dir = Path.GetDirectoryName(outPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(outPath, result.Yaml);
            Console.Error.WriteLine($"Wrote YAML draft for {result.TestId} to {outPath}");
        }
        else
        {
            Console.WriteLine(result.Yaml);
        }

        return 0;
    }

    // Manual command: derive analytics from the run history under runs/ (--analytics, V11). Key-free,
    // read-only: loads every runs/<id>/report.json via RunArtifactLoader and hands the in-memory list
    // to the pure RunAnalytics.Compute. Text summary by default; --format json emits the structured
    // result (stdout-clean — only the JSON payload, diagnostics go to stderr).
    private static int Analytics(WorkflowConfig config, RunnerOptions options)
    {
        var runs = RunArtifactLoader.LoadFromDirectory(config.WorkspaceRoot);
        var result = RunAnalytics.Compute(runs);

        if (options.OutputFormat == CommandOutputFormat.Json)
        {
            WriteJson(result);
            return 0;
        }

        Console.WriteLine($"Run analytics: totalRuns={result.TotalRuns} tests={result.Tests.Count} flaky={result.FlakyTestCount} selectorDrift={result.SelectorDriftCount}");
        if (result.TotalRuns == 0)
        {
            Console.WriteLine("No runs found under " + config.WorkspaceRoot + ". Run a test (artifacts land in runs/) then re-run --analytics.");
            return 0;
        }

        Console.WriteLine(
            $"Durations: runsWithDuration={result.RunsWithDuration} avg={result.AverageRunDurationSeconds}s max={result.MaxRunDurationSeconds}s | avgStepCount={result.AverageStepCount} (totalSteps={result.TotalSteps})");

        Console.WriteLine();
        Console.WriteLine("Per-test (id / runs / passed / failed / flaky):");
        foreach (var t in result.Tests)
            Console.WriteLine($"  {t.TestId}\t{t.Runs}\t{t.Passed}\t{t.Failed}\t{(t.Flaky ? "FLAKY" : "-")}");

        if (result.MostFailingTests.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Most-failing tests:");
            foreach (var t in result.MostFailingTests)
                Console.WriteLine($"  {t.TestId}\tfailed={t.Failed}/{t.Runs}");
        }

        if (result.SelectorDrift.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Selector drift (old -> new / count / maxConfidence):");
            foreach (var d in result.SelectorDrift)
                Console.WriteLine($"  {d.OldTarget} -> {d.NewTarget}\t{d.Count}\t{d.MaxConfidence}%");
        }

        return 0;
    }

    // Manual command: --heal-apply --run <id> [--plan <path>] [--yes]. Local-only, key-free. Takes a
    // run's evidence-only selector-drift suggestions and applies them to the test's `selectors` —
    // a SURGICAL edit (only the selectors line changes) VERIFIED by TestFactGuard (the rewrite must
    // change nothing but selectors, else it's refused). Dry-run preview unless --yes. Single-test files
    // only (multi-test files: edit by hand), so it can't clobber a sibling test.
    private static int HealApply(WorkflowConfig config, RunnerOptions options)
    {
        var repoRoot = config.WorkflowDirectory ?? Directory.GetCurrentDirectory();
        var runDir = Path.Combine(config.WorkspaceRoot, options.HealRunId!);
        var run = RunArtifactLoader.LoadFromDirectory(runDir).FirstOrDefault();
        if (run == null)
        {
            Console.Error.WriteLine($"Run '{options.HealRunId}' not found under {config.WorkspaceRoot}.");
            return 2;
        }
        if (string.IsNullOrWhiteSpace(run.TestId))
        {
            Console.Error.WriteLine("That run has no testId, so its test can't be located.");
            return 2;
        }

        var planPaths = string.IsNullOrWhiteSpace(options.PlanPath)
            ? TestPlanLoader.DiscoverPlanPaths(repoRoot)
            : new List<string> { options.PlanPath! };

        string? planPath = null;
        TestDefinition? test = null;
        foreach (var p in planPaths)
        {
            TestPlan plan;
            try { plan = TestPlanLoader.Load(p); } catch { continue; }
            var t = plan.FindById(run.TestId!);
            if (t == null) continue;
            if (plan.Tests.Count != 1)
            {
                Console.Error.WriteLine($"Test '{run.TestId}' lives in a multi-test file ({p}); --heal-apply rewrites single-test files only (edit multi-test files by hand).");
                return 2;
            }
            planPath = p; test = t; break;
        }
        if (test == null || planPath == null)
        {
            Console.Error.WriteLine($"Could not find a single-test plan for testId '{run.TestId}'.");
            return 2;
        }

        var healPlan = HealApplier.Plan(run, test);
        if (!healPlan.HasChanges)
        {
            Console.WriteLine($"No selector heals to apply for {run.TestId}: the run's drift suggestions don't match any of the test's declared `selectors`.");
            return 0;
        }

        Console.WriteLine($"Proposed selector heals for {run.TestId} ({planPath}):");
        foreach (var r in healPlan.Replacements)
            Console.WriteLine($"  {r.Old} -> {r.New}  ({r.Confidence}% match)");

        if (!options.HealConfirmed)
        {
            Console.WriteLine("Dry run. Re-run with --yes to apply (local-only, the file shows in Git).");
            return 0;
        }

        var original = File.ReadAllText(planPath);
        var rewritten = HealApplier.RewriteSelectorsInYaml(original, healPlan.Replacements);
        if (rewritten == original)
        {
            Console.Error.WriteLine("No `selectors:` entry matched in the YAML; nothing written.");
            return 1;
        }

        TestDefinition after;
        try { after = TestPlanLoader.Parse(rewritten, run.TestId)!.FindById(run.TestId!)!; }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Rewrite produced invalid YAML; not writing: " + ex.Message);
            return 1;
        }

        // The fact-gate is the safety net: the surgical edit must change ONLY `selectors`.
        var guard = TestFactGuard.Verify(test, after, allowedToChange: new[] { "selectors" });
        if (!guard.Ok)
        {
            Console.Error.WriteLine("Refusing to write — the rewrite changed more than `selectors`:");
            foreach (var v in guard.Violations)
                Console.Error.WriteLine("  " + v);
            return 1;
        }

        File.WriteAllText(planPath, rewritten);
        Console.WriteLine($"Applied {healPlan.Replacements.Count} selector heal(s) to {planPath}.");
        return 0;
    }

    // Manual command: live-capture a manual UIA session into a session.json (--record --window <title>
    // [--out <session.json>] [--seconds N], V9.5 inc.2b). ENV-BOUND: needs a real interactive desktop +
    // the running target app — it can't be exercised headless. Key-free (no .env / LLM). Attaches via
    // FlaUI/UIA3, subscribes to automation events, smooths them through the pure SessionRecorder, then
    // writes the RecordedSession JSON the existing --compose-recording consumes. Secret VALUES are
    // redacted AT CAPTURE (SecretRedactor.RedactValue, keyed by IsPassword + identifier) so a password
    // never lands on disk. Stdout is the session JSON when no --out is given (diagnostics go to stderr).
    private static int RecordSession(WorkflowConfig config, RunnerOptions options)
    {
        var window = options.TargetWindow;
        if (string.IsNullOrWhiteSpace(window))
        {
            Console.Error.WriteLine("--record requires --window <title>.");
            return 2;
        }

        var redactor = new SecretRedactor();
        var sink = new SessionRecorder { Window = window, Title = window };

        using var recorder = new UiaSessionRecorder(
            sink.Observe,
            redactor.RedactValue,
            diagnostics: msg => Console.Error.WriteLine("  " + msg));

        Console.Error.WriteLine($"Attaching to window \"{window}\"...");
        bool attached;
        try
        {
            attached = recorder.Attach(window, TimeSpan.FromSeconds(15));
        }
        catch (Exception ex)
        {
            // Env-bound: UIA needs a real interactive desktop session. Report cleanly, don't crash.
            Console.Error.WriteLine($"Could not attach via UI Automation: {ex.Message}");
            Console.Error.WriteLine("--record needs a real interactive Windows desktop session and the target app running.");
            return 1;
        }
        if (!attached)
        {
            Console.Error.WriteLine($"Could not find window \"{window}\". Is the target app running?");
            return 1;
        }

        sink.Title = recorder.WindowTitle ?? window;
        Console.Error.WriteLine(
            $"Recording \"{recorder.WindowTitle}\" for up to {options.RecordSeconds}s. " +
            "Interact with the app now; press Ctrl+C to stop early.");

        using var stop = new System.Threading.ManualResetEventSlim(false);
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Set(); };
        stop.Wait(TimeSpan.FromSeconds(options.RecordSeconds));

        var session = sink.ToSession();
        Console.Error.WriteLine($"Captured {session.Actions.Count} action(s).");

        var json = JsonSerializer.Serialize(
            session,
            new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

        if (!string.IsNullOrWhiteSpace(options.RecordOutputPath))
        {
            var outPath = options.RecordOutputPath!;
            var dir = Path.GetDirectoryName(outPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(outPath, json);
            Console.Error.WriteLine($"Wrote session to {outPath}. Compose it with: --compose-recording {outPath}");
        }
        else
        {
            Console.WriteLine(json);
        }

        return 0;
    }

    // Manual command: serve the MCP adapter over stdio (--mcp). An adapter over the same CLI
    // contract — exposes list/validate/read tools, no .env, nothing that spawns a run. Writes are
    // read-only by default; the opt-in create_test authoring tool is enabled only with
    // --mcp-allow-write (or AGENTLOOP_MCP_ALLOW_WRITE=1). stdout MUST carry only JSON-RPC, so we
    // write nothing else there.
    private static int RunMcp(WorkflowConfig config, RunnerOptions options)
    {
        var repoRoot = config.WorkflowDirectory ?? Directory.GetCurrentDirectory();
        var server = new Mcp.McpServer(repoRoot, config.WorkspaceRoot, options.McpAllowWrite);

        string? line;
        while ((line = Console.In.ReadLine()) != null)
        {
            var response = server.HandleLine(line);
            if (response != null)
            {
                Console.Out.WriteLine(response);
                Console.Out.Flush();
            }
        }
        return 0;
    }

    // Manual command: serve the local-only all-in-one dashboard (OBS-2). No .env
    // required to start; launching a run from it spawns the CLI, which then needs
    // the user's target app + provider config.
    private static int RunDashboard(WorkflowConfig config, RunnerOptions options)
    {
        var repoRoot = config.WorkflowDirectory ?? Directory.GetCurrentDirectory();
        using var server = new DashboardServer(repoRoot, config.WorkspaceRoot, options.DashboardPort);
        try
        {
            server.Start();
        }
        catch (HttpListenerException ex)
        {
            Console.Error.WriteLine($"Failed to start dashboard on port {options.DashboardPort}: {ex.Message}");
            return 1;
        }

        Console.WriteLine($"AgentLoop Dashboard (local-only, not for CI) at {server.Url}");
        Console.WriteLine("Serves tests/ + runs/. Launching a run spawns the CLI (needs your target app + .env). Press Ctrl+C to stop.");
        server.WaitForShutdown();
        Console.WriteLine("Dashboard stopped.");
        return 0;
    }

    // Manual command: serve a "human/agent in the loop" OpenAI-compatible endpoint so a
    // person or an external agent (e.g. Claude Code) can be the decider with no provider
    // key. Point a run's LLM_ENDPOINT at the printed URL; answer each req-N.txt with a
    // resp-N.json action. Manual-first (no .env to start).
    private static int RunBridgeLlm(WorkflowConfig config, RunnerOptions options)
    {
        var repoRoot = config.WorkflowDirectory ?? Directory.GetCurrentDirectory();
        var ioDir = options.BridgeIoDir ?? Path.Combine(repoRoot, "bridge-io");
        using var server = new BridgeLlmServer(ioDir, options.BridgePort);
        try
        {
            server.Start();
        }
        catch (HttpListenerException ex)
        {
            Console.Error.WriteLine($"Failed to start bridge on port {options.BridgePort}: {ex.Message}");
            return 1;
        }

        Console.WriteLine($"Bridge LLM (no key) at {server.BaseUrl} — set LLM_ENDPOINT to this URL for a run.");
        Console.WriteLine($"Per step it writes {server.IoDir}\\req-N.txt; reply with resp-N.json (an action). Ctrl+C to stop.");
        server.WaitForShutdown();
        Console.WriteLine("Bridge stopped.");
        return 0;
    }

    private static int WriteGuardDemos(RunnerOptions options)
    {
        var outputRoot = options.GuardDemoOutputRoot ?? Path.GetFullPath("runs");
        var result = GuardFailureDemoFactory.WriteAll(outputRoot);
        Console.WriteLine($"Guard demo artifacts written to {result.OutputRoot} runs={result.Artifacts.Count}");
        foreach (var artifact in result.Artifacts)
            Console.WriteLine($"- {artifact.RunId}: {artifact.TestId} result={artifact.Result}");

        return 0;
    }

    // Manual command: convert captured run artifacts (runs/<id>/report.json) into a
    // JUnit XML report for CI dashboards. No .env / LLM / desktop app required.
    private static int ToJUnit(WorkflowConfig config, RunnerOptions options)
    {
        var runs = RunArtifactLoader.LoadFromDirectory(config.WorkspaceRoot);
        var xml = JUnitReportWriter.Write(runs);

        var outputPath = Path.GetFullPath(options.JUnitOutputPath!);
        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir))
            Directory.CreateDirectory(outputDir);
        File.WriteAllText(outputPath, xml);

        Console.WriteLine($"JUnit report written to {outputPath} runs={runs.Count}");
        return 0;
    }

    private static bool HasArgument(string[] args, string name)
    {
        return Array.Exists(args, arg => string.Equals(arg, name, StringComparison.Ordinal));
    }

    private static bool HasOptionValue(string[] args, string name, string value)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.Ordinal) &&
                string.Equals(args[i + 1], value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static int ValidatePlans(WorkflowConfig config, RunnerOptions options)
    {
        var planPaths = ResolvePlanPaths(config, options);
        var output = new PlanValidationOutput
        {
            PlanCount = planPaths.Count
        };

        if (planPaths.Count == 0)
        {
            output.Errors.Add("No test plans found. Use --plan <path>, --suite <name>, or add YAML files under tests/.");
            output.ErrorCount = output.Errors.Count;
            output.Valid = false;
            WriteValidationOutput(options, output);
            return 2;
        }

        foreach (var planPath in planPaths)
        {
            var planOutput = new PlanValidationPlanOutput { Path = planPath };
            output.Plans.Add(planOutput);

            try
            {
                var plan = TestPlanLoader.Load(planPath);
                planOutput.Suite = plan.Suite;
                planOutput.TestCount = plan.Tests.Count;
                output.TestCount += plan.Tests.Count;

                if (!string.IsNullOrWhiteSpace(options.TestId) && plan.FindById(options.TestId!) == null)
                {
                    planOutput.Errors.Add($"{planPath}:{options.TestId}: test id was not found.");
                }

                var validation = TestPlanValidator.Validate(plan, planPath);
                foreach (var error in validation.Errors)
                {
                    planOutput.Errors.Add(error);
                }
                foreach (var warning in validation.Warnings)
                {
                    planOutput.Warnings.Add(warning);
                }
            }
            catch (Exception ex)
            {
                planOutput.Errors.Add($"{planPath}: {ex.Message}");
            }

            planOutput.Valid = planOutput.Errors.Count == 0;
            output.Errors.AddRange(planOutput.Errors);
            output.Warnings.AddRange(planOutput.Warnings);
        }

        output.WarningCount = output.Warnings.Count;
        output.ErrorCount = output.Errors.Count;
        output.Valid = output.ErrorCount == 0;
        WriteValidationOutput(options, output);
        return output.Valid ? 0 : 2;
    }

    private static void WriteValidationOutput(RunnerOptions options, PlanValidationOutput output)
    {
        if (options.OutputFormat == CommandOutputFormat.Json)
        {
            WriteJson(output);
            return;
        }

        foreach (var plan in output.Plans)
        {
            if (plan.Valid)
                Console.WriteLine($"OK {plan.Path} suite={plan.Suite ?? "-"} tests={plan.TestCount}");
            else
                foreach (var error in plan.Errors)
                    Console.Error.WriteLine("ERROR " + error);
        }

        foreach (var error in output.Errors)
        {
            if (!output.Plans.Exists(plan => plan.Errors.Contains(error)))
                Console.Error.WriteLine(error);
        }

        // Policy advisories (non-fatal) go to stderr so stdout stays clean for OK lines.
        foreach (var warning in output.Warnings)
            Console.Error.WriteLine("WARN " + warning);

        if (output.Valid)
            Console.WriteLine($"Validation passed: plans={output.PlanCount} tests={output.TestCount} warnings={output.WarningCount}");
        else
            Console.Error.WriteLine($"Validation failed: plans={output.PlanCount} tests={output.TestCount} errors={output.ErrorCount}");
    }

    private static int ListTests(WorkflowConfig config, RunnerOptions options)
    {
        var planPaths = ResolvePlanPaths(config, options);
        var output = new TestListOutput();

        if (planPaths.Count == 0)
        {
            output.Errors.Add("No test plans found. Use --plan <path>, --suite <name>, or add YAML files under tests/.");
            output.Valid = false;
            WriteTestListOutput(options, output);
            return 2;
        }

        foreach (var planPath in planPaths)
        {
            TestPlan plan;
            try
            {
                plan = TestPlanLoader.Load(planPath);
            }
            catch (Exception ex)
            {
                output.Errors.Add($"{planPath}: {ex.Message}");
                continue;
            }

            foreach (var test in plan.Tests)
            {
                if (!string.IsNullOrWhiteSpace(options.TestId) &&
                    !string.Equals(test.Id, options.TestId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                output.Tests.Add(new ListedTestOutput
                {
                    PlanPath = planPath,
                    Suite = plan.Suite,
                    Id = test.Id,
                    Title = test.Title,
                    Priority = test.Priority,
                    Framework = test.Framework,
                    TargetWindow = test.TargetWindow,
                    SourceIssue = test.SourceIssue,
                    SourcePr = test.SourcePr,
                    AuthoringAgent = test.AuthoringAgent,
                    Risk = test.Risk,
                    CiProfile = test.CiProfile,
                    Goal = test.Goal,
                    SuccessCondition = test.SuccessCondition,
                    MaxSteps = test.MaxSteps,
                    AllowedActions = new List<string>(test.AllowedActions),
                    Tags = new List<string>(test.Tags),
                    ExistingTests = new List<string>(test.ExistingTests)
                });
            }
        }

        output.Count = output.Tests.Count;
        if (output.Count == 0 && output.Errors.Count == 0)
        {
            output.Errors.Add($"No tests matched '{options.TestId}'.");
        }

        output.Valid = output.Errors.Count == 0;
        WriteTestListOutput(options, output);
        return output.Valid ? 0 : 2;
    }

    private static void WriteTestListOutput(RunnerOptions options, TestListOutput output)
    {
        if (options.OutputFormat == CommandOutputFormat.Json)
        {
            WriteJson(output);
            return;
        }

        foreach (var error in output.Errors)
            Console.Error.WriteLine("ERROR " + error);

        if (output.Errors.Count > 0)
            return;

        Console.WriteLine("ID\tSuite\tFramework\tPriority\tTitle\tTarget");
        foreach (var test in output.Tests)
            Console.WriteLine($"{test.Id}\t{test.Suite ?? "-"}\t{test.Framework ?? "-"}\t{test.Priority ?? "-"}\t{test.Title ?? "-"}\t{test.TargetWindow ?? "-"}");
    }

    private static List<string> ResolvePlanPaths(WorkflowConfig config, RunnerOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.PlanPath))
            return [options.PlanPath!];

        var repoRoot = config.WorkflowDirectory ?? Directory.GetCurrentDirectory();
        return TestPlanLoader.DiscoverPlanPaths(repoRoot);
    }

    private static void WriteJson<T>(T value)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        options.Converters.Add(new JsonStringEnumConverter());
        Console.WriteLine(JsonSerializer.Serialize(value, options));
    }
}
