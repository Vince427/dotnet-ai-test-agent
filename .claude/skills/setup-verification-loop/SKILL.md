---
name: setup-verification-loop
description: >-
  Scaffold a verification-first agentic dev loop into a repository: Claude Code
  hooks (a Stop gate that runs build+test and blocks finishing when it fails, and
  a PreToolUse guard that blocks dangerous shell commands), a read-only QA review
  subagent, and minimal hygiene (.editorconfig, CHANGELOG). Stack-aware (dotnet,
  node, python, go, rust). Use this skill whenever the user wants to set up Claude
  Code hooks, a build/test gate, agent guardrails, a code-review subagent, or asks
  to "make the agent verify its own work" / "stop the agent saying done while tests
  are red" / "add a pre-commit-style check for Claude". Trigger even if they only
  say "set up hooks", "add a verification gate", or "harden this repo for agents".
---

# Setup Verification Loop

## Why this exists

The single highest-ROI thing you can give a coding agent is a **pass/fail signal it
can read and loop on**. Give it `build + test` as a gate and it self-corrects:
it does the work, runs the check, reads the result, fixes, repeats — instead of
declaring "done" while the build is red and making the human the feedback loop.

This skill scaffolds that loop. But it stays **deliberately lean**. Empirical work
(ETH Zürich, arXiv:2602.11988) found that piling context/instruction files on an
agent *reduces* success and raises cost — so the value here is the **verification
hooks and a focused reviewer**, not volume of docs. Resist the urge to over-scaffold.

## What it installs

1. **`.claude/settings.json`** (committed, shared) with two hooks:
   - **`Stop`** → a verify script that runs the stack's build + tests and **exits 2
     to block finishing** when they fail (stderr is fed back to the agent).
   - **`PreToolUse`** (matcher `Bash`) → a guard script that **exits 2 to block**
     dangerous commands (`.env` writes, `rm -rf`, force-push, `--no-verify`).
2. **`.claude/scripts/verify.*` + `guard.*`** — the hook scripts, **conditional**
   (only build/test when source changed), **OS-aware**, with an `AGENT_SKIP_VERIFY=1`
   escape hatch for intentional WIP stops.
3. **`.claude/agents/code-reviewer.md`** — a **read-only** QA subagent that reviews
   the diff against the repo's rules and invariants with a fresh context.
4. **Hygiene**: `.editorconfig` (deterministic style) and `CHANGELOG.md` — only if
   absent.

## Procedure

### 1. Detect the stack and its commands

Look for the project markers and pick the build/test/lint commands. Confirm with
the user before writing anything — and ask if a command differs from the default.

| Marker | Stack | Build | Test | Lint (optional) |
|---|---|---|---|---|
| `*.sln` / `*.csproj` | dotnet | `dotnet build -v minimal` | `dotnet test -v minimal` | `dotnet format --verify-no-changes` |
| `package.json` | node | `npm run build` (if present) | `npm test` | `npm run lint` |
| `pyproject.toml` / `setup.py` | python | — | `pytest -q` | `ruff check .` |
| `go.mod` | go | `go build ./...` | `go test ./...` | `go vet ./...` |
| `Cargo.toml` | rust | `cargo build` | `cargo test` | `cargo clippy` |

Reuse commands the repo already documents (e.g. a `CLAUDE.md` "Validation Commands"
section, a `Makefile`, or CI workflow) rather than inventing new ones.

### 2. Decide hook placement and shell

- **Placement**: project-shared `.claude/settings.json` (committed, reviewed in PRs)
  by default. Use `.claude/settings.local.json` (gitignored) only if the user wants
  to try it privately first. Committed hooks run on every contributor's machine —
  say so, and keep the scripts simple and reviewable.
- **Shell**: pick the script flavor that's always present. Windows-only repo →
  `powershell` + `.ps1`. Unix → `bash` + `.sh`. Cross-platform → ship both and let
  the script no-op on the OS that can't run the suite.

### 3. Write the files (templates below)

Generate the scripts with the detected commands substituted in. Keep the verify
script **fast**: skip the gate when no source files changed this session (so docs
turns stay instant), use incremental build, and degrade to a no-op (exit 0) on an
OS that cannot run the suite rather than failing every stop.

