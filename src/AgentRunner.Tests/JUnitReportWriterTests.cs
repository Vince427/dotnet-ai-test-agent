using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using DesktopAiTestAgent.AgentRunner;

namespace DesktopAiTestAgent.AgentRunner.Tests;

public sealed class JUnitReportWriterTests
{
    private static RunArtifact Run(string result, string? testId = null, string? error = null)
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return new RunArtifact
        {
            RunId = "run1234",
            TestId = testId,
            Suite = "smoke",
            Framework = "winforms",
            Result = result,
            ErrorMessage = error,
            StartedAt = start,
            EndedAt = start.AddSeconds(5)
        };
    }

    private static XDocument Parse(string xml) => XDocument.Parse(xml);

    [Fact]
    public void WriteProducesWellFormedXmlWithDeclaration()
    {
        var xml = JUnitReportWriter.Write(new[] { Run("Succeeded") });

        Assert.StartsWith("<?xml", xml);
        var doc = Parse(xml);
        Assert.Equal("testsuites", doc.Root!.Name.LocalName);
    }

    [Fact]
    public void SucceededRunHasNoFailureOrError()
    {
        var xml = JUnitReportWriter.Write(new[] { Run("Succeeded") });
        var doc = Parse(xml);

        var testcase = doc.Descendants("testcase").Single();
        Assert.Empty(testcase.Elements("failure"));
        Assert.Empty(testcase.Elements("error"));
        Assert.Equal("0", doc.Root!.Attribute("failures")!.Value);
        Assert.Equal("0", doc.Root!.Attribute("errors")!.Value);
        Assert.Equal("1", doc.Root!.Attribute("tests")!.Value);
    }

    [Theory]
    [InlineData("Passed")]
    [InlineData("Succeeded")]
    public void PassingResultsHaveNoFailureOrError(string result)
    {
        // "Passed" (test runs) must be a pass, not an <error> — regression guard.
        var xml = JUnitReportWriter.Write(new[] { Run(result) });
        var doc = Parse(xml);
        var testcase = doc.Descendants("testcase").Single();
        Assert.Empty(testcase.Elements("failure"));
        Assert.Empty(testcase.Elements("error"));
        Assert.Equal("0", doc.Root!.Attribute("errors")!.Value);
    }

    [Fact]
    public void CrossLinkPropertiesAreEmitted()
    {
        var run = Run("Passed", testId: "LOGIN-001");
        run.ExistingTests = new List<string> { "MyApp.Tests.LoginTests.HappyPath" };
        run.SourceIssue = "ISSUE-42";
        run.SourcePr = "PR-7";
        run.TraceId = "0af7651916cd43dd8448eb211c80319c";

        var xml = JUnitReportWriter.Write(new[] { run });
        var props = Parse(xml).Descendants("testcase").Single()
            .Element("properties")!.Elements("property")
            .ToDictionary(p => (p.Attribute("name")!.Value, p.Attribute("value")!.Value));

        Assert.Contains(("existing_test", "MyApp.Tests.LoginTests.HappyPath"), props.Keys);
        Assert.Contains(("source_issue", "ISSUE-42"), props.Keys);
        Assert.Contains(("source_pr", "PR-7"), props.Keys);
        Assert.Contains(("trace_id", "0af7651916cd43dd8448eb211c80319c"), props.Keys);
    }

    [Theory]
    [InlineData("Failed")]
    [InlineData("Aborted")]
    [InlineData("LoopDetected")]
    public void FailingResultsProduceFailureElement(string result)
    {
        var xml = JUnitReportWriter.Write(new[] { Run(result) });
        var doc = Parse(xml);

        var testcase = doc.Descendants("testcase").Single();
        Assert.Single(testcase.Elements("failure"));
        Assert.Equal("1", doc.Root!.Attribute("failures")!.Value);
        Assert.Equal("0", doc.Root!.Attribute("errors")!.Value);
    }

    [Fact]
    public void RunningRunProducesErrorElement()
    {
        var xml = JUnitReportWriter.Write(new[] { Run("Running") });
        var doc = Parse(xml);

        var testcase = doc.Descendants("testcase").Single();
        Assert.Single(testcase.Elements("error"));
        Assert.Equal("1", doc.Root!.Attribute("errors")!.Value);
        Assert.Equal("0", doc.Root!.Attribute("failures")!.Value);
    }

    [Fact]
    public void TestCaseUsesTestIdWhenPresent()
    {
        var xml = JUnitReportWriter.Write(new[] { Run("Succeeded", testId: "LOGIN-001") });
        var doc = Parse(xml);

        Assert.Equal("LOGIN-001", doc.Descendants("testcase").Single().Attribute("name")!.Value);
    }

    [Fact]
    public void TestCaseNameFallsBackToRunId()
    {
        var xml = JUnitReportWriter.Write(new[] { Run("Succeeded", testId: null) });
        var doc = Parse(xml);

        Assert.Equal("run1234", doc.Descendants("testcase").Single().Attribute("name")!.Value);
    }

    [Fact]
    public void TimeReflectsDurationInSeconds()
    {
        var xml = JUnitReportWriter.Write(new[] { Run("Succeeded") });
        var doc = Parse(xml);

        var time = double.Parse(
            doc.Descendants("testcase").Single().Attribute("time")!.Value,
            CultureInfo.InvariantCulture);
        Assert.Equal(5.0, time, 3);
    }

    [Fact]
    public void FailureMessageUsesErrorMessage()
    {
        var xml = JUnitReportWriter.Write(new[] { Run("Failed", error: "target window not found") });
        var doc = Parse(xml);

        var failure = doc.Descendants("failure").Single();
        Assert.Equal("target window not found", failure.Attribute("message")!.Value);
    }

    [Fact]
    public void AggregateCountsAcrossMultipleRuns()
    {
        var xml = JUnitReportWriter.Write(new[]
        {
            Run("Succeeded"),
            Run("Failed"),
            Run("Aborted"),
            Run("Running")
        });
        var doc = Parse(xml);

        Assert.Equal("4", doc.Root!.Attribute("tests")!.Value);
        Assert.Equal("2", doc.Root!.Attribute("failures")!.Value);
        Assert.Equal("1", doc.Root!.Attribute("errors")!.Value);
        Assert.Equal(4, doc.Descendants("testcase").Count());
    }

    [Fact]
    public void EmptyRunsProduceEmptySuite()
    {
        var xml = JUnitReportWriter.Write(Array.Empty<RunArtifact>());
        var doc = Parse(xml);

        Assert.Equal("0", doc.Root!.Attribute("tests")!.Value);
        Assert.Empty(doc.Descendants("testcase"));
    }

    [Fact]
    public void XmlSpecialCharactersAreEscaped()
    {
        var xml = JUnitReportWriter.Write(new[] { Run("Failed", error: "expected <ok> & \"done\"") });

        // Must still parse (escaping is correct) and round-trip the message.
        var doc = Parse(xml);
        Assert.Equal("expected <ok> & \"done\"", doc.Descendants("failure").Single().Attribute("message")!.Value);
    }
}
