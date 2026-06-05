# Release Checklist — Road to 1.0 (and how not to break users after)

> **Why this file exists**: once there are real users, the risk is shipping a future
> version (including AI-assisted / "vibe-coded" changes) that silently breaks them.
> The defense is not discipline — it's **freezing a public contract and locking it with
> tests**, so a contract-breaking change becomes a *red test*, not a user ticket.
>
> This checklist is referenced from `.claude/plans/current.md` (so `/suite` surfaces it)
> and from the agent memory `road-to-v1-contract`. Do **not** tag `1.0` until the
> "Definition of 1.0" boxes are checked.

## The public contract (what users depend on)

Everything else (dashboard UI, internals, refactors) is free to change. The contract is now
frozen and authoritative in **[`CONTRACT.md`](../CONTRACT.md)** (repo root); the table below maps
each surface to where it lives in code:

| Surface | Where |
|---|---|
| **CLI**: flags, exit codes, stdout payload format, `--format json` shape | `RunnerOptions`, `Program`, `docs/`, `CONTRACT.md` §1 |
| **YAML test schema**: field names + meaning | `schemas/test-plan.schema.json`, `TestDefinition`, `CONTRACT.md` §2 |
| **Artifacts**: shape read by others | `report.json`, `summary.md`, JUnit (`--to-junit`), `CONTRACT.md` §3 |
| **MCP**: tool names + params | `docs/mcp.md`, `Mcp/McpServer.cs`, `CONTRACT.md` §4 |

## Definition of 1.0 (gate — do all before tagging)

- [x] **Write `CONTRACT.md`** that freezes the surfaces above (the explicit "stable API").
- [x] **Golden / contract tests** that fail on any contract drift (`src/AgentRunner.Tests/ContractTests.cs`):
  - [x] load + validate (zero errors) **every** `tests/**/*.yaml` with the same loader/validator the CLI uses;
  - [x] snapshot the shape (top-level + item key sets) of `--list-tests` / `--validate-plan` `--format json`
        (and the `report.json`/`summary.md`/JUnit shapes are documented in `CONTRACT.md` §3);
  - [x] assert CLI exit codes (invalid args → 2; known-good plan validates → 0) headlessly;
  - [x] assert schema ⇄ loader/validator agreement (`max_steps` bounds, required `goal`, action vocabulary).
- [ ] **`schema_version`** on YAML + a `version` on artifacts, with a **tolerant loader**
      (ignore unknown fields, fill defaults) so new tool reads old files.
- [x] **SemVer policy documented**: `1.x` = additive-only (new flags / **optional** fields
      with defaults); breaking changes only at `2.0`; **deprecate with a `WARN` for ≥1 minor
      before removing/renaming** anything in the contract. (See `CONTRACT.md` § SemVer policy.)
- [ ] **CHANGELOG** has a clear `1.0.0` section + a "Migration" note convention.
- [ ] **Tag `v1.0.0`** (and, when ready, `dotnet tool install -g …@1.0.0` so users pin a version).

## Keep-it-true after 1.0 (every PR)

- [ ] New capability is **additive** (new flag / optional field), never a silent change in
      meaning of an existing one. (Already practiced: `Category`, `AuthoringAgent`,
      `--compose-recording` were all additive.)
- [ ] The `Stop` build+test hook is green and the `code-reviewer` subagent reviewed the diff
      against the domain contracts. A human merges the PR.
- [ ] Any contract change → bump version per the policy + CHANGELOG migration note.
