using System;
using System.Collections.Generic;
using System.IO;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner;

/// <summary>
/// Reads and exposes typed configuration from WORKFLOW.md YAML front matter.
/// Follows Symphony's Config Layer pattern (Section 6).
/// </summary>
public class WorkflowConfig
{
    public const string DefaultLlmEndpoint = "http://localhost:4000";
    public const string DefaultLlmApiKey = "dummy-key";
    public const string DefaultLlmModel = "gpt-4o-mini";

    // Agent settings
    public int MaxConcurrentAgents { get; set; } = 1;
    public int MaxTurns { get; set; } = 30;
    public int MaxRetryBackoffMs { get; set; } = 10000;

    // Scoring
    public int AbortThreshold { get; set; } = -20;

    // Polling
    public int PollIntervalMs { get; set; } = 500;

    // Workspace
    public string WorkspaceRoot { get; set; } = "./runs";
    public string? WorkflowPath { get; set; }
    public string? WorkflowDirectory { get; set; }

    // LLM
    public string? LlmEndpoint { get; set; }
    public string? LlmApiKey { get; set; }
    public string? LlmModel { get; set; }

    // Predefined goals
    public Dictionary<string, AgentGoal> Goals { get; set; } = [];

    // Prompt template (body after YAML front matter)
    public string? PromptTemplate { get; set; }

    /// <summary>
    /// Loads config from a WORKFLOW.md file path.
    /// Simplified parser: reads YAML between --- markers, rest is prompt.
    /// </summary>
    public static WorkflowConfig Load(string? workflowPath = null, bool loadDotEnv = true, bool logConfig = true)
    {
        if (loadDotEnv)
            LoadDotEnv(logConfig);

        workflowPath ??= FindWorkflowFile();
        var cwd = Directory.GetCurrentDirectory();
        if (logConfig)
            Console.WriteLine($"[{DateTime.UtcNow:O}] [INFO] config_cwd=\"{cwd}\" config_path=\"{workflowPath}\"");
        if (workflowPath == null || !File.Exists(workflowPath))
        {
            if (logConfig)
                Console.WriteLine("[WorkflowConfig] No WORKFLOW.md found, using defaults.");
            return CreateDefaults();
        }

        var content = File.ReadAllText(workflowPath);
        var fullWorkflowPath = Path.GetFullPath(workflowPath);
        var config = new WorkflowConfig
        {
            WorkflowPath = fullWorkflowPath,
            WorkflowDirectory = Path.GetDirectoryName(fullWorkflowPath)
        };

        // Split front matter from prompt body
        if (content.StartsWith("---"))
        {
            var endIndex = content.IndexOf("---", 3);
            if (endIndex > 0)
            {
                var yaml = content[3..endIndex].Trim();
                config.PromptTemplate = content[(endIndex + 3)..].Trim();
                ParseSimpleYaml(yaml, config);
            }
        }
        else
        {
            config.PromptTemplate = content.Trim();
        }

        // Resolve $VAR environment variables
        var envEndpoint = Environment.GetEnvironmentVariable("LLM_ENDPOINT");
        var envApiKey = Environment.GetEnvironmentVariable("LLM_API_KEY");
        var envModel = Environment.GetEnvironmentVariable("LLM_MODEL");

        config.LlmEndpoint = ResolveEnvVar(config.LlmEndpoint) ?? envEndpoint ?? DefaultLlmEndpoint;
        config.LlmApiKey = ResolveEnvVar(config.LlmApiKey) ?? envApiKey ?? DefaultLlmApiKey;
        config.LlmModel = ResolveEnvVar(config.LlmModel) ?? envModel ?? DefaultLlmModel;
        config.ResolveWorkspaceRoot();

        return config;
    }

    /// <summary>
    /// Gets a goal by name, falling back to the default goal.
    /// </summary>
    public AgentGoal GetGoal(string? name = null)
    {
        if (!string.IsNullOrEmpty(name))
        {
            var key = name!;
            if (Goals.TryGetValue(key, out var namedGoal))
                return namedGoal;
        }

        if (Goals.TryGetValue("default", out var defaultGoal))
            return defaultGoal;

        return new AgentGoal();
    }

    private static WorkflowConfig CreateDefaults()
    {
        var config = new WorkflowConfig
        {
            LlmEndpoint = Environment.GetEnvironmentVariable("LLM_ENDPOINT") ?? DefaultLlmEndpoint,
            LlmApiKey = Environment.GetEnvironmentVariable("LLM_API_KEY") ?? DefaultLlmApiKey,
            LlmModel = Environment.GetEnvironmentVariable("LLM_MODEL") ?? DefaultLlmModel,
            WorkflowDirectory = Directory.GetCurrentDirectory()
        };
        config.ResolveWorkspaceRoot();

        config.Goals["default"] = new AgentGoal
        {
            Description = "Log in to the application using username 'admin' and password 'password123'.",
            SuccessCondition = "Login successful",
            MaxSteps = 30,
            Identifier = "login"
        };

        return config;
    }

