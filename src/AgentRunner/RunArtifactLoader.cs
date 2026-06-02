using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DesktopAiTestAgent.AgentRunner;

/// <summary>
/// Loads <see cref="RunArtifact"/> records from a runs root (each run lives at
/// <c>runs/&lt;id&gt;/report.json</c>). Uses <see cref="JsonStringEnumConverter"/> to match
/// <c>ArtifactWriter</c>'s string-enum serialization — otherwise every real run fails to
/// deserialize and is silently dropped (see DISCOVERY_LOG 2026-06-01). Broken or partial
/// reports are skipped, never fatal.
/// </summary>
public static class RunArtifactLoader
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    public static IReadOnlyList<RunArtifact> LoadFromDirectory(string? runsRoot)
    {
        var runs = new List<RunArtifact>();
        if (string.IsNullOrWhiteSpace(runsRoot) || !Directory.Exists(runsRoot))
            return runs;

        foreach (var reportPath in Directory.GetFiles(runsRoot!, "report.json", SearchOption.AllDirectories))
        {
            try
            {
                var run = JsonSerializer.Deserialize<RunArtifact>(File.ReadAllText(reportPath), Options);
                if (run != null)
                    runs.Add(run);
            }
            catch
            {
                // Skip broken/partial reports; one bad run must not block the rest.
            }
        }

        return runs;
    }
}
