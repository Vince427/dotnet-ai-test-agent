# MVP Path

This is the smallest useful product path for Desktop AI Test Agent.

The MVP is not a dashboard and not a plugin. It is a portable contract that can
be used by a human locally, by CI, by the AgentLoop Workbench, or by AI agents:

```text
YAML specs -> CLI runner -> artifacts -> AgentLoop Workbench
```

All files and commands now use AgentLoop for the orchestrator and workbench.

## MVP User Flow

1. Write or update a YAML test in `tests/`.
2. Validate the YAML without `.env` or LLM access.
3. List tests and choose one test id.
4. Run a selected test against a launched desktop app when runtime access is
   available.
5. Inspect `summary.md`, `report.json`, screenshots, and the AgentLoop Workbench.
6. Publish a simple GitHub Pages view of the docs/workbench demo when sharing
   the project outside the local machine.

## MVP Commands

Validate plans:

```powershell
dotnet run --project .\src\AgentRunner\AgentRunner.csproj -f net8.0-windows -- --validate-plan --format json
```

List tests:

```powershell
dotnet run --project .\src\AgentRunner\AgentRunner.csproj -f net8.0-windows -- --list-tests --format json
```

Render the workbench:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\render-ui.ps1
```

Run one directed test:

```powershell
dotnet run --project .\src\AgentRunner\AgentRunner.csproj -f net8.0-windows -- --suite smoke --test-id LOGIN-001 --evidence-level standard
```

## MVP Boundaries

- OpenRouter is optional for runtime agent decisions and belongs in the real
  local `.env` or CI secrets.
- Validation, listing, workbench rendering, and artifact reading must not need
  `.env`.
- MCP servers and plugins should expose wrappers over these commands instead of
  adding hidden behavior.
- GitHub Copilot can use `.github/copilot-instructions.md`; Codex and other
  agents can use `AGENTS.md`; Claude Code can use `CLAUDE.md`.
- GitHub Pages is the first public documentation target. DocFX can come later
  when API reference generation is worth the extra setup.

## Next Useful Increments

- Add Avalonia sample parity.
- Split larger TestZoo backlogs into smaller YAML files, one business scenario
  per file.
- Add common TestZoo controls in WinForms/WPF first: radio, combo, list, grid,
  modal, disabled state.
- Add standard CI output format such as JUnit XML after the JSON path is stable.
