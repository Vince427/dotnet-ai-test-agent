using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using DesktopAiTestAgent.AgentRunner.Mcp;

namespace DesktopAiTestAgent.AgentRunner.Tests;

/// <summary>
/// The MCP JSON-RPC dispatcher (pure): initialize/tools-list/tools-call over the read-only,
/// key-free tools, plus protocol error handling. No stdio, no spawned runs.
/// </summary>
public sealed class McpServerTests : IDisposable
{
    private readonly string _repo =
        Path.Combine(Path.GetTempPath(), "mcp-tests-" + Guid.NewGuid().ToString("N"));
    private readonly McpServer _server;

    public McpServerTests()
    {
        Directory.CreateDirectory(Path.Combine(_repo, "tests"));
        File.WriteAllText(Path.Combine(_repo, "tests", "smoke.yaml"),
            """
            suite: smoke
            tests:
              SMOKE-001:
                title: "A smoke test"
                framework: "winforms"
                goal: "Log in and confirm"
                success_condition: "Login successful"
                max_steps: 5
                allowed_actions: ["EnterText", "Click", "Done"]
            """);
        _server = new McpServer(_repo, Path.Combine(_repo, "runs"));
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_repo)) Directory.Delete(_repo, recursive: true); }
        catch { /* best-effort */ }
    }

    private static JsonElement Parse(string? json) => JsonDocument.Parse(json!).RootElement;

    [Fact]
    public void Initialize_ReturnsProtocolAndServerInfo()
    {
        var r = Parse(_server.HandleLine("""{"jsonrpc":"2.0","id":1,"method":"initialize"}"""));
        Assert.Equal(1, r.GetProperty("id").GetInt32());
        var result = r.GetProperty("result");
        Assert.False(string.IsNullOrEmpty(result.GetProperty("protocolVersion").GetString()));
        Assert.Equal("agentloop", result.GetProperty("serverInfo").GetProperty("name").GetString());
    }

    [Fact]
    public void Notification_WithoutId_ProducesNoResponse()
    {
        Assert.Null(_server.HandleLine("""{"jsonrpc":"2.0","method":"notifications/initialized"}"""));
    }

    [Fact]
    public void ToolsList_AdvertisesTheReadOnlyTools()
    {
        var r = Parse(_server.HandleLine("""{"jsonrpc":"2.0","id":2,"method":"tools/list"}"""));
        var names = r.GetProperty("result").GetProperty("tools").EnumerateArray()
            .Select(t => t.GetProperty("name").GetString()).ToList();
        Assert.Contains("list_tests", names);
        Assert.Contains("validate_plan", names);
        Assert.Contains("list_runs", names);
        Assert.Contains("get_run", names);
    }

    [Fact]
    public void ToolsCall_ListTests_ReturnsTheCatalogAsTextContent()
    {
        var r = Parse(_server.HandleLine("""{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"list_tests"}}"""));
        var result = r.GetProperty("result");
        Assert.False(result.GetProperty("isError").GetBoolean());
        // content[0].text is JSON we can parse back.
        var text = result.GetProperty("content")[0].GetProperty("text").GetString();
        var data = Parse(text);
        Assert.Equal(1, data.GetProperty("count").GetInt32());
        Assert.Equal("SMOKE-001", data.GetProperty("tests")[0].GetProperty("id").GetString());
    }

    [Fact]
    public void ToolsCall_ValidatePlan_ReportsValid()
    {
        var r = Parse(_server.HandleLine("""{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"validate_plan"}}"""));
        var text = r.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString();
        Assert.True(Parse(text).GetProperty("valid").GetBoolean());
    }

    [Fact]
    public void ToolsCall_GetRun_NotFound_IsToolError()
    {
        var r = Parse(_server.HandleLine("""{"jsonrpc":"2.0","id":5,"method":"tools/call","params":{"name":"get_run","arguments":{"runId":"deadbeef"}}}"""));
        Assert.True(r.GetProperty("result").GetProperty("isError").GetBoolean());
    }

    [Fact]
    public void ToolsCall_GetRun_RejectsUnsafeRunId()
    {
        var r = Parse(_server.HandleLine("""{"jsonrpc":"2.0","id":6,"method":"tools/call","params":{"name":"get_run","arguments":{"runId":"../secrets"}}}"""));
        Assert.True(r.GetProperty("result").GetProperty("isError").GetBoolean());
    }

    [Fact]
    public void UnknownMethod_IsJsonRpcMethodNotFound()
    {
        var r = Parse(_server.HandleLine("""{"jsonrpc":"2.0","id":7,"method":"does/notExist"}"""));
        Assert.Equal(-32601, r.GetProperty("error").GetProperty("code").GetInt32());
    }

    [Fact]
    public void UnknownTool_IsToolError()
    {
        var r = Parse(_server.HandleLine("""{"jsonrpc":"2.0","id":8,"method":"tools/call","params":{"name":"delete_everything"}}"""));
        Assert.True(r.GetProperty("result").GetProperty("isError").GetBoolean());
    }

    [Fact]
    public void Garbage_IsParseError()
    {
        var r = Parse(_server.HandleLine("not json"));
        Assert.Equal(-32700, r.GetProperty("error").GetProperty("code").GetInt32());
    }
}
