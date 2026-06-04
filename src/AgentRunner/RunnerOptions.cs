using System;
using System.IO;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner;

public sealed class RunnerOptions
{
    public const string DefaultWindowTitle = "Sample Login App (.NET 8)";

    public string TargetWindow { get; set; } = DefaultWindowTitle;
    public string? GoalName { get; set; }
    public string? PlanPath { get; set; }
    public string? Suite { get; set; }
    public string? TestId { get; set; }
    public TestDefinition? Test { get; set; }
    public bool RenderUiOnly { get; set; }
    public bool Watch { get; set; }
    public bool ValidatePlanOnly { get; set; }
    public bool ListTestsOnly { get; set; }
    public bool WriteGuardDemosOnly { get; set; }
    public bool ToJUnitOnly { get; set; }
    public bool DashboardOnly { get; set; }
    public int DashboardPort { get; set; } = 8090;
    public bool BridgeLlmOnly { get; set; }
    public int BridgePort { get; set; } = 8088;
    public string? BridgeIoDir { get; set; }

    /// <summary>Wrap the decider in the V3 Tier-2 <c>VisionActionDecider</c> (vision fallback).</summary>
    public bool Vision { get; set; }

    /// <summary>Serve the read-only MCP adapter over stdio (`--mcp`).</summary>
    public bool McpOnly { get; set; }
    public string? JUnitOutputPath { get; set; }
    public string? UiOutputPath { get; set; }
    public string? GuardDemoOutputRoot { get; set; }
    public EvidenceLevel EvidenceLevel { get; set; } = EvidenceLevel.Standard;
    public CommandOutputFormat OutputFormat { get; set; } = CommandOutputFormat.Text;
    public AgentGoal Goal { get; set; } = new();

