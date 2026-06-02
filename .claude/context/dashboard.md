# Context: Dashboard (OBS-2)

Owns the local-only, all-in-one developer dashboard: a thin HTTP server + single-page
UI that is a **view + launcher** over the existing CLI contract and run artifacts.

## Files

- `src/AgentRunner/Dashboard/DashboardServer.cs` (HttpListener routing, localhost-only)
- `src/AgentRunner/Dashboard/DashboardApi.cs` (transport-agnostic handlers: catalog,
  run history/detail, create-ticket, launch, screenshot serving)
- `src/AgentRunner/Dashboard/RunJobManager.cs` (spawns the CLI per launch, captures
  stdout, correlates `runId` from `session_id=` log markers)
- `src/AgentRunner/Dashboard/DashboardHtml.cs` (the single-page UI, vanilla JS)
- `src/AgentRunner.Tests/DashboardApiTests.cs`
- CLI entry: `--dashboard [port]` in `RunnerOptions` + `Program.RunDashboard`

## Invariants

- **Local-only dev tool. Never in CI, never exposed beyond `localhost`.** Bind only
  to `http://localhost:<port>`.
- It is a view/launcher over the CLI + artifacts — **no new data model**. Tests come
  from `tests/` (via `TestPlanLoader`), runs from `runs/` (via `RunArtifactLoader`),
  launching spawns the AgentRunner CLI (`--plan/--test-id`). Don't add a parallel store.
- Manual-first: `--dashboard` starts without `.env`/LLM. Launching a run from it spawns
  the CLI, which then needs the user's target app + provider config.
- Created "tickets" must be **validated YAML** (`TestPlanValidator`) before persisting,
  written under `tests/created/`. YAML stays the source of truth.
- Security: every filesystem-serving path must reject traversal — use
  `DashboardApi.IsSafeSegment` and confirm the resolved path stays under the runs/repo
  root. Screenshots are served from `runs/` only; logs/text are already redacted by
  `SecretRedactor` in the artifacts. Screenshot pixel-blur of secret fields is **not**
  implemented yet (deferred): localhost-only + OS-masked password fields are the current
  boundary; respect `EvidenceLevel` (Minimal = none).

## Validation

```powershell
dotnet test .\DesktopAiTestAgent.sln --no-restore -v minimal
dotnet run --project .\src\AgentRunner\AgentRunner.csproj -f net8.0-windows -- --dashboard 8090
# then GET http://localhost:8090/  (Ctrl+C to stop)
```

## Cross-Domain Notes

- Reuses the runner contract (CLI flags, artifact shape): changes there touch `runner.md`.
- Distinct from the **static** AgentLoop Workbench (`workbench.md`), which stays
  server-less for CI/offline. The dashboard is the interactive local counterpart.
- Trace link uses the OBS-1 `RunArtifact.TraceId`; optional deep-link via
  `AGENTLOOP_TRACE_UI_TEMPLATE` (`{traceId}` placeholder).
