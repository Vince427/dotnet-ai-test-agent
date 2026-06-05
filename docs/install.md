# Install & run

The agent is a **Windows desktop** tool: it drives WinForms / WPF / MAUI / Avalonia apps from the
outside via UI Automation (FlaUI). So it ships as a **published Windows executable**, not as a
cross-platform `dotnet tool` (see "Why not `dotnet tool`" below).

## Option A — download a release build (recommended for users)

1. Get `AgentRunner.exe` (and its companion files) from a release `artifacts/release` zip.
2. Prerequisite: **Windows** + the **.NET 8 Desktop Runtime** (framework-dependent build). For a
   build with no prerequisite, use the self-contained option below.
3. Run it from a folder that contains a `tests/` directory:

   ```powershell
   .\AgentRunner.exe --list-tests
   .\AgentRunner.exe --validate-plan --format json
   .\AgentRunner.exe --dashboard 8090        # local-only dashboard
   .\AgentRunner.exe --plan tests\examples\winforms\login.yaml --test-id EX-WINFORMS-LOGIN-001
   ```

   The manual commands (`--list-tests`, `--validate-plan`, `--render-ui`, `--show-prompt`,
   `--compose-recording`, `--dashboard`, `--mcp`) work with **no `.env`**. Driving a real app at
   runtime needs the target app running plus a `.env` with your OpenRouter/OpenAI config.

## Option B — build the release yourself

```powershell
# Framework-dependent single-file exe (small; needs the .NET 8 Desktop Runtime):
powershell -ExecutionPolicy Bypass -File scripts/publish-release.ps1 -Zip

# Self-contained (bigger, no runtime prerequisite):
powershell -ExecutionPolicy Bypass -File scripts/publish-release.ps1 -SelfContained -Zip
```

Output lands in `artifacts/release/` (and `artifacts/release.zip` with `-Zip`).

## Option C — run from source (contributors)

```powershell
dotnet run --project src/AgentRunner/AgentRunner.csproj -f net8.0-windows -- --list-tests
```

## Why not `dotnet tool install -g`?

A .NET **global tool** must target a platform-neutral TFM (`net8.0`) and cannot use
`net8.0-windows` / `UseWPF` / `UseWindowsForms` (SDK error `NETSDK1146`). This agent depends on
FlaUI + WinForms/WPF for desktop automation, so it can't be packaged as a global tool today. A
published Windows exe is the correct distribution for a desktop tool. (If the manual/CLI surface is
ever split from the desktop driver into a `net8.0` core, a `dotnet tool` for the key-free commands
becomes possible — tracked in `.claude/DISCOVERY_LOG.md`.)
