using System;
using System.Diagnostics;
using System.IO;
using DesktopAiTestAgent.UIAutomation;

namespace DesktopAiTestAgent.AgentRunner.Tests;

/// <summary>
/// Shared helpers for the gated, interactive desktop E2E tests: locate and launch
/// the built WinForms sample, and wait for it to be ready before driving it.
/// Kept separate so each scenario test stays focused on its scripted flow.
/// </summary>
internal static class DesktopE2E
{
    /// <summary>
    /// A buildable desktop sample target. WinForms and WPF expose the SAME automation
    /// ids and the same status strings (LoginForm.cs / MainWindow.xaml.cs) — only the
    /// window title and exe differ — so one scripted flow drives both.
    /// </summary>
    public sealed record SampleTarget(string Key, string WindowTitle, string ProjectDir, string ExeName, string TfmDir);

    public static readonly SampleTarget WinForms =
        new("winforms", "Sample Login App (.NET 8)", "Sample.WinFormsApp.Net8", "Sample.WinFormsApp.Net8.exe", "net8.0-windows");

    public static readonly SampleTarget Wpf =
        new("wpf", "WPF AI Test Target", "Sample.WpfApp", "Sample.WpfApp.exe", "net8.0-windows");

    public static readonly SampleTarget Avalonia =
        new("avalonia", "Avalonia AI Test Target", "Sample.AvaloniaApp", "Sample.AvaloniaApp.exe", "net8.0");

    /// <summary>Resolves a target by its theory key ("winforms" / "wpf" / "avalonia").</summary>
    public static SampleTarget Target(string key) => key switch
    {
        "winforms" => WinForms,
        "wpf" => Wpf,
        "avalonia" => Avalonia,
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown sample target.")
    };

    /// <summary>Starts the sample app exe (shell execute, so the window shows).</summary>
    public static Process LaunchSample(SampleTarget target)
    {
        var exePath = ResolveSampleExe(target);
        if (!File.Exists(exePath))
            throw new FileNotFoundException(
                $"Sample app not built at '{exePath}'. Build the solution first (dotnet build).", exePath);

        return Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true })
            ?? throw new InvalidOperationException("Failed to start the sample app process.");
    }

    /// <summary>
    /// Polls until the target window exposes the given control (or the timeout
    /// elapses), so the orchestrator's first observe sees a fully rendered form.
    /// Without this, the scripted mock (which advances on every decide call) would
    /// fire its first action before the control exists and never retry it.
    /// </summary>
    public static void WaitForControlReady(
        FlaUiDesktopDriver driver, string window, string automationId, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (driver.AttachToWindow(window, TimeSpan.FromSeconds(1)))
            {
                try
                {
                    var elements = driver.GetAllElements();
                    if (elements.Exists(e =>
                            string.Equals(e.AutomationId, automationId, StringComparison.OrdinalIgnoreCase)))
                        return;
                }
                catch { /* window still settling */ }
            }

            System.Threading.Thread.Sleep(250);
        }
    }

    /// <summary>
    /// Locates the built sample exe relative to the test assembly: walk up to the repo
    /// root (the folder containing the .sln), then into the sample's bin for the same
    /// build configuration (Debug/Release) the tests were built in.
    /// </summary>
    public static string ResolveSampleExe(SampleTarget target)
    {
        var baseDir = AppContext.BaseDirectory;
        var configuration = baseDir.Contains(
            $"{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
            ? "Release"
            : "Debug";

        var repoRoot = FindRepoRoot(baseDir)
            ?? throw new InvalidOperationException("Could not locate repo root (DesktopAiTestAgent.sln).");

        return Path.Combine(repoRoot,
            "src", "Samples", target.ProjectDir, "bin", configuration, target.TfmDir, target.ExeName);
    }

    /// <summary>Walks up from <paramref name="startDir"/> to the folder holding the .sln.</summary>
    public static string? FindRepoRoot(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "DesktopAiTestAgent.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
