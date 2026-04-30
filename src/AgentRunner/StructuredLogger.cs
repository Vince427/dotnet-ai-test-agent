using System;

namespace DesktopAiTestAgent.AgentRunner;

/// <summary>
/// Structured logger following Symphony's key=value logging conventions.
/// Logs to console with structured context fields.
/// </summary>
public class StructuredLogger(string? issueId = null, string? sessionId = null)
{
    private string? _issueId = issueId;
    private string? _sessionId = sessionId;

    public void SetContext(string? issueId, string? sessionId)
    {
        _issueId = issueId;
        _sessionId = sessionId;
    }

    public void Info(string message, string? detail = null)
    {
        Log("INFO", message, detail);
    }

    public void Action(string actionType, string target, string? outcome = null)
    {
        var msg = $"action={actionType} target={target}";
        if (!string.IsNullOrEmpty(outcome))
            msg += $" outcome={outcome}";
        Log("ACTION", msg);
    }

    public void Decision(string reasoning)
    {
        Log("DECISION", reasoning);
    }

    public void Score(string summary)
    {
        Log("SCORE", summary);
    }

    public void Error(string message, Exception? ex = null)
    {
        var detail = ex?.Message;
        Log("ERROR", message, detail);
    }

    public void Warning(string message)
    {
        Log("WARN", message);
    }

    private void Log(string level, string message, string? detail = null)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var ctx = "";
        if (!string.IsNullOrEmpty(_issueId))
            ctx += $" issue_id={_issueId}";
        if (!string.IsNullOrEmpty(_sessionId))
            ctx += $" session_id={_sessionId}";

        var line = $"[{timestamp}] [{level}]{ctx} {message}";
        if (!string.IsNullOrEmpty(detail))
            line += $" detail={detail}";

        Console.WriteLine(line);
    }
}
