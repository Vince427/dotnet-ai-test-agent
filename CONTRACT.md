# Public Contract (the stable API)

This file freezes the surfaces users and integrations depend on. **Everything not listed here
(dashboard UI, internals, refactors, log wording) is free to change.** A contract change is a
breaking change: bump per the SemVer policy below and add a CHANGELOG migration note.

Locked by golden/contract tests in `src/AgentRunner.Tests/ContractTests.cs`. If one of those
fails, either the change is a contract break (revert, or schedule for `2.0`) **or** this file must
be updated in the same change. See `docs/release-checklist.md` for the road-to-1.0 gate.

---

## 1. CLI

Entry point: `AgentRunner` (`dotnet run --project src/AgentRunner` or the published `AgentRunner.exe`).

### Exit codes

| Code | Meaning |
|---|---|
| `0` | Success. For runtime runs: the run completed and the goal succeeded. For manual commands: the command produced its output (e.g. validation passed, tests listed). |
| `1` | Operational failure (a run failed/aborted/loop-detected; a server failed to start; a recording/compose produced an invalid draft). |
| `2` | Bad usage or "nothing to do": invalid/unknown arguments, an invalid flag value, a required value missing, no plans found, a requested test id not found, or validation found errors. |

Invalid arguments always map to `2` (`Program` catches `RunnerOptions.Parse`'s `ArgumentException`).

### Flags (stable)

These flag names and meanings are part of the contract. New flags may be **added** at `1.x`;
existing ones are not renamed or repurposed before `2.0` (deprecate with a `WARN` for ≥1 minor first).

| Flag | Value | Purpose |
|---|---|---|
| `--window <title>` (also first positional) | window title | Target window to attach to. |
| `--goal-name <name>` | name | Select a named goal from workflow config. |
| `--goal <text>` / `--success <text>` / `--goal-id <id>` | text | Override goal description / success condition / identifier. |
| `--max-steps <n>` | positive int | Override the step cap. |
| `--plan <path>` | path | Plan YAML to use. |
| `--suite <name>` | name | Resolve `tests/<name>.yaml`. |
| `--test-id <id>` | id | Select one test. |
| `--evidence-level <minimal\|standard\|full>` | enum | Artifact capture level. |
| `--format <text\|json>` | enum | Output format for manual commands (see JSON section). |
| `--validate-plan [path]` | optional path | Validate plan(s). Manual, key-free. |
| `--list-tests [path]` | optional path | List discovered tests. Manual, key-free. |
| `--analytics` | — | Summarise `runs/` history (flaky / selector-drift / duration). Manual, key-free. |
| `--show-prompt --test-id <id> [--plan <path>]` | — | Preview the LLM prompt for a test. Key-free. |
| `--to-junit [path]` | optional path | Convert captured runs to JUnit XML (default `artifacts/junit-results.xml`). |
| `--render-ui <path> [--watch]` | path | Render the static AgentLoop Workbench HTML. |
| `--write-guard-demos [root]` | optional path | Write guard-failure demo artifacts. |
| `--dashboard [port]` | optional port (1–65535, default 8090) | Local-only dashboard server. |
| `--bridge-llm [port]` / `--bridge-io <dir>` | optional port / dir | Key-free human/agent-in-the-loop LLM bridge. |
| `--vision` | — | Enable the V3 Tier-2 vision fallback decider. |
| `--vision-bridge <dir>` | dir | Key-free agent-in-the-loop vision loop. |
| `--mcp` | — | Serve the MCP adapter over stdio (read-only by default). |
| `--mcp-allow-write` (or `AGENTLOOP_MCP_ALLOW_WRITE=1`) | — | Modifier of `--mcp`: enable the opt-in `create_test` write tool. NOT a standalone mode. |
| `--compose-recording <session.json> [--out <draft.yaml>]` | path | Transform a recorded session into a YAML draft. Key-free. |
| `--record [--window <title>] [--out <session.json>] [--seconds <n>]` | — | Live UIA capture (env-bound). |
| `--replay <session.json>` | path | Replay a recorded UIA session deterministically. Key-free. |
| `--heal-apply` | — | Apply the self-healing suggestion to the target YAML test plan. |
| `--run <runId>` | string | Select a specific run ID to apply healing suggestions from. Modifier of `--heal-apply`. |
| `--yes` | — | Skip confirmation prompt when applying healing. Modifier of `--heal-apply`. |

The "manual" / one-shot modes (`--render-ui`, `--validate-plan`, `--list-tests`, `--analytics`,
`--write-guard-demos`, `--to-junit`, `--dashboard`, `--bridge-llm`, `--mcp`, `--show-prompt`,
`--compose-recording`, `--record`, `--replay`, `--heal-apply`) are **mutually exclusive** — combining two is exit `2`.
(`--mcp-allow-write` is a modifier of `--mcp`, not a mode, so it does not count.)

### `--format json` (stdout payload)

`--format json` is accepted **only** with `--validate-plan`, `--list-tests`, `--show-prompt`, and
`--analytics` (any other use is exit `2`). When set, those commands emit a single JSON object on
**stdout**; diagnostics/warnings go to **stderr**. JSON uses **camelCase** keys and string-valued enums.
- `--analytics --format json` → `kind:"runAnalytics"`, plus the `RunAnalyticsResult` fields
  (`totalRuns`, per-test pass/fail + `flaky`, `selectorDrift` groups, duration/step stats, most-failing).

- `--list-tests --format json` → `kind:"testList"`, plus `valid`, `count`, `tests[]`, `errors[]`.
  Each `tests[]` item: `planPath, suite, id, title, priority, framework, targetWindow, sourceIssue,
  sourcePr, authoringAgent, risk, ciProfile, goal, successCondition, maxSteps, allowedActions, tags,
  existingTests`.
- `--validate-plan --format json` → `kind:"planValidation"`, plus `valid`, `planCount`, `testCount`,
  `errorCount`, `warningCount`, `plans[]`, `errors[]`, `warnings[]`. Each `plans[]` item:
  `path, suite, testCount, valid, errors, warnings`.
- `--show-prompt --format json` → `kind:"promptPreview"`, plus `testId`, `prompt`.

Adding a new key is additive (allowed at `1.x`); renaming or removing a key, or changing a `kind`
literal, is breaking.

---

## 2. YAML test schema

Source of truth: `schemas/test-plan.schema.json` (+ the tolerant `TestPlanLoader`). A plan is a map
of `tests:` keyed by test id; an optional top-level `suite:`.

- **Required**: `tests` (plan), `goal` (each test). Everything else is optional with sensible
  defaults — the loader ignores unknown lines, so new tool reads old files and vice versa.
- **Fields**: `title, priority (P0–P3), framework (winforms|wpf|maui|avalonia), target_window,
  source_issue, source_pr, authoring_agent, risk (low|medium|high|critical), ci_profile,
  category (Scenario|Smoke|Audit|Monkey), goal, success_condition, max_steps (1–100),
  allowed_actions, tags, blocked_if, existing_tests, selectors, schema_version`.
- **`allowed_actions` vocabulary**: `EnterText, Click, DoubleClick, Scroll, Wait, Assert, Done,
  Explore` (must match `Core.ActionVocabulary`).
- `max_steps` bounds: schema `minimum: 1`, `maximum: 100`; the loader rejects `<= 0`; the validator
  warns (non-fatal) above 100.

New **optional** fields with defaults may be added at `1.x`. Removing/renaming a field, or making an
optional field required, is breaking.

Discovery: all `tests/**/*.yaml` (and `.yml`) **except** anything under `tests/archived/`.

---

## 3. Artifacts

Written under `runs/<runId>/` by a run; read by the dashboard, workbench, JUnit export, and MCP.

### `report.json` (camelCase, string enums)

Top-level: `runId, evidenceLevel, goalDescription, goalIdentifier, testId, testTitle, testPriority,
  framework, suite, targetWindow, startedAt, endedAt, result, finalScore, errorMessage, traceId,
  existingTests, sourceIssue, sourcePr, steps[], version`.

`result` is one of `Running, Succeeded, Failed, Aborted, LoopDetected`.

Each `steps[]` item: `stepNumber, timestamp, uiStateSnapshot, actionType, actionTarget, actionValue,
  reasoning, outcome, failureCode, failureMessage, guardStatus, guardCode, guardMessage, scoreDelta,
  cumulativeScore, screenshotPath, uiTreePath, overlayPath, overlayIndexPath, healingSuggestion`.
  * `healingSuggestion` contains: `oldTarget`, `newTarget`, `newName`, `controlType`, `confidence`, `rationale`.

### `summary.md`

Human-readable Markdown: a header block (Goal, Test, Suite, Framework, Target, Evidence level,
Result, Score, Started/Ended, Error) and a `## Steps` table
(`# | Action | Target | Outcome | Failure | Guard | Score | Evidence`), plus an optional
`## Selector Healing Suggestions` section when any step carries a suggestion.

### JUnit XML (`--to-junit`)

`<testsuites>` → `<testsuite name="DesktopAiTestAgent">` → `<testcase name classname time>` per run.
`Succeeded` = pass; `Failed`/`Aborted`/`LoopDetected` = `<failure>`; anything else = `<error>`.
Cross-link `<property>` names: `existing_test, source_issue, source_pr, trace_id`.

---

## 4. MCP (adapter; read-only by default)

`--mcp` serves JSON-RPC 2.0 over stdio (protocol `2024-11-05`, server `agentloop`). It is an adapter
over the same loaders — **read-only and key-free by default**, nothing that spawns a run. Tool names +
params (stable):

| Tool | Params | Returns |
|---|---|---|
| `list_tests` | — | `{ count, tests[] }` (id, title, framework, priority, category, suite, tags, planPath, selectors). |
| `validate_plan` | `path?` (repo-relative) | `{ valid, planCount, testCount, errorCount, warningCount, plans[], errors[], warnings[] }`. |
| `list_runs` | — | `{ count, runs[] }` (runId, testId, result, finalScore, startedAt, endedAt, steps). |
| `get_run` | `runId` (required) | the run's `report.json` passed through. |
| `show_prompt` | `testId` (required), `path?` | `{ testId, prompt }`. |
| `create_test` *(opt-in write)* | `id` (required, safe-segment), `goal`, `framework`, `title`, `targetWindow`, `category`, `allowedActions[]`, `tags[]`, `successCondition`, `maxSteps`, `suite`, `priority` | `{ ok, id, planPath, warnings[] }` — writes a validated `tests/created/<id>.yaml`. |

`create_test` is the only write tool and is **disabled by default**: it is advertised + callable only
when `--mcp-allow-write` (or `AGENTLOOP_MCP_ALLOW_WRITE=1`) is set; otherwise it is not listed and a call
returns a "writes are disabled" tool error. It reuses the same YAML emitter + validator as the dashboard.
`run_test` (spawning a run) is intentionally not exposed. Adding a tool or an optional param is additive;
renaming/removing a tool or required param is breaking.

---

## SemVer policy

- `1.x` = **additive only**: new flags, new **optional** YAML fields with defaults, new JSON keys, new
  MCP tools. The meaning of an existing flag/field/key never silently changes.
- **Breaking changes only at a major bump (`2.0`)**, and only after deprecating with a `WARN` for ≥1
  minor release.
- Every contract change → version bump per this policy + a CHANGELOG **Migration** note.
