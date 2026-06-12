using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DesktopAiTestAgent.AgentRunner;

/// <summary>
/// A "human/agent in the loop" OpenAI-compatible endpoint: a bridge that lets a person —
/// or an external agent such as Claude Code — be the decider with no provider key. It is
/// drop-in for the real <see cref="LlmService"/>: point <c>LLM_ENDPOINT</c> at it.
///
/// On each chat-completions request it writes the agent's prompt to
/// <c>&lt;ioDir&gt;/req-&lt;n&gt;.txt</c> and then waits for a reply file
/// <c>&lt;ioDir&gt;/resp-&lt;n&gt;.json</c> (or <c>.txt</c>) containing the next action JSON
/// (the same shape <see cref="LlmResponseParser"/> consumes). When the reply appears it is
/// returned as the assistant message; on timeout a safe <c>Wait</c> action is returned so a
/// run never hangs forever.
/// </summary>
public sealed class BridgeLlmServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly string _ioDir;
    private readonly int _timeoutMs;
    private readonly CancellationTokenSource _cts = new();
    private readonly object _gate = new();
    private int _count;

    public string BaseUrl { get; }
    public string IoDir => _ioDir;

    public BridgeLlmServer(string ioDir, int port, int timeoutMs = 120_000)
    {
        _ioDir = Path.GetFullPath(ioDir);
        Directory.CreateDirectory(_ioDir);
        _timeoutMs = timeoutMs;
        BaseUrl = $"http://localhost:{port}";
        _listener.Prefixes.Add($"{BaseUrl}/");
    }

    public void Start()
    {
        _listener.Start();
        _ = Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    public void WaitForShutdown()
    {
        using var stop = new ManualResetEventSlim(false);
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Set(); };
        stop.Wait();
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener.IsListening)
        {
            HttpListenerContext context;
            try { context = await _listener.GetContextAsync(); }
            catch { break; }
            _ = Task.Run(() => Handle(context), ct);
        }
    }

    private void Handle(HttpListenerContext context)
    {
        try
        {
            // Only POSTs to the chat-completions path are agent decisions; answer anything
            // else (health probes, favicon, GET /) cheaply so they don't consume a step.
            var path = context.Request.Url?.AbsolutePath ?? "/";
            if (context.Request.HttpMethod != "POST" || !path.Contains("chat/completions"))
            {
                var ok = Encoding.UTF8.GetBytes("AgentLoop bridge LLM. POST /v1/chat/completions.");
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain; charset=utf-8";
                context.Response.OutputStream.Write(ok, 0, ok.Length);
                context.Response.OutputStream.Close();
                return;
            }

            string body;
            using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding ?? Encoding.UTF8))
                body = reader.ReadToEnd();

            int n;
            lock (_gate) { n = ++_count; }

            var reqPath = Path.Combine(_ioDir, $"req-{n}.txt");
            File.WriteAllText(reqPath, ExtractPrompt(body));
            Console.WriteLine($"[bridge] step {n}: wrote {reqPath} — awaiting resp-{n}.json …");

            var content = WaitForReply(n) ?? "{\"actionType\":\"Wait\",\"reason\":\"bridge timeout\"}";
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
        finally { try { context.Response.OutputStream.Close(); } catch { } }
    }

    private string? WaitForReply(int n)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var json = Path.Combine(_ioDir, $"resp-{n}.json");
        var txt = Path.Combine(_ioDir, $"resp-{n}.txt");
        while (sw.ElapsedMilliseconds < _timeoutMs && !_cts.IsCancellationRequested)
        {
            foreach (var path in new[] { json, txt })
            {
                if (!File.Exists(path)) continue;
                try
                {
                    var content = File.ReadAllText(path).Trim();
                    if (content.Length > 0) return content;
                }
                catch { /* still being written; retry */ }
            }
            Thread.Sleep(250);
        }
        return null;
    }

    /// <summary>Pull the last message's content out of the OpenAI request for readability.</summary>
    private static string ExtractPrompt(string requestBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(requestBody);
            if (doc.RootElement.TryGetProperty("messages", out var messages) &&
                messages.ValueKind == JsonValueKind.Array && messages.GetArrayLength() > 0)
            {
                var last = messages[messages.GetArrayLength() - 1];
                if (last.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
                    return content.GetString() ?? requestBody;
            }
        }
        catch { /* fall through to raw body */ }
        return requestBody;
    }

    private static string BuildChatCompletionJson(string assistantContent)
    {
        var response = new
        {
            id = "chatcmpl-bridge",
            @object = "chat.completion",
            created = 1_700_000_000,
            model = "bridge",
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
