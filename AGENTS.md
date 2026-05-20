# AGENTS.md - Desktop AI Test Agent

Portable entry point for Codex and other coding agents.

## Read First

1. `CLAUDE.md`
2. `project_rules.md`
3. The matching `.claude/context/<domain>.md` for files you will touch.

## Core Rules

- Portable-first desktop test runner: local, CI, AgentLoop Workbench, and agents
  all use the same CLI/YAML/artifact contract.
- Manual-first: YAML, CLI, artifacts, and docs must work without AI.
- AI optional: OpenRouter is for runtime assistance, not validation/listing/UI.
- MCP and plugins are adapters over CLI commands, not the product core.
- Non-intrusive: never require target apps to reference agent packages or add
  agent-only production code.
- Keep `.env` ignored and out of commits. Use `.env.template` placeholders only.
- Do not revert user or other-agent work.
- Keep WinForms, WPF, MAUI Windows, and Avalonia direction aligned.

## If The User Says "Continue The Plan"

Follow `.claude/commands/suite.md`.

Equivalent triggers include:

- `/suite`
- `fais la suite`
- `continue le plan`
- `continue la suite`
- `next step`
- `reprends`

## Git

Check `git status` before and after. Commit or push only when explicitly asked.
