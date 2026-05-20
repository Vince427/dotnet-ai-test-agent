---
ticket_id: TICKET-WINFORMS-LOGIN-001
title: WinForms login proof
framework: winforms
launch_sample: true
plan: tests/examples/winforms/login.yaml
test_id: EX-WINFORMS-LOGIN-001
target_window: Sample Login App (.NET 8)
evidence_level: full
expected_artifacts:
  - report.json
  - summary.md
  - screenshots
  - ui-tree
---

# WinForms Login Proof

## Goal

Prove the WinForms .NET 8 login scenario can be executed from the portable
contract without app-side agent code.

## Agent Work

- Read the ticket frontmatter and matching YAML plan.
- Use the existing test id `EX-WINFORMS-LOGIN-001`.
- Keep the test non-intrusive: no target app package references, hooks, or
  test-only production paths.
- Prefer YAML-only edits unless the ticket is explicitly expanded to product
  code work.

## Runtime Proof

Run the selected plan through the AgentLoop CLI against the target window. The
proof is complete when the run produces `report.json`, `summary.md`,
screenshots, and UI tree snapshots, and the AgentLoop Workbench can inspect the
run.

## Ticket Comment Payload

Include:

- Result: pass, fail, blocked, or skipped.
- Plan: `tests/examples/winforms/login.yaml`.
- Test id: `EX-WINFORMS-LOGIN-001`.
- Evidence level: `full`.
- Artifact path or Workbench link.
- Short notes for any blocked condition.