### 4. Validate the gate

Prove it works before declaring done:
- Run the verify script on the clean repo → exit 0.
- Temporarily introduce a failing test, run it → **exit 2** with the error, then
  remove the temp test.
- Pipe a dangerous command JSON into the guard → **exit 2**; a safe one → exit 0.

The **hook wiring itself** only fires inside a Claude Code session, so tell the user
to start a fresh session to see the `Stop`/`PreToolUse` hooks live — the scripts are
what you can validate standalone.

## Templates

### `.claude/settings.json`

```json
{
  "hooks": {
    "Stop": [
      { "hooks": [ { "type": "command", "command": "<SHELL>",
        "args": ["<ARGS>", "${CLAUDE_PROJECT_DIR}/.claude/scripts/verify.<EXT>"] } ] }
    ],
    "PreToolUse": [
      { "matcher": "Bash", "hooks": [ { "type": "command", "command": "<SHELL>",
        "args": ["<ARGS>", "${CLAUDE_PROJECT_DIR}/.claude/scripts/guard.<EXT>"] } ] }
    ]
  }
}
```

For Windows: `"command": "powershell"`, args `["-NoProfile","-ExecutionPolicy","Bypass","-File", ...]`.
For Unix: `"command": "bash"`, args `["...path..."]`.

### verify script (logic — adapt to the shell/stack)

```
if env AGENT_SKIP_VERIFY == "1": print skip; exit 0
if host OS cannot run the suite: print skip; exit 0          # OS-aware no-op
cd repo root
changed = git status --porcelain matching the stack's source extensions
if changed is empty: print "no source changes -> skip"; exit 0   # conditional/fast
run <BUILD>; if failed: stderr "verify FAILED: build"; exit 2
run <TEST>;  if failed: stderr "verify FAILED: tests";  exit 2
print "verify: green"; exit 0
```

### guard script (logic)

```
read hook JSON from stdin; cmd = .tool_input.command
if cmd empty or unparseable: exit 0          # fail-open
for pattern in [ rm -rf, > .env, >> .env, git push --force, -f force-push, --no-verify ]:
    if cmd matches pattern: stderr "guard: BLOCKED (<pattern>)"; exit 2
exit 0
```

### `.claude/agents/code-reviewer.md`

```markdown
---
name: code-reviewer
description: Use proactively after any code change to review the diff against the repo's rules and invariants before finishing. Read-only QA with a fresh context.
tools: Read, Grep, Glob, Bash
model: inherit
---

You are a read-only QA reviewer. Never edit, commit, or push.

1. Run `git diff` and `git diff --staged`; read only the changed files + their
   immediate dependencies.
2. Read the repo's rules (e.g. `project_rules.md`, `CONTRIBUTING.md`, `CLAUDE.md`)
   and any domain doc relevant to the touched files.
3. Check the project's hard invariants (security/secrets, public API/contracts,
   tests cover new behavior, build/test green).
4. Verdict: APPROVE or REQUEST CHANGES. List Critical issues (block) then Minor.
   Cite file:line. Be concise.
```

## Discipline guardrails (do / don't)

- **Do** keep the verify gate fast and conditional. A 6-second build on every stop,
  including docs-only turns, trains the user to disable it.
- **Do** explain in one line why each hook exists when you present them.
- **Don't** generate a "codebase overview" doc or per-file docs — the agent reads
  the code faster than it reads paraphrases of it, and stale docs mislead.
- **Don't** bloat `CLAUDE.md`/`AGENTS.md`. If it exists, leave it lean; if you must
  add to it, add only load-bearing rules and a pointer to build/test commands.
- **Don't** set `TreatWarningsAsErrors` or aggressive analyzer severities as part of
  scaffolding — that can break an existing build. Keep `.editorconfig` advisory.
- **Don't** force-centralize per-project build settings; only touch what is safe.

## Done means

The verify script returns 0 on green and 2 on a forced failure, the guard blocks a
sample dangerous command, the repo's own build+test still pass, and you've told the
user to start a fresh Claude Code session to activate the committed hooks.
