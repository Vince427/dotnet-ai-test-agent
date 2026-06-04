# AI Authoring Guide

This project can be used manually or with Codex, Claude Code, Copilot, MCP wrappers, GitHub Actions, and Azure Pipelines.

The same portable contract applies everywhere: edit YAML, call CLI commands,
read artifacts. Plugins and MCP wrappers are convenience layers over that
contract.

## Ground Rules

- Edit YAML test plans under `tests/` or an external test pack.
- Do not modify the target application code unless the user explicitly asks for app changes.
- Do not add agent-specific packages, classes, hooks, or test-only code paths to the app being tested.
- Prefer linking existing tests with `existing_tests` instead of duplicating unit or integration coverage.
- Validate plans before proposing a PR.

## Required Flow

1. Read the user story, bug report, manual QA steps, or existing CI failure.
2. Add or update YAML tests only.
3. Include traceability metadata when available: `source_issue`, `source_pr`, `authoring_agent`, `risk`, `ci_profile`.
4. Run `--validate-plan --format json`.
5. Run `--list-tests --format json` and check that the expected test id appears.
6. Leave runtime execution to the user, CI, or an explicit follow-up when a desktop session is available.

## Commands

```powershell
dotnet run --project .\src\AgentRunner\AgentRunner.csproj -f net8.0-windows -- --validate-plan --format json
dotnet run --project .\src\AgentRunner\AgentRunner.csproj -f net8.0-windows -- --list-tests --format json
```

For CI, prefer the shared script:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\validate-test-plans.ps1
```

GitHub Actions and Azure Pipelines examples are provided in `.github/workflows/` and `.azure/pipelines/`.

GitHub Copilot and GitHub-native coding agents should also read
`.github/copilot-instructions.md`, which is a short repo-local entry point for
the same rules.

Detailed private prompts are intentionally not tracked in this public repo.
Keep reusable public guidance short and product-focused; use local ignored files
such as `*.prompt.local.md` or `.private-prompts/` for team-specific agent
prompts.

## Fields To Fill

The smallest valid test is just a `goal`. A copy-paste, fully-commented template with
every field is at [`docs/test-template.yaml`](test-template.yaml). Core fields:

| Field | Required | What to put |
|---|---|---|
| `goal` | yes | Plain-language intent; name the exact values (e.g. "Enter admin / password123…") |
| `success_condition` | no | Text the app shows when done; omit and verify with an `Assert` |
| `framework` | no | `winforms` \| `wpf` \| `avalonia` \| `maui` |
| `target_window` | no | Exact window title to attach to |
| `max_steps` | no | Safety cap on iterations (typical 6-12) |
| `allowed_actions` | no | Subset of `EnterText, Click, DoubleClick, Scroll, Wait, Assert, Done, Explore` |
| `category` | no | Test style — see below (default `Scenario`) |
| `priority` | no | `P0`-`P3` · `risk` `low`-`critical` · `tags` free-form |

### Category taxonomy

- **Scenario** — a directed business flow (most tests). Has a clear goal + success.
- **Smoke** — a quick "does it open / does the basic path work" check.
- **Audit** — inspect UI / accessibility metadata (AutomationId coverage, labels); no mutation.
- **Monkey** — exploratory / stress poking to surface crashes or dead ends.

## YAML Metadata

- `existing_tests`: ids of existing automated tests this run complements (e.g. a TRX/JUnit
  testcase name like `MyApp.Tests.LoginTests.HappyPath`). Prefer linking over duplicating.
- `source_issue`: issue, work item, bug id, or ticket URL.
- `source_pr`: pull request that introduced or updated the test.
- `authoring_agent`: `manual`, `codex`, `claude-code`, `copilot`, or team-specific value.
- `risk`: `low`, `medium`, `high`, or `critical`.
- `ci_profile`: where the test is expected to run, such as `local-windows`, `github-windows`, or `azure-windows`.

These links surface in the CI report: `--to-junit` emits each as a `<property>` on the
run's `<testcase>` (`existing_test`, `source_issue`, `source_pr`, `trace_id`), so a CI
dashboard can cross-link the AgentLoop run to its existing-test counterpart and live trace.

```yaml
tests:
  LOGIN-HAPPY-001:
    title: "Login happy path"
    goal: "Enter admin / password123, click Login, confirm success."
    success_condition: "Login successful"
    existing_tests: ["MyApp.UiTests.LoginTests.HappyPath"]
    source_issue: "JIRA-1234"
    source_pr: "https://github.com/acme/app/pull/567"
```

The schema lives at `schemas/test-plan.schema.json`.
