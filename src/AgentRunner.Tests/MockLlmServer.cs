using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DesktopAiTestAgent.AgentRunner.Tests;

/// <summary>
/// A deterministic, key-free, OpenAI-compatible chat-completions server for tests.
///
/// It ignores the request body and returns a scripted sequence of assistant
/// messages — each one a raw agent-action JSON string (the same shape
/// <see cref="LlmResponseParser"/> consumes). Point a <see cref="WorkflowConfig"/>'s
/// <c>LlmEndpoint</c> at <see cref="BaseUrl"/> and the real <see cref="LlmService"/>
/// will drive through it with no API key and no network egress.
///
/// Once the scripted responses are exhausted it repeats the last one, so a loop
/// that runs one extra step (e.g. detecting success on observe before consuming
/// the final <c>Done</c>) stays well-defined.
/// </summary>
public sealed class MockLlmServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly string[] _scriptedContents;
    private readonly CancellationTokenSource _cts = new();
    private readonly object _gate = new();
    private int _index = -1;

    /// <summary>Base URL (no trailing slash), e.g. <c>http://localhost:50713</c>.</summary>
    public string BaseUrl { get; }

    /// <summary>Number of completion requests served so far.</summary>
    public int RequestCount { get; private set; }

    /// <param name="scriptedActionJson">
    /// Assistant message contents, returned in order. Each should be a JSON object
    /// like <c>{"actionType":"Click","automationId":"btnLogin","confidence":95}</c>.
    /// </param>
    public MockLlmServer(params string[] scriptedActionJson)
    {
        _scriptedContents = scriptedActionJson.Length > 0
            ? scriptedActionJson
            : ["{\"actionType\":\"Wait\"}"];

        var port = GetFreeTcpPort();
        BaseUrl = $"http://localhost:{port}";
        _listener.Prefixes.Add($"{BaseUrl}/");
        _listener.Start();
        _ = Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    private static int GetFreeTcpPort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch
            {
                break; // listener stopped/disposed
            }

            _ = Task.Run(() => HandleRequest(context));
        }
    }

    private void HandleRequest(HttpListenerContext context)
    {
        try
        {
            // Drain the request body; we intentionally ignore its content.
            using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
                _ = reader.ReadToEnd();

            string content;
            lock (_gate)
            {
                RequestCount++;
                if (_index < _scriptedContents.Length - 1)
                    _index++;
                content = _scriptedContents[_index];
            }

            var payload = BuildChatCompletionJson(content);
            var bytes = Encoding.UTF8.GetBytes(payload);

            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = bytes.Length;
            context.Response.OutputStream.Write(bytes, 0, bytes.Length);
        }
        catch
        {
            try { context.Response.StatusCode = 500; } catch { }
        }
        finally
        {
            try { context.Response.OutputStream.Close(); } catch { }
        }
    }

    private static string BuildChatCompletionJson(string assistantContent)
    {
        // Minimal-but-complete OpenAI chat.completion response. JsonSerializer
        // handles escaping the embedded action JSON inside the "content" string.
        var response = new
        {
            id = "chatcmpl-mock",
            @object = "chat.completion",
            created = 1_700_000_000,
            model = "mock-model",
            choices = new[]
            {
                new
                {
                    index = 0,
                    message = new { role = "assistant", content = assistantContent },
                    finish_reason = "stop"
                }
            },
            usage = new { prompt_tokens = 0, completion_tokens = 0, total_tokens = 0 }
        };

        return JsonSerializer.Serialize(response);
    }

    public void Dispose()
    {
        try { _cts.Cancel(); } catch { }
        try { if (_listener.IsListening) _listener.Stop(); } catch { }
        try { _listener.Close(); } catch { }
        _cts.Dispose();
    }
}
