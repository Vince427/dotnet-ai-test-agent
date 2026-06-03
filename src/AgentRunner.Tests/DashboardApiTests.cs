using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using DesktopAiTestAgent.AgentRunner.Dashboard;

namespace DesktopAiTestAgent.AgentRunner.Tests;

/// <summary>
/// Deterministic coverage of the dashboard's HTTP-independent logic: catalog read,
/// ticket creation (form → validated YAML), and path-traversal safety on artifact
/// serving. No sockets, no spawned processes.
/// </summary>
public sealed class DashboardApiTests : IDisposable
{
    private readonly string _repo =
        Path.Combine(Path.GetTempPath(), "dash-tests-" + Guid.NewGuid().ToString("N"));

    private readonly DashboardApi _api;

    public DashboardApiTests()
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

        var runsRoot = Path.Combine(_repo, "runs");
        _api = new DashboardApi(_repo, runsRoot, new RunJobManager(_repo));
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_repo)) Directory.Delete(_repo, recursive: true); }
        catch { /* best-effort */ }
    }

    private static JsonElement ParseBody(ApiResponse r) =>
        JsonDocument.Parse(Encoding.UTF8.GetString(r.Body)).RootElement;

    [Fact]
    public void GetTests_ReturnsCatalogFromTestsDir()
    {
        var res = _api.GetTests();
        Assert.Equal(200, res.Status);

        var root = ParseBody(res);
        Assert.Equal(1, root.GetProperty("count").GetInt32());
        var first = root.GetProperty("tests")[0];
        Assert.Equal("SMOKE-001", first.GetProperty("id").GetString());
        Assert.Equal("smoke", first.GetProperty("suite").GetString());
        Assert.Equal("winforms", first.GetProperty("framework").GetString());
    }

    [Fact]
    public void CreateTest_WritesValidatedYaml()
    {
        var body = """
            {"id":"NEW-001","suite":"created","title":"New one","framework":"wpf",
             "goal":"Do a thing and confirm","maxSteps":6,
             "allowedActions":["Click","Done"],"tags":["dash"]}
            """;
        var res = _api.CreateTest(body);
        Assert.Equal(200, res.Status);

        var root = ParseBody(res);
        Assert.True(root.GetProperty("ok").GetBoolean());

        // File persisted under tests/created and is itself loadable + valid.
        var path = Path.Combine(_repo, "tests", "created", "NEW-001.yaml");
        Assert.True(File.Exists(path));
        var plan = TestPlanLoader.Load(path);
        Assert.Equal("NEW-001", plan.Tests[0].Id);
        Assert.True(TestPlanValidator.Validate(plan, path).IsValid);

        // And it now shows up in the catalog.
        Assert.Equal(2, ParseBody(_api.GetTests()).GetProperty("count").GetInt32());
    }

    [Fact]
    public void CreateTest_RejectsUnsafeId()
    {
        var res = _api.CreateTest("""{"id":"../evil","goal":"x"}""");
        Assert.Equal(400, res.Status);
        Assert.False(Directory.Exists(Path.Combine(_repo, "tests", "created")));
    }

    [Fact]
    public void CreateTest_RejectsMissingGoal()
    {
        var res = _api.CreateTest("""{"id":"NOGOAL-001"}""");
        Assert.Equal(400, res.Status);
    }

    [Fact]
    public void GetRun_NotFound_Returns404()
    {
        Assert.Equal(404, _api.GetRun("deadbeef").Status);
    }

    [Theory]
    [InlineData("..", "step_001.png")]
    [InlineData("good", "../../secret.png")]
    [InlineData("good", "step_001.txt")]
    public void GetScreenshot_RejectsTraversalAndNonPng(string runId, string file)
    {
        var res = _api.GetScreenshot(runId, file);
        Assert.True(res.Status is 400 or 404);
    }

    [Fact]
    public void LaunchRun_RejectsPathOutsideRepo()
    {
        var res = _api.LaunchRun("""{"planPath":"/etc/passwd","testId":"x"}""");
        Assert.Equal(400, res.Status);
    }

    [Fact]
    public void ResolveUnderRoot_AllowsUnderRoot_RejectsEscapesAndSiblingPrefix()
    {
        var root = Path.Combine(Path.GetTempPath(), "ru-" + Guid.NewGuid().ToString("N"));

        // Inside the root: allowed.
        Assert.NotNull(DashboardApi.ResolveUnderRoot(root, "tests/x.yaml"));
        Assert.NotNull(DashboardApi.ResolveUnderRoot(root, Path.Combine(root, "a", "b.yaml")));

        // Sibling-prefix directory (root + "-evil") must NOT pass containment.
        var sibling = root + "-evil" + Path.DirectorySeparatorChar + "x.yaml";
        Assert.Null(DashboardApi.ResolveUnderRoot(root, sibling));

        // Traversal and absolute-outside escape.
        Assert.Null(DashboardApi.ResolveUnderRoot(root, Path.Combine("..", "outside.yaml")));
        Assert.Null(DashboardApi.ResolveUnderRoot(root, @"C:\Windows\System32\drivers\etc\hosts"));
    }

    [Fact]
    public void GetFiles_ListsTestsTree()
    {
        var root = ParseBody(_api.GetFiles());
        Assert.True(root.GetProperty("count").GetInt32() >= 1);
        var paths = root.GetProperty("files").EnumerateArray()
            .Select(f => f.GetProperty("path").GetString()).ToList();
        Assert.Contains("tests/smoke.yaml", paths);
        var yaml = root.GetProperty("files").EnumerateArray()
            .First(f => f.GetProperty("path").GetString() == "tests/smoke.yaml");
        Assert.True(yaml.GetProperty("editable").GetBoolean());
    }

    [Fact]
    public void GetFile_ReturnsTextUnderRepo()
    {
        var res = _api.GetFile("tests/smoke.yaml");
        Assert.Equal(200, res.Status);
        Assert.Contains("SMOKE-001", Encoding.UTF8.GetString(res.Body));
    }

    [Fact]
    public void GetFile_ConfinedToAdvertisedRoots()
    {
        // A text file under the repo but OUTSIDE tests/ + runs/ must not be previewable,
        // matching what GetFiles advertises (tests/, runs/, WORKFLOW.md, .env.template).
        File.WriteAllText(Path.Combine(_repo, "Directory.Build.props"), "<Project/>");
        Assert.Equal(403, _api.GetFile("Directory.Build.props").Status);

        // The two named root files ARE allowed when present.
        File.WriteAllText(Path.Combine(_repo, "WORKFLOW.md"), "# workflow");
        Assert.Equal(200, _api.GetFile("WORKFLOW.md").Status);
    }

    [Theory]
    [InlineData("../../../etc/passwd")]   // traversal
    [InlineData(".env")]                   // secrets file
    [InlineData("secrets/.env")]           // secrets file in a subdir
    [InlineData("AgentRunner.dll")]        // non-text / not allow-listed
    public void GetFile_RejectsUnsafeOrDisallowed(string path)
    {
        var res = _api.GetFile(path);
        Assert.True(res.Status is 400 or 403 or 404 or 415);
    }

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("has space", "\"has space\"")]
    [InlineData("a\"b", "\"a\\\"b\"")]
    [InlineData("ends\\", "ends\\")]
    [InlineData("path with\\", "\"path with\\\\\"")]
    public void QuoteArg_QuotesPerWindowsRules(string input, string expected)
    {
        Assert.Equal(expected, RunJobManager.QuoteArg(input));
    }
}
