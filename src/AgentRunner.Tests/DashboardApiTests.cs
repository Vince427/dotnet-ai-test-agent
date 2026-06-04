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
    public void GetTests_ExposesCategoryAndArchivableEditableFlags()
    {
        var first = ParseBody(_api.GetTests()).GetProperty("tests")[0];
        Assert.Equal("Scenario", first.GetProperty("category").GetString()); // default category surfaced
        Assert.True(first.GetProperty("archivable").GetBoolean());           // single-test file
        Assert.False(first.GetProperty("editable").GetBoolean());            // not under tests/created
    }

    [Fact]
    public void CreatedTest_IsEditable()
    {
        _api.CreateTest("""{"id":"ED-001","goal":"do a thing and confirm","framework":"wpf","allowedActions":["Click","Done"]}""");

        var created = ParseBody(_api.GetTests()).GetProperty("tests").EnumerateArray()
            .First(t => t.GetProperty("id").GetString() == "ED-001");
        Assert.True(created.GetProperty("editable").GetBoolean());
    }

    [Fact]
    public void ArchiveTest_MovesYamlUnderArchivedAndOutOfTheCatalog()
    {
        var res = _api.ArchiveTest("""{"planPath":"tests/smoke.yaml"}""");
        Assert.Equal(200, res.Status);
        Assert.True(ParseBody(res).GetProperty("ok").GetBoolean());

        Assert.False(File.Exists(Path.Combine(_repo, "tests", "smoke.yaml")));
        Assert.True(File.Exists(Path.Combine(_repo, "tests", "archived", "smoke.yaml")));
        Assert.Equal(0, ParseBody(_api.GetTests()).GetProperty("count").GetInt32()); // excluded from discovery
    }

    [Fact]
    public void ArchiveTest_RejectsPathOutsideTests()
    {
        File.WriteAllText(Path.Combine(_repo, "WORKFLOW.md"), "# workflow");
        Assert.Equal(400, _api.ArchiveTest("""{"planPath":"WORKFLOW.md"}""").Status);
        Assert.True(File.Exists(Path.Combine(_repo, "WORKFLOW.md"))); // untouched
    }

    [Fact]
    public void SetConcurrency_RejectsZeroAndUpdatesValue()
    {
        Assert.Equal(400, _api.SetConcurrency("""{"max":0}""").Status);

        Assert.Equal(200, _api.SetConcurrency("""{"max":4}""").Status);
        Assert.Equal(4, _api.MaxConcurrency);
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

    [Fact]
    public void CreateTest_AlsoEmitsSymphonyTicket()
    {
        var res = _api.CreateTest("""
            {"id":"TKT-001","suite":"created","title":"Ticketed test","framework":"winforms",
             "targetWindow":"Sample Login App (.NET 8)","goal":"Log in and confirm","evidenceLevel":"full",
             "launchSample":true,"allowedActions":["Click","Done"]}
            """);
        Assert.Equal(200, res.Status);
        var root = ParseBody(res);
        Assert.Equal("tickets/created/TKT-001.md", root.GetProperty("ticketPath").GetString());

        // The ticket carries the flat frontmatter run-ticket-proof.ps1 needs.
        var md = File.ReadAllText(Path.Combine(_repo, "tickets", "created", "TKT-001.md"));
        Assert.Contains("plan: tests/created/TKT-001.yaml", md);
        Assert.Contains("test_id: TKT-001", md);
        Assert.Contains("framework: winforms", md);
        Assert.Contains("evidence_level: full", md);
        Assert.Contains("launch_sample: true", md);

        // And it shows up in the tickets listing.
        var tickets = ParseBody(_api.GetTickets());
        Assert.True(tickets.GetProperty("count").GetInt32() >= 1);
        Assert.Contains(tickets.GetProperty("tickets").EnumerateArray(),
            t => t.GetProperty("testId").GetString() == "TKT-001");
    }

    [Theory]
    [InlineData("../../etc/passwd")]      // outside repo
    [InlineData("tests/created/x.yaml")]  // wrong extension / not a ticket
    [InlineData("notes.md")]              // .md but not under tickets/
    public void GetTicket_RejectsUnsafeOrNonTicket(string path)
    {
        Assert.True(_api.GetTicket(path).Status is 400 or 404);
    }

    [Fact]
    public void RunTicket_RejectsPathOutsideTickets()
    {
        Assert.Equal(400, _api.RunTicket("""{"ticketPath":"tests/created/x.md"}""").Status);
    }

    [Fact]
    public void CreateTest_SanitizesFrontmatterAgainstInjection()
    {
        // A title with an embedded newline + a forged plan: line must NOT inject a second
        // frontmatter key — the newline is stripped, so only the real plan: line remains.
        var res = _api.CreateTest("""
            {"id":"INJ-001","goal":"g","framework":"winforms","title":"t\nplan: ../../evil.yaml"}
            """);
        Assert.Equal(200, res.Status);

        var md = File.ReadAllText(Path.Combine(_repo, "tickets", "created", "INJ-001.md"));
        var planLines = md.Split('\n')
            .Where(l => System.Text.RegularExpressions.Regex.IsMatch(l, @"^\s*plan\s*:")).ToArray();
        Assert.Single(planLines);                                   // no injected plan: line
        Assert.Contains("tests/created/INJ-001.yaml", planLines[0]); // and it's the real plan
    }
}
