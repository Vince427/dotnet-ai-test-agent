# Desktop AI Test Agent - agent orientation

Portable-first desktop test runner for existing .NET applications.

This repo is built for parallel agent work. Keep each session scoped to one
domain, update that domain contract when the public API changes, and write
cross-domain surprises to `.claude/DISCOVERY_LOG.md`.

## Required Reading

For every task:

1. Read `project_rules.md`.
2. Read `docs/spec.md` and `docs/roadmap.md` when the task touches product
   behavior or planning.
3. Read only the matching domain file under `.claude/context/`.

## Resuming Work (Read This First)

A session has no memory of previous sessions. To pick up where work left off
without losing context:

1. Read `.claude/plans/current.md` — the live backlog (**Next Executable Items**).
2. Read `docs/architecture-decisions.md` for the *why* (load-bearing decisions +
   deferred roadmap with rationale), and `.claude/DISCOVERY_LOG.md` for open gotchas.
3. Run `git branch -a` and `git log --oneline -15` to see the real branch/commit
   state, then continue from the next executable item (or follow `/suite`).

Keep `.claude/plans/current.md` updated when you finish or start work so the next
session resumes cleanly.

## Domain Context Map

| Files touched | Context to read |
|---|---|
| `src/AgentRunner/**`, `src/Core/AgentGoal.cs`, runner tests | `.claude/context/runner.md` |
| `WORKFLOW.md`, `tests/*.yaml`, `schemas/**`, `docs/ai-authoring.md` | `.claude/context/workflow.md` |
| `src/UIAutomation/**`, `src/Core/IAutomationDriver.cs`, `src/Core/AgentAction.cs`, `src/Core/Ui*.cs` | `.claude/context/automation.md` |
| `src/Samples/**`, `tests/testzoo.yaml`, `docs/testzoo.md` | `.claude/context/testzoo.md` |
| `src/AgentRunner/SymphonyWorkbenchGenerator.cs`, `docs/symphony.html`, `scripts/render-ui.ps1` | `.claude/context/workbench.md` |
| `.github/**`, `.azure/**`, `scripts/**`, CI docs, release docs | `.claude/context/ci.md` |
| `.env.template`, `.gitignore`, `LlmService`, provider config, secrets handling | `.claude/context/security.md` |

If a change spans two domains, one session owns the whole change. Do not split a
single feature across parallel sessions unless ownership is explicit.

## Fixed Product Rules

- Portable-first. Local runs, CI, the AgentLoop Workbench, MCP/plugins, and AI
  agents must all use the same CLI/YAML/artifact contract.
- No SaaS dashboard or hosted service as product core.
- Manual-first. Every critical capability needs editable YAML, CLI commands,
  readable artifacts, and short docs.
- AI optional. OpenRouter and other LLMs help runtime execution, but validation,
  listing, workbench rendering, and artifact review must work without `.env`.
- Non-intrusive. Do not require target apps to add agent-specific packages,
  classes, code paths, or source modifications.
- YAML and artifacts are source of truth. The workbench reads them; it does not
  replace them.
- OpenRouter config belongs in the real local `.env`, never in tracked files.
- Keep .NET Framework 4.8 and .NET 8 support healthy.
- First-class Windows desktop targets: WinForms, WPF, MAUI Windows, Avalonia
  Windows Desktop.
- MCP/plugins are adapters over CLI commands, not the core product.

## Parallel Work Rules

- Start with `git status` and identify files already modified by another agent
  or the user.
- Prefer one git worktree or one branch per parallel session.
- Branch naming: `codex/<domain>-<slug>` for Codex, `claude/<domain>-<slug>`
  for Claude Code, or another explicit agent prefix when useful.
- Stay inside your domain file ownership.
- Do not edit another domain context unless your change intentionally changes
  that domain contract.
- Do not revert unrelated changes.
- If you find a cross-domain issue, log it in `.claude/DISCOVERY_LOG.md` instead
  of forcing it into the wrong context.
- If an API, schema, CLI flag, persistent artifact, or public YAML field changes,
  update the matching context and docs in the same change.

## Parallel Session Recipe

Use this pattern for a forked session:

```text
Domain: <runner|workflow|automation|testzoo|workbench|ci|security>
Task: <specific task>

Read AGENTS.md, CLAUDE.md, project_rules.md, then only the matching
.claude/context/<domain>.md file. Own only this domain unless the task clearly
requires a cross-domain change. If you find something important outside the
domain, write it to .claude/DISCOVERY_LOG.md.
```

Suggested split:

- Runner session: CLI, loop, scoring, guards, artifacts.
- Workflow session: YAML plans, schema, prompt policy, authoring docs.
- Automation session: FlaUI/UIA driver, action execution, UI snapshots.
- TestZoo session: samples and multi-framework runtime scenarios.
- Workbench session: static AgentLoop UI.
- CI session: scripts, GitHub Actions, Azure Pipelines, repo docs.
- Security session: `.env`, provider config, secret logging.

## Natural Language Suite Trigger

When the user says any of these:

- `/suite`
- `fais la suite`
- `continue le plan`
- `continue la suite`
- `next step`
- `reprends`

Follow `.claude/commands/suite.md`.

## Validation Commands

Use the smallest validation set that proves the change:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\validate-test-plans.ps1 -SkipRestore
dotnet build .\DesktopAiTestAgent.sln --no-restore -v minimal
dotnet test .\DesktopAiTestAgent.sln --no-restore -v minimal
```

For manual CLI surface:

```powershell
dotnet run --project .\src\AgentRunner\AgentRunner.csproj -f net8.0-windows -- --validate-plan --format json
dotnet run --project .\src\AgentRunner\AgentRunner.csproj -f net8.0-windows -- --list-tests --format json
```

Runtime agent validation may require the user-owned `.env` with OpenRouter and
a launched target desktop app.

## Publication

Do not commit, push, force-push, or open PRs unless the user explicitly asks.
Never commit the real `.env`.
