using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DesktopAiTestAgent.AgentRunner.Dashboard;

/// <summary>
/// Local-only all-in-one dashboard (OBS-2): a thin HTTP server, bound to
/// <c>localhost</c>, that serves a single-page UI and a small JSON API over the
/// existing CLI contract and run artifacts. It is a developer tool — never run it in
/// CI, never expose it beyond loopback.
///
/// Built on <see cref="HttpListener"/> so it adds no dependency. All read/create/launch
/// logic lives in <see cref="DashboardApi"/>; this class only adapts HTTP to it.
/// </summary>
public sealed class DashboardServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly DashboardApi _api;
    private readonly RunJobManager _jobs;
    private readonly string _traceUiTemplate;
    private readonly CancellationTokenSource _cts = new();

    public string Url { get; }

    public DashboardServer(string repoRoot, string runsRoot, int port)
    {
        _jobs = new RunJobManager(repoRoot);
        _api = new DashboardApi(repoRoot, runsRoot, _jobs);
        // Optional deep-link template, e.g. "http://localhost:18888/traces/{traceId}".
        _traceUiTemplate = Environment.GetEnvironmentVariable("AGENTLOOP_TRACE_UI_TEMPLATE") ?? "";
        Url = $"http://localhost:{port}";
        _listener.Prefixes.Add($"{Url}/");
    }

    public void Start()
    {
        _listener.Start();
        _ = Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    /// <summary>Blocks until Ctrl+C, then stops the listener.</summary>
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
            _ = Task.Run(() => Handle(context));
        }
    }

    private void Handle(HttpListenerContext context)
    {
        ApiResponse response;
        try
        {
            response = Route(context.Request);
        }
        catch (Exception ex)
        {
            response = ApiResponse.Error(500, "Internal error: " + ex.Message);
        }

        try
        {
            context.Response.StatusCode = response.Status;
            context.Response.ContentType = response.ContentType;
            context.Response.ContentLength64 = response.Body.Length;
            context.Response.OutputStream.Write(response.Body, 0, response.Body.Length);
        }
        catch { /* client went away */ }
        finally { try { context.Response.OutputStream.Close(); } catch { } }
    }

    private ApiResponse Route(HttpListenerRequest request)
    {
        var path = request.Url?.AbsolutePath ?? "/";
        var method = request.HttpMethod;

        if (method == "GET")
        {
            switch (path)
            {
                case "/":
                case "/index.html":
                    return ApiResponse.Text(DashboardHtml.Page, 200, "text/html; charset=utf-8");
                case "/api/config":
                    return ApiResponse.Json(new { traceUiTemplate = _traceUiTemplate });
                case "/api/tests":
                    return _api.GetTests();
                case "/api/runs":
                    return _api.GetRuns();
                case "/api/jobs":
                    return _api.GetJobs();
                case "/api/files":
                    return _api.GetFiles();
                case "/api/file":
                    return _api.GetFile(request.QueryString["path"] ?? "");
                case "/api/tickets":
                    return _api.GetTickets();
                case "/api/ticket":
                    return _api.GetTicket(request.QueryString["path"] ?? "");
                case "/api/screenshot":
                    return _api.GetScreenshot(
                        request.QueryString["run"] ?? "", request.QueryString["file"] ?? "");
            }

            // /api/runs/{id} and /api/runs/{id}/screenshots
            if (path.StartsWith("/api/runs/", StringComparison.Ordinal))
            {
                var rest = path["/api/runs/".Length..];
                if (rest.EndsWith("/screenshots", StringComparison.Ordinal))
                    return _api.GetScreenshotList(rest[..^"/screenshots".Length]);
                return _api.GetRun(rest);
            }

            if (path.StartsWith("/api/jobs/", StringComparison.Ordinal))
                return _api.GetJob(path["/api/jobs/".Length..]);

            return ApiResponse.Error(404, "Not found.");
        }

        if (method == "POST")
        {
            // CSRF guard: POSTs spawn processes / write files, so only accept them from the
            // dashboard's own page. A cross-origin page (or a forged Origin) is rejected.
            if (!IsSameOriginPost(request))
                return ApiResponse.Error(403, "Cross-origin POST blocked (localhost dev tool).");

            var body = ReadBody(request);
            return path switch
            {
                "/api/tests" => _api.CreateTest(body),
                "/api/runs" => _api.LaunchRun(body),
                "/api/tickets/run" => _api.RunTicket(body),
                _ => ApiResponse.Error(404, "Not found.")
            };
        }

        return ApiResponse.Error(405, "Method not allowed.");
    }

    /// <summary>
    /// True if a POST is same-origin: the browser-sent Origin must equal our URL, or (for
    /// non-browser clients like curl that send no Origin) the Host must be our loopback
    /// authority. Browsers attach Origin to cross-origin POSTs, so a malicious page is rejected.
    /// </summary>
    private bool IsSameOriginPost(HttpListenerRequest request)
    {
        var origin = request.Headers["Origin"];
        if (!string.IsNullOrEmpty(origin))
            return string.Equals(origin.TrimEnd('/'), Url, StringComparison.OrdinalIgnoreCase);

        var host = request.Headers["Host"];
        return !string.IsNullOrEmpty(host) &&
               string.Equals(host, new Uri(Url).Authority, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadBody(HttpListenerRequest request)
    {
        using var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8);
        return reader.ReadToEnd();
    }

    public void Dispose()
    {
        try { _cts.Cancel(); } catch { }
        try { if (_listener.IsListening) _listener.Stop(); } catch { }
        try { _listener.Close(); } catch { }
        _jobs.Dispose();
        _cts.Dispose();
    }
}
