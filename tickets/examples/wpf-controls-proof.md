---
ticket_id: TICKET-WPF-CONTROLS-001
title: WPF controls proof
framework: wpf
launch_sample: true
plan: tests/examples/wpf/controls-selection.yaml
test_id: EX-WPF-CONTROLS-001
target_window: WPF AI Test Target
evidence_level: full
expected_artifacts:
  - report.json
  - summary.md
  - screenshots
  - ui-tree
---

# WPF Controls Proof

## Goal

Prove the WPF controls scenario covers radio, combo, list, and grid behavior
through the same YAML, CLI, artifacts, and Workbench contract.

## Agent Work

- Read the ticket frontmatter and matching YAML plan.
- Use the existing test id `EX-WPF-CONTROLS-001`.
- Preserve the current WPF sample and test files unless the ticket is expanded
  to explicitly request code changes.
- Keep any authoring traceable back to this ticket id.

## Runtime Proof

Run the selected plan through the AgentLoop CLI against the target window. The
proof is complete when the run produces `report.json`, `summary.md`,
screenshots, and UI tree snapshots, and the AgentLoop Workbench can inspect the
run.

## Ticket Comment Payload

Include:

- Result: pass, fail, blocked, or skipped.
- Plan: `tests/examples/wpf/controls-selection.yaml`.
- Test id: `EX-WPF-CONTROLS-001`.
- Evidence level: `full`.
- Artifact path or Workbench link.
- Short notes for any blocked condition.
