using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DesktopAiTestAgent.AgentRunner;

/// <summary>
/// Extended agent memory with action history, persistent facts, and visited screen tracking.
/// </summary>
public class AgentMemory
{
    private readonly Queue<string> _history = [];
    private readonly Dictionary<string, string> _facts = [];
    private readonly HashSet<string> _visitedScreens = [];
    private const int MaxHistory = 15;

    public void AddAction(string actionDescription)
    {
        _history.Enqueue(actionDescription);
        while (_history.Count > MaxHistory)
            _history.Dequeue();
    }

    public void AddFact(string key, string value)
    {
        _facts[key] = value;
    }

    public void RecordScreen(string screenSignature)
    {
        _visitedScreens.Add(screenSignature);
    }

    public bool HasVisitedScreen(string screenSignature)
    {
        return _visitedScreens.Contains(screenSignature);
    }

    public int UniqueScreensVisited => _visitedScreens.Count;

    public string GetHistoryString()
    {
        if (!_history.Any())
            return "No previous actions.";

        return string.Join("\n", _history);
    }

    public string GetFactsString()
    {
        if (!_facts.Any())
            return "No known facts.";

        var sb = new StringBuilder();
        foreach (var kv in _facts)
            sb.AppendLine($"- {kv.Key}: {kv.Value}");
        return sb.ToString();
    }

    public string GetFullContextString()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Action History ===");
        sb.AppendLine(GetHistoryString());
        sb.AppendLine();
        sb.AppendLine("=== Known Facts ===");
        sb.AppendLine(GetFactsString());
        sb.AppendLine();
        sb.AppendLine($"=== Exploration Stats ===");
        sb.AppendLine($"Unique screens visited: {UniqueScreensVisited}");
        return sb.ToString();
    }

    public void Reset()
    {
        _history.Clear();
        _facts.Clear();
        _visitedScreens.Clear();
    }
}
