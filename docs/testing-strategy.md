# Testing Strategy

Desktop AI Test Agent complements existing test suites. It does not replace
unit tests, integration tests, or CI checks that already belong to the target
application.

## Test Layers

Use these terms consistently:

- **Unit tests**: fast tests for internal code. In this repo, they cover
  redaction, parsing, scoring, validation, artifact serialization, and small
  services.
- **Integration tests**: tests that combine internal components with fakes or
  filesystem artifacts, without requiring a live desktop UI.
- **Business tests**: YAML plans that describe a user-visible workflow in terms
  a reviewer can understand.
- **E2E tests**: business YAML plans executed against a real WinForms, WPF,
  MAUI Windows, or Avalonia app.
- **Characterization tests**: tests that capture current behavior before a
  refactor or migration. They may be YAML/UI tests or code tests.

In this project, `tests/*.yaml` are business plans first. They become E2E tests
when executed against a live target app.

Guard failure demos are deterministic integration artifacts. They use the same
`report.json` and `summary.md` contract as runtime runs, but are generated with
`--write-guard-demos` so QA can review missing target, crash/closed-window,
empty UI tree, and unexpected modal evidence without needing `.env`, a live
LLM, or a desktop session.

## MVP Focus

The MVP should prove this vertical slice:

```text
small YAML business plan -> CLI runner -> artifacts -> AgentLoop Workbench
```

Prioritize:

1. security and redaction
2. stable CLI/YAML/artifact behavior
3. WinForms and WPF TestZoo coverage
4. workbench readability
5. GitHub Pages quickstart and demo artifacts

Defer API-first, Aspire, MCP, plugins, Appium, RunDiffer, recording mode, and
vision fallback until the core desktop contract is stable.

## YAML Organization

Prefer one business scenario per YAML file for new work:

```text
tests/
  smoke.yaml
  testzoo/
    winforms-login.yaml
    winforms-profile-save.yaml
    wpf-login.yaml
    wpf-profile-save.yaml
```

Existing aggregate files such as `tests/testzoo.yaml` can remain while the
suite is small. Split them when review, agent loading, or ownership becomes
awkward.

## TestZoo Priority

Improve controlled internal samples before targeting external legacy apps.

Build richer WinForms and WPF flows first:

- login success and validation error
- checkbox, radio, combo
- list and grid
- modal confirmation
- disabled/enabled state
- async/loading state
- CRUD-like form flow
- guard failure demo artifacts

After those flows are stable, add MAUI Windows and Avalonia parity.

## GitHub Actions

GitHub-hosted Windows runners are appropriate for:

- build
- unit tests
- integration tests
- YAML validation
- listing tests
- workbench rendering

They may run very small UI smoke scenarios, but serious FlaUI runtime suites
should use a self-hosted interactive Windows runner so desktop focus, dialogs,
and screenshots are reliable.

## Running The Loop Without OpenRouter

The "decide" step is behind `IActionDecider` (and the runner speaks the OpenAI
chat-completions contract), so the real loop + driver + app can be exercised with no
provider key in three ways:

- **Scripted mock** (`MockLlmServer`, tests): returns a fixed action sequence —
  deterministic regression E2E.
- **Heuristic decider** (`HeuristicActionDecider`): rule-based, no LLM — fills configured
  inputs and clicks a sequence from the live UI state. Automated → CI smoke without a key.
- **Human/agent bridge** (`--bridge-llm [port]`): an OpenAI-compatible endpoint that writes
  each prompt to `bridge-io/req-N.txt` and waits for a `resp-N.json` action, so a person or
  an external agent (e.g. Claude Code) can be the decider. Point a run's `LLM_ENDPOINT` at
  it. Semi-interactive → exploration/demo, not unattended CI.

Only the production path (`LlmService`) needs OpenRouter or another OpenAI-compatible LLM.

## LLM Provider Options And CI/CD

The runtime LLM is **any OpenAI-compatible endpoint**, selected purely by `LLM_ENDPOINT`
(no code change to switch):

- **Direct LLM** — OpenRouter (`https://openrouter.ai/api/v1`), Anthropic-compatible, etc.
- **API gateway / proxy** — a local OpenAI-compatible proxy such as LiteLLM at
  `http://localhost:4000`, or a local model server.
- **Bridge** — `--bridge-llm` (a person or external agent as the decider, no key).

Full CI/CD does **not** require a paid provider:

- `--validate-plan`, `--list-tests`, `--render-ui`, `--to-junit`, and `--dashboard` need
  no LLM at all.
- Automated agentic UI smoke can run **key-free** with the heuristic decider on an
  interactive Windows runner; the scripted mock covers non-UI regression.
- The **bridge is a local dev tool only** — loopback-only, no auth, semi-interactive. Never
  run it in CI and never expose it beyond `localhost`.

So a complete pipeline (build → validate → list → unit/E2E → JUnit report) is possible at
zero provider cost; a real LLM is opt-in for richer, dynamic exploration.

## Recording Mode

Recording mode should remain visible on the roadmap because it is important for
adoption. It is not part of the MVP.

The intended flow is:

```text
da-test record -> user performs workflow -> YAML draft -> validate -> run
```

Generated YAML must stay editable and reviewable. Sensitive recorded values must
be redacted before persistence or LLM usage.
