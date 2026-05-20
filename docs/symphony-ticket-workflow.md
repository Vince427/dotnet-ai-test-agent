# Ticket-To-Evidence Workflow

This workflow makes ticket-driven agent work repeatable without making
OpenAI Symphony, MCP, a plugin, or an LLM the product core. External
orchestrators can coordinate sessions and hooks, but the local contract stays:

```text
ticket -> isolated workspace -> YAML/code -> CLI -> artifacts -> AgentLoop Workbench -> ticket or PR comment
```

The invariant is the same as the rest of the repo: a human must be able to
edit the YAML, run the CLI, inspect artifacts, and open the Workbench without
AI or a hosted dashboard.

## Ticket Contract

Each ticket should carry enough structured data for scripts and agents to start
without rereading a full issue thread:

- `ticket_id`: stable issue, work item, or local ticket id.
- `title`: short human title.
- `framework`: `winforms`, `wpf`, `maui-windows`, or `avalonia-windows`.
- `plan`: YAML test plan path, usually under `tests/` or an external test pack.
- `test_id`: selected test id inside the plan.
- `target_window`: expected desktop window title.
- `evidence_level`: `minimal`, `standard`, or `full`.
- `launch_sample`: optional `true`/`false` flag for built-in WinForms/WPF
  examples, letting the proof script start and stop the sample app around the
  AgentLoop runtime run.
- `expected_artifacts`: expected runtime outputs, normally `report.json`,
  `summary.md`, screenshots, and UI tree snapshots when `evidence_level` is
  `full`.

Markdown tickets can store these fields in frontmatter. GitHub issue forms can
mirror the same field ids so an adapter can normalize either source into the
same payload.

## Flow

1. Create or select a ticket.
   Capture the scenario, expected visible result, related tests, target
   framework, target window, evidence level, and expected artifacts.

2. Create an isolated workspace.
   Use a git worktree, branch checkout, or clean repo copy per ticket. Do not
   copy `.env` into generated ticket material or commits. Keep target
   applications non-intrusive: no agent-only packages, production code paths, or
   source changes unless the ticket explicitly asks for application code work.

3. Run the optional `after_create` hook.
   This hook belongs to the orchestration layer, not the product core. It can
   normalize ticket fields, write a local ticket markdown file, create a branch
   or workspace, and run read-only discovery such as listing plans. It should
   not depend on `.env`, OpenRouter, or runtime desktop automation.

4. Start the agent session.
   The agent reads the ticket plus repo context, then edits the smallest owned
   surface. For pure test requests this usually means YAML only. For product
   issues it may include repo code, docs, or tests, followed by the same CLI
   and artifact contract.

5. Run the optional `before_run` hook.
   This hook should verify that the selected plan and `test_id` exist, the
   requested evidence level is valid, the target app/window is known, and the
   workspace has no unrelated edits. It can run validation commands that work
   without `.env`.

6. Build and validate.
   Validate YAML first. If repo code changed, build and run the relevant unit
   or integration tests before attempting desktop E2E execution.

7. Run AgentLoop E2E.
   Execute the selected YAML test through the CLI against the desktop target.
   The runner writes artifacts for the chosen evidence level:
   `report.json`, `summary.md`, screenshots, and optional UI tree snapshots.

8. Open the AgentLoop Workbench.
   The Workbench reads YAML and artifacts. It is the local review surface for
   backlog, runs, guard failures, screenshots, summaries, and prompt previews.
   It does not replace YAML or the CLI.

9. Comment back on the ticket or PR.
   Post the result, artifact path, evidence level, failures, screenshots or
   Workbench link, and any follow-up work. The comment should be useful even if
   the agent session is gone.

## Hook Shape

The hook names are intentionally generic so OpenAI Symphony, a GitHub Action,
a local script, or another agent wrapper can implement them.

```yaml
hooks:
  after_create:
    inputs:
      - ticket_id
      - framework
      - plan
      - test_id
    actions:
      - create_isolated_workspace
      - write_ticket_snapshot
      - validate_ticket_fields
      - list_available_tests
  before_run:
    inputs:
      - workspace
      - plan
      - test_id
      - evidence_level
      - target_window
    actions:
      - verify_workspace_scope
      - validate_plan
      - verify_test_id
      - verify_expected_artifacts
```

These hooks are adapters over the repo contract. They must not introduce a
second source of truth for tests, validation, run state, or evidence.

## Definition Of Evidence

Use the existing evidence levels:

- `minimal`: `report.json` and `summary.md`.
- `standard`: minimal evidence plus screenshots.
- `full`: standard evidence plus UI tree JSON snapshots per step.

A ticket is evidence-complete when the expected artifacts exist, the summary is
readable by a human reviewer, and the Workbench can navigate to the run without
requiring hidden agent state.

## Review Checklist

- Ticket fields are normalized and script-readable.
- Workspace is isolated from other tickets and agents.
- YAML remains editable manually.
- CLI validation passes before runtime E2E.
- Code changes, if any, are covered by focused build/tests.
- Runtime artifacts match `evidence_level`.
- Workbench can inspect the run.
- Ticket or PR comment links the artifact path and summarizes the result.
