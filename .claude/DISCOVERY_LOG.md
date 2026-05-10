# Discovery Log

Inbox for unknown unknowns discovered by agents.

Use this file when an observation is important but does not belong cleanly in a
single `.claude/context/<domain>.md` contract. This keeps parallel sessions from
polluting the wrong domain while still preserving the discovery for the human
orchestrator.

## When To Write Here

- You find a bug that crosses runner, UI automation, YAML, samples, or CI.
- You notice a security or secrets risk.
- You find a hidden coupling, dead code path, flaky assumption, or duplicate
  responsibility.
- You discover that the current domain map is missing a new subsystem.
- You are not the owner of the affected domain but the observation matters.

## When Not To Write Here

- You change the public API of your own domain: update that domain context
  directly.
- You fix a small internal bug inside your domain.
- You have a cosmetic suggestion that belongs in a PR summary.

## Entry Format

```markdown
## YYYY-MM-DD - branch/session - origin domain

**Observation**: what you saw, with files and lines when useful.

**Why it matters**: one or two sentences.

**Suggestion**: optional next step.

**Status**: `OPEN` | `IN PROGRESS` | `CLOSED - see <ref>` | `CLOSED - not an issue`
```

## Open Entries

_None yet. Created on 2026-05-10._

## Archive

_Empty._

