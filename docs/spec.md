# Product Spec

## Goal

Build a portable-first AI UI testing runner for existing .NET applications.

The agent must support:

- WinForms
- WPF
- .NET MAUI Windows
- Avalonia Windows Desktop

Later phases may support MAUI Android, iOS, and Mac through Appium.

## Portable-First Contract

The stable product contract is:

- YAML test definitions;
- CLI commands;
- readable artifacts;
- the static AgentLoop Workbench.

Local users, CI jobs, MCP/plugins, and AI agents must use these same surfaces.
No critical capability should exist only inside a plugin, an MCP server, a
dashboard, or an LLM prompt.

## Non-Intrusive Requirement

The agent must not pollute existing application code.

Do not require:

- agent-specific classes in the target app;
- agent-specific package references in the target app;
- test-only code paths in production code;
- invasive source modifications.

Allow optional improvements only when teams choose them, such as stable AutomationIds or accessibility metadata.

## Test Definition

Directed tests live in YAML files, normally under `tests/`.

Prefer small files: one business scenario per YAML file, grouped by suite or
framework when useful. Large aggregate files are acceptable as a bootstrap
step, but new TestZoo work should move toward `tests/<suite>/<id>.yaml` so
agents and reviewers can load one scenario without scanning the whole backlog.

Each test should define:

- id
- title
- priority
- framework
- target window or target executable
- goal
- success condition
- max steps
- allowed actions
- blocked conditions
- optional links to existing tests

## TestZoo Samples

The sample suite must move beyond login. Add equivalent TestZoo applications for each supported UI framework with common UI patterns:

- login
- form validation
- text, password, checkbox, radio, combo controls
- lists and grids
- CRUD flow
- tabs and navigation
- menus and toolbars
- modal dialogs
- async loading
- disabled/enabled states
- visible errors and status messages
- missing or ambiguous automation metadata
- deliberate crash or failure scenario for guards

The first TestZoo backlog lives in `tests/testzoo.yaml`. Current seed entries cover login, profile save, invalid profile validation, checkboxes, and UI automation metadata audits. The next expansion should prioritize richer WinForms and WPF business/E2E scenarios first, then extend the same patterns to MAUI Windows and Avalonia parity.

## Existing CI/CD And Tests

The agent should integrate around existing test suites instead of replacing them.

Supported strategy:

- link YAML tests to existing unit/integration test names;
- import standard result formats later: TRX, JUnit, NUnit/xUnit XML;
- export agent results in standard CI-friendly formats;
- preserve existing pipelines.

## Manual And AI Workflows

The product must be fully usable manually.

Manual workflow:

- edit YAML test definitions;
- validate plans with CLI commands that do not load `.env`;
- list and select tests from the CLI or workbench;
- run a selected test against a real desktop app;
- inspect Markdown, JSON, screenshots, and workbench output.

AI-assisted workflow:

- Codex, Claude Code, Copilot, or a future MCP/plugin may create or edit YAML;
- CI validates the YAML before runtime execution;
- the runner produces evidence;
- the AI agent can read artifacts and suggest fixes.

The AI workflow must never be the only path for a critical operation.

Manual command output:

- default `text` output is optimized for humans;
- `--format json` is available for validation and test listing;
- JSON output must stay clean on stdout so agents, MCP wrappers, GitHub Actions, and Azure Pipelines can parse it directly.

AI-authored tests should include traceability metadata where possible: `source_issue`, `source_pr`, `authoring_agent`, `risk`, and `ci_profile`. The schema is stored in `schemas/test-plan.schema.json`.

## Runtime Evidence Contract

Runtime execution must produce evidence that humans and coding agents can inspect without rerunning the test.

Evidence levels:

- `minimal`: `report.json` and `summary.md`.
- `standard`: minimal evidence plus screenshots.
- `full`: standard evidence plus UI tree JSON snapshots per step.

The default is `standard`. CI may use `minimal` for fast checks or `full` for flaky/debug runs.

## Mini UI

The AgentLoop Workbench is a local static UI over YAML and artifacts. Existing
filenames may still use `symphony` until the rename migration is complete, but
new product language should use AgentLoop for the orchestrator and workbench.

It should:

- show the backlog;
- show recent runs;
- show test detail;
- show guard failures;
- show screenshots and summaries;
- preview prompts later;
- provide simple choices and filters.

It should not:

- become the source of truth;
- require a database;
- require authentication;
- replace YAML;
- become a SaaS dashboard in early versions.
