using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace DesktopAiTestAgent.AgentRunner;

/// <summary>
/// Converts AgentLoop run artifacts into JUnit XML, the de-facto CI test report
/// format understood by GitHub Actions, Azure Pipelines, Jenkins, and others.
///
/// Deterministic and key-free: it only transforms already-captured run data, so it
/// is fully unit-testable without an LLM, FlaUI, or a target app. Result mapping:
///   - "Succeeded"                          -> passing testcase
///   - "Failed" / "Aborted" / "LoopDetected" -> testcase with &lt;failure&gt;
///   - anything else (e.g. "Running")        -> testcase with &lt;error&gt; (incomplete)
/// </summary>
public static class JUnitReportWriter
{
    private static readonly string[] FailureResults = { "Failed", "Aborted", "LoopDetected" };
    private static readonly string[] PassResults = { "Passed", "Succeeded" };

    /// <summary>
    /// Renders the given runs as a JUnit XML document string (with declaration).
    /// </summary>
    public static string Write(IEnumerable<RunArtifact> runs, string suiteName = "DesktopAiTestAgent")
    {
        var runList = runs?.ToList() ?? new List<RunArtifact>();

        var failures = runList.Count(IsFailure);
        var errors = runList.Count(IsError);
        var totalTime = runList.Sum(DurationSeconds);

        var testcases = runList.Select(ToTestCase).ToArray();

        var testsuite = new XElement("testsuite",
            new XAttribute("name", suiteName),
            new XAttribute("tests", runList.Count),
            new XAttribute("failures", failures),
            new XAttribute("errors", errors),
            new XAttribute("time", FormatTime(totalTime)),
            testcases);

        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("testsuites",
                new XAttribute("tests", runList.Count),
                new XAttribute("failures", failures),
                new XAttribute("errors", errors),
                new XAttribute("time", FormatTime(totalTime)),
                testsuite));

        using var writer = new Utf8StringWriter();
        doc.Save(writer);
        return writer.ToString();
    }

    private static XElement ToTestCase(RunArtifact run)
    {
        var testcase = new XElement("testcase",
            new XAttribute("name", run.TestId ?? run.GoalIdentifier ?? run.RunId),
            new XAttribute("classname", run.Suite ?? run.Framework ?? "AgentLoop"),
            new XAttribute("time", FormatTime(DurationSeconds(run))));

        var properties = BuildProperties(run);
        if (properties != null)
            testcase.Add(properties);

        if (IsFailure(run))
        {
            testcase.Add(new XElement("failure",
                new XAttribute("message", FailureMessage(run)),
                BuildDetail(run)));
        }
        else if (IsError(run))
        {
            testcase.Add(new XElement("error",
                new XAttribute("message", run.ErrorMessage ?? $"Run did not complete (result: {run.Result})"),
                BuildDetail(run)));
        }

        return testcase;
    }

    /// <summary>
    /// Cross-link properties on the testcase (V4-A): existing TRX/JUnit testcase links
    /// and the source issue/PR. Returns null when there is nothing to emit.
    /// </summary>
    private static XElement? BuildProperties(RunArtifact run)
    {
        var props = new List<XElement>();
        foreach (var existing in run.ExistingTests)
        {
            if (!string.IsNullOrWhiteSpace(existing))
                props.Add(Property("existing_test", existing));
        }
        if (!string.IsNullOrWhiteSpace(run.SourceIssue))
            props.Add(Property("source_issue", run.SourceIssue!));
        if (!string.IsNullOrWhiteSpace(run.SourcePr))
            props.Add(Property("source_pr", run.SourcePr!));
        if (!string.IsNullOrWhiteSpace(run.TraceId))
            props.Add(Property("trace_id", run.TraceId!));

        return props.Count == 0 ? null : new XElement("properties", props);
    }

    private static XElement Property(string name, string value) =>
        new("property", new XAttribute("name", name), new XAttribute("value", value));

    private static string FailureMessage(RunArtifact run)
    {
        if (!string.IsNullOrWhiteSpace(run.ErrorMessage))
            return run.ErrorMessage!;

        var lastFailed = run.Steps.LastOrDefault(s =>
            !string.IsNullOrWhiteSpace(s.FailureMessage) || !string.IsNullOrWhiteSpace(s.FailureCode));
        if (lastFailed != null)
            return lastFailed.FailureMessage ?? lastFailed.FailureCode ?? run.Result;

        return run.Result;
    }

    private static string BuildDetail(RunArtifact run)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Result: {run.Result}");
        sb.AppendLine($"Final score: {run.FinalScore}");
        sb.AppendLine($"Steps: {run.Steps.Count}");
        if (!string.IsNullOrWhiteSpace(run.GoalDescription))
            sb.AppendLine($"Goal: {run.GoalDescription}");
        return sb.ToString();
    }

    private static bool IsFailure(RunArtifact run) =>
        FailureResults.Contains(run.Result, StringComparer.OrdinalIgnoreCase);

    private static bool IsPass(RunArtifact run) =>
        PassResults.Contains(run.Result, StringComparer.OrdinalIgnoreCase);

    // Anything that is neither a pass nor a known failure (e.g. "Running", "Blocked")
    // is an incomplete run -> <error>.
    private static bool IsError(RunArtifact run) => !IsFailure(run) && !IsPass(run);

    private static double DurationSeconds(RunArtifact run)
    {
        var end = run.EndedAt ?? run.StartedAt;
        var seconds = (end - run.StartedAt).TotalSeconds;
        return seconds < 0 ? 0 : seconds;
    }

    private static string FormatTime(double seconds) =>
        seconds.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>StringWriter that reports UTF-8 so the XML declaration says utf-8.</summary>
    private sealed class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }
}
