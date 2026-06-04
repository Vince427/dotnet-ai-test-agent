using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DesktopAiTestAgent.AgentRunner;

namespace DesktopAiTestAgent.AgentRunner.Tests;

/// <summary>
/// Verifies the human/agent bridge endpoint: it writes the prompt out and returns the
/// reply file as the assistant message, and falls back to a safe Wait on timeout.
/// </summary>
public sealed class BridgeLlmServerTests : IDisposable
{
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "bridge-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch { /* best-effort */ }
    }

    private static int FreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    private static async Task<string> PostAsync(string baseUrl, string body)
    {
        using var http = new HttpClient();
        var res = await http.PostAsync(baseUrl + "/chat/completions",
            new StringContent(body, Encoding.UTF8, "application/json"));
        return await res.Content.ReadAsStringAsync();
    }

    private static string AssistantContent(string completionJson)
    {
        using var doc = JsonDocument.Parse(completionJson);
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message")
            .GetProperty("content").GetString()!;
    }

    [Fact]
    public async Task ReturnsReplyFileAsAssistantContent_AndWritesPrompt()
    {
        using var bridge = new BridgeLlmServer(_dir, FreePort(), timeoutMs: 5000);
        bridge.Start();

        var action = "{\"actionType\":\"Click\",\"automationId\":\"btnLogin\"}";
        File.WriteAllText(Path.Combine(_dir, "resp-1.json"), action); // pre-answer the first request

        var completion = await PostAsync(bridge.BaseUrl,
            "{\"messages\":[{\"role\":\"user\",\"content\":\"the UI prompt for step 1\"}]}");

        Assert.Equal(action, AssistantContent(completion));
        var reqFile = Path.Combine(_dir, "req-1.txt");
        Assert.True(File.Exists(reqFile));
        Assert.Contains("the UI prompt for step 1", File.ReadAllText(reqFile));
    }

    [Fact]
    public async Task FallsBackToWait_OnTimeout()
    {
        using var bridge = new BridgeLlmServer(_dir, FreePort(), timeoutMs: 500);
        bridge.Start();

        // No resp file written -> the bridge times out and returns a safe Wait action.
        var completion = await PostAsync(bridge.BaseUrl,
            "{\"messages\":[{\"role\":\"user\",\"content\":\"prompt\"}]}");

        Assert.Contains("Wait", AssistantContent(completion));
    }
}