    public static RunnerOptions Parse(string[] args, WorkflowConfig config)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));

        var targetWindow = DefaultWindowTitle;
        string? goalName = null;
        string? goalDescription = null;
        string? successCondition = null;
        string? goalId = null;
        string? planPath = null;
        string? suite = null;
        string? testId = null;
        string? uiOutputPath = null;
        string? guardDemoOutputRoot = null;
        var validatePlanOnly = false;
        var listTestsOnly = false;
        var writeGuardDemosOnly = false;
        var toJUnitOnly = false;
        var dashboardOnly = false;
        var dashboardPort = 8090;
        var bridgeLlmOnly = false;
        var bridgePort = 8088;
        string? bridgeIoDir = null;
        string? junitOutputPath = null;
        var watch = false;
        var vision = false;
        var mcpOnly = false;
        var evidenceLevel = EvidenceLevel.Standard;
        var outputFormat = CommandOutputFormat.Text;
        int? maxSteps = null;
        var positionalWindowSeen = false;
        var windowExplicit = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg == "--window")
            {
                targetWindow = ReadValue(args, ref i, "--window");
                windowExplicit = true;
            }
            else if (arg == "--goal-name")
                goalName = ReadValue(args, ref i, "--goal-name");
            else if (arg == "--plan")
                planPath = ReadValue(args, ref i, "--plan");
            else if (arg == "--suite")
                suite = ReadValue(args, ref i, "--suite");
            else if (arg == "--test-id")
                testId = ReadValue(args, ref i, "--test-id");
            else if (arg == "--render-ui")
                uiOutputPath = ReadValue(args, ref i, "--render-ui");
            else if (arg == "--watch")
                watch = true;
            else if (arg == "--vision")
                vision = true;
            else if (arg == "--mcp")
                mcpOnly = true;
            else if (arg == "--write-guard-demos")
            {
                writeGuardDemosOnly = true;
                if (HasOptionalValue(args, i))
                    guardDemoOutputRoot = ReadValue(args, ref i, "--write-guard-demos");
            }
            else if (arg == "--to-junit")
            {
                toJUnitOnly = true;
                if (HasOptionalValue(args, i))
                    junitOutputPath = ReadValue(args, ref i, "--to-junit");
            }
            else if (arg == "--dashboard")
            {
                dashboardOnly = true;
                if (HasOptionalValue(args, i))
                {
                    var raw = ReadValue(args, ref i, "--dashboard");
                    if (!int.TryParse(raw, out dashboardPort) || dashboardPort is <= 0 or > 65535)
                        throw new ArgumentException("--dashboard port must be between 1 and 65535.");
                }
            }
            else if (arg == "--bridge-llm")
            {
                bridgeLlmOnly = true;
                if (HasOptionalValue(args, i))
                {
                    var raw = ReadValue(args, ref i, "--bridge-llm");
                    if (!int.TryParse(raw, out bridgePort) || bridgePort is <= 0 or > 65535)
                        throw new ArgumentException("--bridge-llm port must be between 1 and 65535.");
                }
            }
            else if (arg == "--bridge-io")
                bridgeIoDir = ReadValue(args, ref i, "--bridge-io");
            else if (arg == "--validate-plan")
            {
                validatePlanOnly = true;
                if (HasOptionalValue(args, i))
                    planPath = ReadValue(args, ref i, "--validate-plan");
            }
            else if (arg == "--list-tests")
            {
                listTestsOnly = true;
                if (HasOptionalValue(args, i))
                    planPath = ReadValue(args, ref i, "--list-tests");
            }
            else if (arg == "--evidence-level")
            {
                var raw = ReadValue(args, ref i, "--evidence-level");
                if (!Enum.TryParse<EvidenceLevel>(raw, true, out evidenceLevel))
                    throw new ArgumentException("--evidence-level must be one of: minimal, standard, full.");
            }
            else if (arg == "--format")
            {
                var raw = ReadValue(args, ref i, "--format");
                if (!Enum.TryParse<CommandOutputFormat>(raw, true, out outputFormat))
                    throw new ArgumentException("--format must be one of: text, json.");
            }
            else if (arg == "--goal")
                goalDescription = ReadValue(args, ref i, "--goal");
            else if (arg == "--success")
                successCondition = ReadValue(args, ref i, "--success");
            else if (arg == "--goal-id")
                goalId = ReadValue(args, ref i, "--goal-id");
            else if (arg == "--max-steps")
            {
                var raw = ReadValue(args, ref i, "--max-steps");
                if (!int.TryParse(raw, out var parsed) || parsed <= 0)
                    throw new ArgumentException("--max-steps must be a positive integer.");
                maxSteps = parsed;
            }
            else if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unknown argument: {arg}");
            }
            else if (!positionalWindowSeen)
            {
                targetWindow = arg;
                positionalWindowSeen = true;
                windowExplicit = true;
            }
            else
            {
                throw new ArgumentException($"Unexpected positional argument: {arg}");
            }
        }

        var modeCount = 0;
        if (!string.IsNullOrWhiteSpace(uiOutputPath)) modeCount++;
        if (validatePlanOnly) modeCount++;
        if (listTestsOnly) modeCount++;
        if (writeGuardDemosOnly) modeCount++;
        if (toJUnitOnly) modeCount++;
        if (dashboardOnly) modeCount++;
        if (bridgeLlmOnly) modeCount++;
        if (mcpOnly) modeCount++;
        if (modeCount > 1)
            throw new ArgumentException("Use only one of --render-ui, --validate-plan, --list-tests, --write-guard-demos, --to-junit, --dashboard, --bridge-llm, or --mcp.");
        if (outputFormat == CommandOutputFormat.Json && !validatePlanOnly && !listTestsOnly)
            throw new ArgumentException("--format json is only supported with --validate-plan or --list-tests.");
        if (watch && string.IsNullOrWhiteSpace(uiOutputPath))
            throw new ArgumentException("--watch is only supported with --render-ui.");

        TestDefinition? selectedTest = null;
        var runtimeTestSelection = !validatePlanOnly && !listTestsOnly;
        if (runtimeTestSelection &&
            (!string.IsNullOrWhiteSpace(planPath) ||
            !string.IsNullOrWhiteSpace(suite) ||
            !string.IsNullOrWhiteSpace(testId)))
        {
            var resolvedPlanPath = ResolvePlanPath(planPath, suite, config);
            var plan = TestPlanLoader.Load(resolvedPlanPath);

            if (!string.IsNullOrWhiteSpace(suite) &&
                !string.IsNullOrWhiteSpace(plan.Suite) &&
                !string.Equals(plan.Suite, suite, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Plan suite '{plan.Suite}' does not match requested suite '{suite}'.");
            }

            if (string.IsNullOrWhiteSpace(suite))
                suite = plan.Suite;

            selectedTest = !string.IsNullOrWhiteSpace(testId)
                ? plan.FindById(testId!)
                : plan.Tests[0];

            if (selectedTest == null)
                throw new ArgumentException($"Test id '{testId}' was not found in plan '{resolvedPlanPath}'.");

            planPath = resolvedPlanPath;
        }
        else if ((validatePlanOnly || listTestsOnly) &&
            (!string.IsNullOrWhiteSpace(planPath) ||
             !string.IsNullOrWhiteSpace(suite) ||
             !string.IsNullOrWhiteSpace(testId)))
        {
            planPath = ResolvePlanPath(planPath, suite, config);
        }

        var goal = selectedTest != null
            ? selectedTest.ToAgentGoal()
            : CloneGoal(config.GetGoal(goalName));

        if (selectedTest?.TargetWindow != null && !windowExplicit)
            targetWindow = selectedTest.TargetWindow;

        if (!string.IsNullOrWhiteSpace(goalDescription))
            goal.Description = goalDescription!;
        if (successCondition != null)
            goal.SuccessCondition = string.IsNullOrWhiteSpace(successCondition) ? null : successCondition;
        if (!string.IsNullOrWhiteSpace(goalId))
            goal.Identifier = goalId;
        if (maxSteps.HasValue)
            goal.MaxSteps = maxSteps.Value;

        return new RunnerOptions
        {
            TargetWindow = targetWindow,
            GoalName = goalName,
            PlanPath = planPath,
            Suite = suite,
            TestId = selectedTest?.Id ?? testId,
            Test = selectedTest,
            RenderUiOnly = !string.IsNullOrWhiteSpace(uiOutputPath),
            Watch = watch,
            ValidatePlanOnly = validatePlanOnly,
            ListTestsOnly = listTestsOnly,
            WriteGuardDemosOnly = writeGuardDemosOnly,
            ToJUnitOnly = toJUnitOnly,
            DashboardOnly = dashboardOnly,
            DashboardPort = dashboardPort,
            BridgeLlmOnly = bridgeLlmOnly,
            BridgePort = bridgePort,
            BridgeIoDir = bridgeIoDir,
            Vision = vision,
            McpOnly = mcpOnly,
            JUnitOutputPath = toJUnitOnly
                ? ResolveOutputPath(junitOutputPath ?? "artifacts/junit-results.xml", config)
                : null,
            UiOutputPath = ResolveOutputPath(uiOutputPath, config),
            GuardDemoOutputRoot = ResolveGuardDemoOutputRoot(guardDemoOutputRoot, config),
            EvidenceLevel = evidenceLevel,
            OutputFormat = outputFormat,
            Goal = goal
        };
    }

    private static string ReadValue(string[] args, ref int index, string optionName)
    {
        if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException($"Missing value for {optionName}.");

        index++;
        return args[index];
    }

    private static bool HasOptionalValue(string[] args, int index)
    {
        return index + 1 < args.Length &&
            !args[index + 1].StartsWith("--", StringComparison.Ordinal);
    }

    private static AgentGoal CloneGoal(AgentGoal source)
    {
        return new AgentGoal
        {
            Description = source.Description,
            SuccessCondition = source.SuccessCondition,
            MaxSteps = source.MaxSteps,
            MaxRetries = source.MaxRetries,
            RetryBaseDelayMs = source.RetryBaseDelayMs,
            Identifier = source.Identifier,
            Category = source.Category,
            AllowedActions = new System.Collections.Generic.List<string>(source.AllowedActions)
        };
    }

    private static string ResolvePlanPath(string? planPath, string? suite, WorkflowConfig config)
    {
        var path = planPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            var fileName = string.IsNullOrWhiteSpace(suite) ? "smoke.yaml" : suite + ".yaml";
            path = Path.Combine("tests", fileName);
        }

        if (Path.IsPathRooted(path!))
            return Path.GetFullPath(path!);

        var baseDir = config.WorkflowDirectory ?? Directory.GetCurrentDirectory();
        return Path.GetFullPath(Path.Combine(baseDir, path!));
    }

    private static string? ResolveOutputPath(string? outputPath, WorkflowConfig config)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
            return null;
        if (Path.IsPathRooted(outputPath))
            return Path.GetFullPath(outputPath);

        var baseDir = config.WorkflowDirectory ?? Directory.GetCurrentDirectory();
        return Path.GetFullPath(Path.Combine(baseDir, outputPath));
    }

    private static string ResolveGuardDemoOutputRoot(string? outputRoot, WorkflowConfig config)
    {
        if (string.IsNullOrWhiteSpace(outputRoot))
            return Path.GetFullPath(config.WorkspaceRoot);
        if (Path.IsPathRooted(outputRoot))
            return Path.GetFullPath(outputRoot);

        var baseDir = config.WorkflowDirectory ?? Directory.GetCurrentDirectory();
        return Path.GetFullPath(Path.Combine(baseDir, outputRoot));
    }
}

public enum CommandOutputFormat
{
    Text,
    Json
}
