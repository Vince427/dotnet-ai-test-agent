# Project Rules & Guidelines

This document consolidates the architecture and workflow rules for Desktop AI Test Agent.

## 1. Mission And Core Priorities

- **Mission**: Build a portable-first desktop test runner for existing .NET applications, usable locally, in CI, and through AI agents.
- **Absolute priorities**:
  1. Runnable code
  2. Clarity
  3. Demo value
  4. Simplicity
  5. Lightweight evolvability
  6. Non-intrusive adoption

## 2. Constraints And Exclusions

- **Hard constraints**: C#, .NET 8, .NET Framework 4.8 legacy support, CLI-first, portable artifacts, simple readable code, few dependencies, no over-engineering.
- **Non-intrusive rule**: The agent must test existing apps from the outside. Do not require app teams to add agent-specific classes, packages, source changes, or production code paths.
- **Framework scope**: Support WinForms, WPF, .NET MAUI Windows, and Avalonia Windows Desktop first. Use Appium later for non-Windows MAUI targets.
- **Source of truth**: YAML tests, CLI behavior, and run artifacts are the source of truth. The mini UI, CI, MCP, plugins, and AI agents read or call those same surfaces; they must not hide or replace them.
- **Manual-first rule**: Every critical capability must work manually through editable YAML, CLI commands, readable artifacts, and short documentation. IA assistance must remain optional.
- **V1 exclusions**: Do not add microservices, Kubernetes, Temporal, Azure, complex auth, complex databases, distributed multi-agent architectures, enterprise dashboards, or advanced cloud infrastructure without an explicit reason.

## 3. Work Loop And Definition Of Done

- **Work loop**:
  1. Read specs and architecture.
  2. Implement the smallest runnable increment.
  3. Run verification scripts.
  4. Fix issues before adding another layer.
- **Definition of done**:
  - The code builds.
  - Tests pass.
  - The demo run remains understandable.
  - Files stay aligned with the spec.
  - No refactor introduces unnecessary complexity.

## 4. Fix Build Guidelines

- Do not massively refactor.
- Fix compilation errors first, then runtime issues.
- Do not add unnecessary architecture.
- Keep the demo runnable.

## 5. Architecture And Folder Roles

- `src/Core`: Models, interfaces, and lightweight abstractions.
- `src/UIAutomation`: FlaUI integration layer.
- `src/AgentRunner`: Observe -> Decide -> Act -> Guard -> Score -> Record loop.
- `src/Samples`: Demo target applications.
- `tests`: YAML test backlog.
- `runs`: Generated artifacts, ignored by git.

## 6. Sample App Strategy

- Move beyond login-only demos.
- Add TestZoo samples for WinForms, WPF, Avalonia, and MAUI Windows.
- Cover common UI workflows: forms, validation, lists, grids, CRUD, modals, async states, disabled controls, and accessibility edge cases.

## 7. Existing Tests And CI/CD

- Do not replace existing unit, integration, or CI tests.
- Link YAML tests to existing test names or CI checks when useful.
- Export standard results later: JUnit, TRX, JSON, Markdown, screenshots.
- Preserve existing pipelines and secrets handling.
- Codex, Claude Code, Copilot, MCP, and plugins may call the runner, but they must use the same CLI/YAML/artifact surfaces that humans and CI can use.

## 8. UX Guidelines

- Keep the Symphony Workbench simple and local.
- Offer useful choices: suite, test id, framework, status filter, result filter, and prompt preview.
- Prefer readable tables, direct links to artifacts, and clear evidence over complex dashboard behavior.
- Do not introduce auth, storage, or server-side state until there is a concrete need.