    private static string? FindWorkflowFile()
    {
        // Look in current directory and parent directories
        var dir = Directory.GetCurrentDirectory();
        for (int i = 0; i < 5; i++)
        {
            var path = Path.Combine(dir, "WORKFLOW.md");
            if (File.Exists(path)) return path;
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }
        return null;
    }

    private static void LoadDotEnv(bool logConfig)
    {
        var startDirs = new[] { Directory.GetCurrentDirectory(), AppDomain.CurrentDomain.BaseDirectory };
        foreach (var startDir in startDirs)
        {
            var dir = startDir;
            for (int i = 0; i < 5; i++)
            {
                var path = Path.Combine(dir, ".env");
                if (File.Exists(path))
                {
                    if (logConfig)
                        Console.WriteLine($"[{DateTime.UtcNow:O}] [INFO] config_dotenv_found=\"{path}\"");
                    foreach (var line in File.ReadAllLines(path))
                    {
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
                        var parts = line.Split(['='], 2);
                        if (parts.Length != 2) continue;
                        var key = parts[0].Trim();
                        var val = parts[1].Trim();
                        Environment.SetEnvironmentVariable(key, val);
                        if (logConfig)
                            Console.WriteLine($"[{DateTime.UtcNow:O}] [INFO] config_env_set=\"{key}\"");
                    }
                    return;
                }
                var parent = Directory.GetParent(dir);
                if (parent == null) break;
                dir = parent.FullName;
            }
        }
    }

    private static string? ResolveEnvVar(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (value![0] == '$')
        {
            var varName = value[1..];
            var resolved = Environment.GetEnvironmentVariable(varName);
            return string.IsNullOrEmpty(resolved) ? null : resolved;
        }
        return value;
    }

    private void ResolveWorkspaceRoot()
    {
        if (Path.IsPathRooted(WorkspaceRoot))
        {
            WorkspaceRoot = Path.GetFullPath(WorkspaceRoot);
            return;
        }

        var baseDir = WorkflowDirectory ?? Directory.GetCurrentDirectory();
        WorkspaceRoot = Path.GetFullPath(Path.Combine(baseDir, WorkspaceRoot));
    }

    /// <summary>
    /// Very simple YAML key:value parser for the front matter.
    /// Handles nested objects at one level deep.
    /// </summary>
    private static void ParseSimpleYaml(string yaml, WorkflowConfig config)
    {
        var lines = yaml.Split('\n');
        string? currentSection = null;
        string? currentGoalName = null;
        AgentGoal? currentGoal = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                continue;

            var indent = line.Length - line.TrimStart().Length;
            var trimmed = line.Trim();

            if (indent == 0 && trimmed.EndsWith(":"))
            {
                // Top-level section
                currentSection = trimmed.TrimEnd(':').Trim();
                currentGoalName = null;
                currentGoal = null;
                continue;
            }

            if (indent == 2 && currentSection == "goals" && trimmed.EndsWith(":"))
            {
                // Goal name
                currentGoalName = trimmed.TrimEnd(':').Trim();
                currentGoal = new AgentGoal();
                config.Goals[currentGoalName] = currentGoal;
                continue;
            }

            var colonIdx = trimmed.IndexOf(':');
            if (colonIdx <= 0) continue;

            var key = trimmed[..colonIdx].Trim();
            var val = trimmed[(colonIdx + 1)..].Trim().Trim('"');

            if (currentGoal != null && indent >= 4)
            {
                switch (key)
                {
                    case "description": currentGoal.Description = val; break;
                    case "success_condition": currentGoal.SuccessCondition = val; break;
                    case "max_steps": if (int.TryParse(val, out var ms)) currentGoal.MaxSteps = ms; break;
                    case "identifier": currentGoal.Identifier = val; break;
                    case "max_retries": if (int.TryParse(val, out var mr)) currentGoal.MaxRetries = mr; break;
                    case "category": if (Enum.TryParse<TestCategory>(val, true, out var cat)) currentGoal.Category = cat; break;
                }
            }
            else
            {
                switch (currentSection)
                {
                    case "agent":
                        if (key == "max_concurrent_agents" && int.TryParse(val, out var mca)) config.MaxConcurrentAgents = mca;
                        if (key == "max_turns" && int.TryParse(val, out var mt)) config.MaxTurns = mt;
                        if (key == "max_retry_backoff_ms" && int.TryParse(val, out var mrb)) config.MaxRetryBackoffMs = mrb;
                        break;
                    case "scoring":
                        if (key == "abort_threshold" && int.TryParse(val, out var at)) config.AbortThreshold = at;
                        break;
                    case "polling":
                        if (key == "interval_ms" && int.TryParse(val, out var pi)) config.PollIntervalMs = pi;
                        break;
                    case "workspace":
                        if (key == "root") config.WorkspaceRoot = val;
                        break;
                    case "llm":
                        if (key == "endpoint") config.LlmEndpoint = val;
                        if (key == "api_key") config.LlmApiKey = val;
                        if (key == "model") config.LlmModel = val;
                        break;
                }
            }
        }
    }
}
