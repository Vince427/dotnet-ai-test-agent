# Context: Dashboard (OBS-2)

Owns the local-only, all-in-one developer dashboard: a thin HTTP server + single-page
UI that is a **view + launcher** over the existing CLI contract and run artifacts.

## Files

- `src/AgentRunner/Dashboard/DashboardServer.cs` (HttpListener routing, localhost-only)
- `src/AgentRunner/Dashboard/DashboardApi.cs` (transport-agnostic handlers: catalog,
  run history/detail, create-ticket, launch, screenshot serving, file tree + preview,
  prompt preview)
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
- Launches go through `RunJobManager`'s **bounded queue**: at most `MaxConcurrency` (default 2,
  clamped [1,16], set via `POST /api/jobs/concurrency`, exposed in `GET /api/config`) run at
  once; the rest sit in `queued` status. Batch run = N enqueues (the UI loops `POST /api/runs`),
  so it still reduces to the same CLI contract — nothing CI can't replay. The OS-process start
  is an overridable seam (`BeginProcess`) so the scheduler is unit-tested without spawning.
- **Mutation is bounded to what maps to a YAML file** (the dashboard never becomes a 2nd source
  of truth): Create writes validated YAML + ticket; Edit re-writes a **single-test file under
  `tests/created/`** through the same validator (catalog `editable` flag); Archive **moves** a
  single-test YAML to `tests/archived/` (catalog `archivable` flag), which `DiscoverPlanPaths`
  excludes everywhere (catalog + CLI + CI) — reversible, shows in Git. **No hard delete.**
  Archive is symmetric: `GET /api/archived` lists archived tests and `POST /api/tests/unarchive`
  (Restore) moves the YAML back — both are just file moves. Multi-test files stay "edit on disk".
  The catalog also surfaces `category` for filtering.
- Manual-first: `--dashboard` starts without `.env`/LLM. Launching a run from it spawns
  the CLI, which then needs the user's target app + provider config.
- **V7 inc.2 (prompt preview + warnings)**: the dashboard mirrors the V7 CLI signals, key-free.
  `GET /api/prompt?planPath=&testId=` returns `{ testId, planPath, prompt }` from `PromptPreview`
  (reuses `PromptBuilder` → can't drift from the runtime prompt; `SecretRedactor` applied;
  path-guarded via `ResolveUnderRepo`) — the dashboard surface for `--show-prompt` / MCP
  `show_prompt`. Each `/api/tests` entry also carries a `warnings: string[]` (the non-fatal
  `TestPlanValidator` advisories for that test, location prefix stripped), and `CreateTest`
  echoes `warnings` in its OK response. These are advisory only — the plan stays valid.
- Created "tickets" must be **validated YAML** (`TestPlanValidator`) before persisting,
  written under `tests/created/`. YAML stays the source of truth. `CreateTest` accepts a
  `category` field (whitelisted to the `TestCategory` taxonomy in `BuildYaml`, default
  `Scenario`) and `risk`; the Create form is a guided, fully-explained surface over these.
- The **Tickets** tab + Create are the AgentLoop bridge: `CreateTest` writes a YAML test
  **and** a `tickets/created/<id>.md` ticket (flat `key: value` frontmatter compatible with
  `scripts/run-ticket-proof.ps1`); `GetTickets`/`GetTicket` list/view them; `RunTicket`
  spawns `run-ticket-proof.ps1` via `RunJobManager.LaunchTicket` — the SAME adapter CI uses,
  so a dashboard-authored ticket runs unchanged in CI. Orchestration stays in the script,
  not C# core (product rule: AgentLoop/hooks are adapters over the CLI).
- The **Files** tab is read-only and reinforces "edit on disk": it lists the tree under
  `tests/` + `runs/` (+ `WORKFLOW.md`, `.env.template`) and previews text/config files.
  `GetFile` MUST stay locked down: `ResolveUnderRoot` containment, an extension
  allow-list (`TextExts`), a size cap, and an explicit refusal of real secrets files
  (anything named `.env*` except `.env.template`). Never add executable/binary serving.
- Security: every filesystem-serving path must reject traversal — use
  `DashboardApi.IsSafeSegment` / `ResolveUnderRoot` (trailing-separator containment) and
  confirm the resolved path stays under the runs/repo root. Screenshots are served from `runs/` only; logs/text are
  redacted by `SecretRedactor`, and secret-field regions are **masked at capture** (V3-A)
  so the on-disk PNGs are already redacted. Still respect `EvidenceLevel` (Minimal = none).

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
