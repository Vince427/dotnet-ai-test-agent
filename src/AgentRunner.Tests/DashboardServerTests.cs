using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using DesktopAiTestAgent.AgentRunner.Dashboard;

namespace DesktopAiTestAgent.AgentRunner.Tests;

/// <summary>
/// HTTP-level checks for the dashboard server: the CSRF guard on POST routes (a localhost
/// dev tool must not be triggerable cross-origin from a random page the dev is visiting).
/// </summary>
public sealed class DashboardServerTests : IDisposable
{
    private readonly string _repo =
        Path.Combine(Path.GetTempPath(), "dashsrv-tests-" + Guid.NewGuid().ToString("N"));

    public DashboardServerTests() => Directory.CreateDirectory(_repo);

    public void Dispose()
    {
        try { if (Directory.Exists(_repo)) Directory.Delete(_repo, recursive: true); }
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

    private async Task<HttpStatusCode> PostAsync(string baseUrl, string? origin)
    {
        using var http = new HttpClient();
        var req = new HttpRequestMessage(HttpMethod.Post, baseUrl + "/api/runs")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        if (origin != null) req.Headers.Add("Origin", origin);
        var res = await http.SendAsync(req);
        return res.StatusCode;
    }

    [Fact]
    public async Task CrossOriginPost_IsBlocked()
    {
        var port = FreePort();
        using var server = new DashboardServer(_repo, Path.Combine(_repo, "runs"), port);
        server.Start();

        var status = await PostAsync(server.Url, origin: "http://evil.example");
        Assert.Equal(HttpStatusCode.Forbidden, status); // 403, before any body handling
    }

    [Fact]
    public async Task SameOriginPost_PassesTheGuard()
    {
        var port = FreePort();
        using var server = new DashboardServer(_repo, Path.Combine(_repo, "runs"), port);
        server.Start();

        // Same-origin POST is NOT blocked by the guard; it reaches LaunchRun which 400s on
        // the empty body. The point: it is not 403.
        var status = await PostAsync(server.Url, origin: server.Url);
        Assert.NotEqual(HttpStatusCode.Forbidden, status);
    }

    [Fact]
    public async Task NoOriginWithLocalhostHost_PassesTheGuard()
    {
        var port = FreePort();
        using var server = new DashboardServer(_repo, Path.Combine(_repo, "runs"), port);
        server.Start();

        // No Origin header (e.g. curl); HttpClient still sends Host: localhost:<port>.
        var status = await PostAsync(server.Url, origin: null);
        Assert.NotEqual(HttpStatusCode.Forbidden, status);
    }
}
