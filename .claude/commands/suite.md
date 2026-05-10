---
description: Continue the next executable item from the active plan.
allowed-tools: Read, Edit, Grep, Glob, Bash, Agent
---

# /suite - Continue The Plan

Use this procedure when the user says `/suite`, `fais la suite`,
`continue le plan`, `continue la suite`, `next step`, `reprends`, or equivalent.

## Procedure

1. Read `AGENTS.md`, `CLAUDE.md`, and `project_rules.md`.
2. Read `.claude/plans/*.md` if present, newest first. Otherwise use
   `docs/roadmap.md` as the active plan.
3. Identify the next unchecked or `In Progress` item that is executable without
   a human-only dependency.
4. If `$ARGUMENTS` is provided, filter by domain, version, suite, or keyword.
5. Map the expected files to the domain table in `CLAUDE.md`.
6. Read only the matching `.claude/context/<domain>.md` files.
7. Run `git status` and protect user or other-agent changes.
8. Implement the smallest useful increment.
9. Update tests/docs/context when public behavior changes.
10. Validate with the smallest command set that proves the change.
11. Report what changed, what passed, and the next executable item.

## Human-Only Or Blocked Items

Do not attempt items that require a human account, secret, GUI decision, paid
provider action, or unpublished business decision. Say why it is blocked and
propose the next executable item.

Examples:

- real OpenRouter key creation;
- publishing credentials;
- accepting legal or store-console prompts;
- manual QA on a machine the agent cannot access.

## Discovery Rule

If you find an important issue outside your domain, add an entry to
`.claude/DISCOVERY_LOG.md` and continue the scoped task unless the issue makes
the task unsafe.

## Publication

Do not commit or push unless the user explicitly asks.

Argument provided: `$ARGUMENTS`

