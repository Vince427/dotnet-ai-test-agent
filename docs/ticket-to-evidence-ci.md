# Ticket-To-Evidence CI

This document describes the deterministic CI lane for turning a GitHub issue or
OpenAI Symphony ticket into reviewable proof artifacts without making AI,
desktop focus, or hosted runtime automation part of the core product.

## Goals

- Keep GitHub Actions as a portable proof runner over the same CLI, YAML, and
  artifact contract used locally.
- Let GitHub issues and Symphony work items point to CI evidence without making
  either system the source of truth.
- Prove ticket readiness with build, tests, plan validation, and a ticket proof
  dry run.
- Keep real desktop automation off GitHub-hosted runners by default.

## Deterministic CI Lane

Use this lane for pull request or ticket evidence that must be safe on a
GitHub-hosted Windows runner:

1. Check out the repository.
2. Install .NET 8.
3. Restore, build, and test the solution.
4. Validate YAML plans and emit plan artifacts.
5. Run the ticket proof command in dry-run mode:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-ticket-proof.ps1 -TicketPath .\tickets\examples\winforms-login-proof.md -SkipRuntime -DryRun
```

The dry run may inspect plans, render intended commands, and produce proof
metadata, but it must not launch target desktop applications, require a real
interactive desktop, call OpenRouter, or read `.env`.

The manual workflow in `.github/workflows/ticket-proof.yml` follows this lane.
It is `workflow_dispatch` only, so it does not add background CI pressure while
the proof contract is still being shaped.

## Local And Runtime Lane

Runtime E2E proof is separate from deterministic CI. Run it locally or on a
self-hosted interactive Windows runner after the target app, provider settings,
and desktop focus are intentionally prepared.

The runtime lane can remove `-SkipRuntime` and `-DryRun` only when the operator
expects real UI automation and has configured any optional AI provider outside
tracked files. Built-in WinForms/WPF tickets may also set `launch_sample: true`
or pass `-LaunchSample`, which starts the sample executable before the AgentLoop
run and stops it afterward. CI evidence should clearly label these runs as
runtime evidence, not deterministic dry-run proof.

## GitHub Issues And Symphony

GitHub issues and Symphony tickets should carry planning context, acceptance
criteria, and links to evidence. They should not replace YAML plans, CLI output,
or artifact files.

A typical handoff is:

1. A GitHub issue or Symphony task names the business behavior to prove.
2. A human or coding agent updates the relevant YAML plan and implementation.
3. The manual proof workflow is dispatched with the ticket reference in the run
   title or issue comment.
4. GitHub Actions uploads validation and proof artifacts.
5. The issue or Symphony task links back to the workflow run and summarizes the
   result for reviewers.

Symphony may orchestrate this flow through GitHub APIs or issue comments, but
the product remains manual-first: the same commands can be run from PowerShell
without Symphony, OpenRouter, or a plugin.

## Evidence Contract

Deterministic proof should leave enough evidence for a reviewer to answer three
questions without opening a desktop app:

- Did the code build and did unit/integration tests pass?
- Did YAML validation and test listing succeed?
- Which ticket proof command would run, and which runtime actions were skipped?

Expected artifacts include the existing plan validation files under
`artifacts/test-plans/` and any dry-run proof files produced by
`scripts/run-ticket-proof.ps1`. If a later script adds a new persistent artifact
name or schema, update this document and `.claude/context/ci.md` in the same
change.

## Hosted Runner Safety

Hosted CI must not:

- launch WinForms, WPF, MAUI Windows, or Avalonia sample apps for ticket proof;
- require `.env` or provider secrets;
- rely on OpenRouter for validation, listing, or proof acceptance;
- treat Symphony or GitHub issue text as more authoritative than YAML and
  artifacts.

Use a self-hosted runner or local machine for real desktop automation. Keep the
manual GitHub Actions proof lane deterministic until a specific runtime lane is
designed and documented.
