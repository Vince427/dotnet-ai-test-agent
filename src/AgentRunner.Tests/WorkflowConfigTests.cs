using DesktopAiTestAgent.AgentRunner;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner.Tests;

public sealed class WorkflowConfigTests
{
    [Fact]
    public void LoadParsesGoalsAndResolvesOpenRouterEnvironment()
    {
        var tempDir = CreateTempDirectory();
        var workflowPath = Path.Combine(tempDir, "WORKFLOW.md");
        File.WriteAllText(workflowPath, """
---
workspace:
  root: ./runs

goals:
  default:
    description: "Default login"
    success_condition: "Login successful"
    category: "Scenario"
    max_steps: 30
    identifier: "login"

  audit:
    description: "Audit controls"
    category: "Audit"
    max_steps: 12
    identifier: "a11y_audit"

llm:
  endpoint: $LLM_ENDPOINT
  api_key: $LLM_API_KEY
  model: $LLM_MODEL
---

# Policy

{{ goal.description }}

{% if goal.success_condition %}
Success Condition: UI must show "{{ goal.success_condition }}"
{% endif %}
""");

        using var env = new TemporaryEnvironment(
            ("LLM_ENDPOINT", "https://openrouter.ai/api/v1"),
            ("LLM_API_KEY", "test-openrouter-key"),
            ("LLM_MODEL", "anthropic/claude-3.5-sonnet:beta"));

        var config = WorkflowConfig.Load(workflowPath, loadDotEnv: false);

        Assert.Equal("https://openrouter.ai/api/v1", config.LlmEndpoint);
        Assert.Equal("test-openrouter-key", config.LlmApiKey);
        Assert.Equal("anthropic/claude-3.5-sonnet:beta", config.LlmModel);
        Assert.Equal(Path.GetFullPath(Path.Combine(tempDir, "runs")), config.WorkspaceRoot);
        Assert.Contains("{{ goal.description }}", config.PromptTemplate);

        var audit = config.GetGoal("audit");
        Assert.Equal("Audit controls", audit.Description);
        Assert.Equal(TestCategory.Audit, audit.Category);
        Assert.Equal(12, audit.MaxSteps);
        Assert.Equal("a11y_audit", audit.Identifier);

        var fallback = config.GetGoal("missing");
        Assert.Equal("Default login", fallback.Description);
        Assert.Equal(TestCategory.Scenario, fallback.Category);
    }

    [Fact]
    public void LoadFallsBackToLocalProxyWhenLlmEnvironmentIsMissing()
    {
        var tempDir = CreateTempDirectory();
        var workflowPath = Path.Combine(tempDir, "WORKFLOW.md");
        File.WriteAllText(workflowPath, """
---
llm:
  endpoint: $LLM_ENDPOINT
  api_key: $LLM_API_KEY
  model: $LLM_MODEL
---

# Policy
""");

        using var env = new TemporaryEnvironment(
            ("LLM_ENDPOINT", null),
            ("LLM_API_KEY", null),
            ("LLM_MODEL", null));

        var config = WorkflowConfig.Load(workflowPath, loadDotEnv: false);

        Assert.Equal(WorkflowConfig.DefaultLlmEndpoint, config.LlmEndpoint);
        Assert.Equal(WorkflowConfig.DefaultLlmApiKey, config.LlmApiKey);
        Assert.Equal(WorkflowConfig.DefaultLlmModel, config.LlmModel);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "desktop-ai-test-agent-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class TemporaryEnvironment : IDisposable
    {
        private readonly List<(string Key, string? Value)> _previousValues = [];

        public TemporaryEnvironment(params (string Key, string? Value)[] values)
        {
            foreach (var (key, value) in values)
            {
                _previousValues.Add((key, Environment.GetEnvironmentVariable(key)));
                Environment.SetEnvironmentVariable(key, value);
            }
        }

        public void Dispose()
        {
            foreach (var (key, value) in _previousValues)
                Environment.SetEnvironmentVariable(key, value);
        }
    }
}
