# Improvement Plan — Quality, Credibility & 1.0 Readiness

> Companion to `docs/roadmap.md` (strategy) and `.claude/plans/current.md` (feature
> backlog). **This file is the engineering-health backlog**: not new features, but the
> debt, drift, and proof gaps that stand between "capability-complete" and "credible 1.0".
> Keep it honest — every item below is grounded in a real observation, with an acceptance
> test, so "done" is checkable.

## How this was produced

Grounded scan on **2026-06-17** of `main` = `5c6c10a`:
- source tree + file sizes (`find`/`wc`), `[Fact]/[Theory]` attribute count (~309),
- `docs/release-checklist.md`, `.claude/DISCOVERY_LOG.md`, `docs/status.md`,
  `.claude/plans/current.md`, `CLAUDE.md`,
- prod-code marker scan (0 `TODO`/`FIXME`/`HACK` in `src/**` outside generated MAUI).

## Executive read

The engine is **capability-complete and unusually disciplined** (no in-code debt
markers; contract + golden + reproducibility + fact-gate tests; multi-target net48 /
net8 / Avalonia / MAUI; key-free deterministic core). The gap to a defensible 1.0 is
**not features**. It is four things, in order of leverage:

1. **One credible real-app proof** (the moat is asserted, not demonstrated on an app we don't own).
2. **Truth-of-docs drift** — three status sources disagree; for an "evidence-first" tool, wrong self-reporting is a credibility bug.
3. **The last 1.0 box** — the release gate is one checkbox (the tag) from green, but a contradiction must be reconciled first.
4. **A few structural/robustness debts** — the desktop-driving path has no CI protection; `Program.cs` is a 922-line god-object; secret-safety is best-effort; distribution can't be a global tool.

---

## P0 — Credibility & truth (do these first; low effort, high signal)

### P0-1 — Real-app end-to-end case study (the moat, demonstrated)
**Problem.** Every capability (record → replay → drift → heal, vision fallback,
secret-safety, non-intrusive) is proven on *our own* sample apps or asserted in docs.
The single most convincing artifact — the whole value prop run once on an *uncontrolled*
app the agent never saw — does not exist. This is the highest-leverage gap (already the
headline item in `current.md`).
**Action.** Pick one real OSS Windows desktop app (ideally native .NET Framework 4.8 LOB,
which also closes `current.md` item 3). Run, in one session, and capture artifacts as a
committed reference case study under `docs/case-studies/<app>/`:
1. `--record` a real flow (login + form + submit),
2. `--replay` it deterministically (no LLM),
3. induce selector drift (rename/move a control or use a newer app build) → replay fails,
4. `SelectorHealer` suggests → `--heal-apply` fixes → replay passes again,
5. on the same app: vision (`--vision` / `--vision-bridge`) on a flat-UIA/owner-drawn
   control, prove non-intrusive (zero app changes), prove secret-safety (login secret
   never in `session.json`/screenshots; re-injected via `AGENTLOOP_SECRET_*`).
**Needs.** User picks the app + a real Windows desktop (env-bound).
**Acceptance.** `docs/case-studies/<app>/README.md` + the run artifacts committed; a
reader can follow the drift→heal→pass arc from the evidence alone, no model required.
**Candidate apps to evaluate (P0-1a, can start now without a desktop):** shortlist 3–4
native-.NET-Framework OSS apps with a login/form/submit flow (e.g. KeePass 2.x — master
password = a real secret field, add-entry dialog = form; Greenshot; an older net4.x
Git Extensions / ShareX release). Verify each is *natively* net4.x (not a retarget — the
DISCOVERY_LOG warns retargets hit API walls) and runnable from a released binary
(non-intrusive, no build needed). Output: a one-page comparison + a recommendation.

### P0-2 — Reconcile the documentation drift (single source of truth)
**Problem.** Self-reported status is inconsistent across files:
- `docs/status.md`: "~167 tests + 2 gated E2E (6 cases, WinForms/WPF/Avalonia)" and
  "Branch: `claude/runner-orchestrator` (pushed); open the PR" — both stale.
- `.claude/plans/current.md` snapshot: "338 tests + 3 gated", "main HEAD = 248d8e0".
- Reality: `main` = `5c6c10a`; ~309 `[Fact]/[Theory]` attributes (theories expand to
  more cases).
- `.claude/DISCOVERY_LOG.md` (2026-06-05) still marks `--heal-apply` **OPEN/deferred**,
  but it shipped (PR #31, `ba0868e`, "V8 inc.2 complete").
For an "evidence-first" product, wrong self-reporting is itself a defect.
**Action.**
- Make the test count *derived*, not hand-typed: a tiny script (or a line in
  `validate-test-plans`/CI summary) prints the real `dotnet test` total; docs link to it
  instead of quoting a frozen number. (Or just stop quoting an exact number in prose.)
- Refresh `docs/status.md` (Health, Branch line, Shipped list incl. heal/replay/MCP).
- Close the stale `DISCOVERY_LOG` `--heal-apply` entry (`CLOSED - shipped PR #31`).
- Refresh the `current.md` RESUME SNAPSHOT HEAD pointer (done in this change).
**Acceptance.** No two docs disagree on test count, HEAD, or feature status; CI surfaces
the live test total.

### P0-3 — Cut `v1.0.0` (after reconciling the gate contradiction)
**Problem.** `docs/release-checklist.md` has every "Definition of 1.0" box checked
**except the tag** — including `schema_version` + tolerant loader + SemVer policy +
CHANGELOG 1.0.0. But `current.md` says those are "**still open for the tag**". One of
the two is wrong.
**Action.** Verify on `main` which is true: does YAML carry `schema_version`, do
artifacts carry `version`, does the loader tolerate unknown fields (there should be a
test)? Then either (a) tick the boxes and tag, or (b) implement the genuinely-missing
piece, then tag. Release workflow (`release.yml`, tag `v*` → Release) already exists.
**Acceptance.** `git tag v1.0.0` pushed → GitHub Release built by `release.yml`;
`current.md` and `release-checklist.md` agree.

---

## P1 — Robustness & real coverage

### P1-1 — Put the desktop-driving path under automated regression
**Problem.** The only proof that the agent *actually drives a Windows app via UIA*
(`LoginE2ETests`, `ProtectedActionE2ETests`, `UiaSessionRecorderE2ETests`) is gated
behind `RUN_E2E_UI=1`, run by hand on an interactive desktop, and has already faulted
(`RPC_E_SERVERFAULT`, env). The product's core behavior has **zero CI protection**;
everything green in CI is the key-free deterministic layer around it.
**Action.** Stand up a **self-hosted Windows runner** (or a scheduled interactive job)
that runs the `RUN_E2E_UI=1` suite on a logged-in session, on a cadence (nightly or
per-release), and publishes artifacts. Keep it *out of* the PR-blocking path (flaky env)
but make failures visible.
**Acceptance.** A scheduled green/red signal for the gated E2E suite, with artifacts,
independent of a human remembering to run it.

### P1-2 — Wire the MAUI gated E2E
**Problem.** WinForms/WPF/Avalonia run in the gated theories; MAUI is samples-only
("E2E wiring pending"). The MAUI example YAML now exists (merged PR #36), so the content
half is done.
**Action.** Add the MAUI sample to `[InteractiveUiTheory]`: an exe-locate step (win10
RID output path), packaged vs unpackaged launch handling, control-ready settle. Custom
MAUI controls are exactly where vision pays off — good vision-fallback coverage too.
**Acceptance.** MAUI is a row in the gated E2E theory and passes under `RUN_E2E_UI=1`.

### P1-3 — Occlusion-proof screenshot capture (and re-honor "non-intrusive")
**Problem.** `FlaUiDesktopDriver.CaptureScreenshot` was fixed for occluded windows by
calling `SetForeground()` before the shot (DISCOVERY_LOG 2026-06-05, CLOSED). But
foregrounding **steals focus** — intrusive in a multi-window real environment, mild
tension with the "non-intrusive" product rule, and racy. The robust fix is noted as a
follow-up.
**Action.** Capture via window-handle `PrintWindow`/DWM thumbnail so a correct shot is
produced even when occluded, without foregrounding. Fall back to the current path if it
fails.
**Acceptance.** A test/demo: target window occluded → screenshot still shows the target,
focus not stolen.

### P1-4 — Secret-safety: deny-by-default in record/replay + a leak-assert test
**Problem.** Redaction/masking is best-effort and keys off the element *identifier*
(plus `IsPassword`). The DISCOVERY_LOG (2026-06-03) notes a field whose secret lives only
in `Value` with a non-sensitive id could be left unmasked. For a tool that **records real
logins**, an unmasked secret in `session.json`/a screenshot is the highest-severity class.
**Action.** In record mode, treat `Edit`/text-input control values as **secret by
default** in persisted artifacts unless explicitly allow-listed (or invert: never persist
raw input values, only references). Add a regression test that records a flow containing a
known secret and asserts the secret string appears in **no** artifact (`session.json`,
screenshots' masked regions, `report.json`).
**Acceptance.** "Secret never lands in any artifact" is a green, always-run test — not a
documented "best-effort" caveat.

### P1-5 — Recording: "Click not captured" investigation
**Problem.** Live finding (DISCOVERY_LOG / current.md item 4): the driver's `Click` may
not raise the UIA `Invoked` event, so `--record` can miss clicks — directly undermining
V9.5 recording, a P1 top-of-funnel lever.
**Action.** Reproduce, identify why `Invoked` isn't raised (control type? Click via
mouse-synth vs invoke pattern?), and capture clicks robustly (e.g. also hook
`MouseDown`/structure-change or synthesize an explicit recorded action on click dispatch).
**Acceptance.** A recorded session of a click-heavy flow contains every click; covered by
the gated recorder E2E.

---

## P2 — Structure & distribution

### P2-1 — Decompose the `Program.cs` god-object (922 lines)
**Problem.** `Program.Main` dispatches ~18 command handlers (`ShowPrompt`,
`ComposeRecording`, `Analytics`, `HealApply`, `RecordSession`, `RunMcp`, `RunDashboard`,
`RunBridgeLlm`, `WriteGuardDemos`, `ToJUnit`, `ValidatePlans`, `ListTests`, …) with
hand-rolled arg helpers (`HasArgument`/`HasOptionValue`) alongside `RunnerOptions`. It's
the biggest prod file and a change-magnet. `DashboardApi.cs` (756) and `DashboardHtml.cs`
(698, a large inline HTML blob) are next.
**Action.** Extract a small command-dispatch seam (`ICommand` / `name → handler` map) and
move each handler to its own class; consolidate arg parsing into `RunnerOptions`. Keep the
**public CLI contract byte-identical** — `ContractTests` is the safety net. Consider
moving the dashboard HTML to an embedded resource file (testable, lintable).
**Acceptance.** `Program.cs` is parse + dispatch only; per-command handlers are unit-
testable; `ContractTests`/golden tests unchanged and green.

### P2-2 — net8 key-free core / desktop-driver split (unblocks the global tool)
**Problem.** No `dotnet tool install -g` is possible: the desktop driver forces a
`net8.0-windows` TFM + `UseWPF`/`UseWindowsForms`, which `PackAsTool` rejects
(DISCOVERY_LOG 2026-06-05, OPEN). Distribution is a published exe instead.
**Action.** Split the manual/key-free CLI (validate, list, render, compose-recording,
mcp, show-prompt) into a plain `net8.0` core assembly, with the FlaUI desktop driver as an
optional/plugin dependency loaded only when a runtime desktop run is requested. This both
enables a global tool **and** sharpens the `CONTRACT.md` core/driver boundary.
**Acceptance.** `dotnet tool install -g …` works for the key-free surface; desktop runs
still work via the optional driver; CONTRACT documents the split. (Pre-1.0 architecture
decision — record it in `docs/architecture-decisions.md`.)

### P2-3 — GitHub Pages live demo (pending human action)
**Problem.** Pages still fails at `Configure Pages`; the `enablement:true` auto-enable
didn't take. Public docs/demo aren't live.
**Action (human).** Settings → Pages → Source: **GitHub Actions**. Then confirm the docs
site + (optionally) link the P0-1 case-study artifacts from it.
**Acceptance.** Pages builds green; docs reachable; case study linked.

---

## Suggested sequencing

| Wave | Items | Why this order |
|---|---|---|
| **A (now, no desktop)** | P0-2 (doc reconcile), P0-3 (verify gate + tag), P0-1a (app shortlist) | Pure-repo, high signal, unblock the tag and the case-study choice. |
| **B (needs a desktop)** | P0-1 (case study), P1-2 (MAUI E2E), P1-5 (click capture) | The credibility proof + the env-bound coverage, once an app + desktop are chosen. |
| **C (infra)** | P1-1 (self-hosted E2E runner), P2-3 (Pages) | Make the env-bound proof repeatable and public. |
| **D (debt)** | P1-3 (occlusion capture), P1-4 (secret-safety test), P2-1 (Program.cs), P2-2 (core/driver split) | Robustness + maintainability; safe behind the frozen CONTRACT. |

## Guardrails (unchanged product rules these items must respect)

- Portable-first, manual-first, **AI-optional**, **non-intrusive** (P1-3 explicitly
  re-honors this), YAML/artifacts are source of truth, net48 + net8 both healthy.
- Deterministic key-free core stays gated/golden in CI; **stochastic LLM runs are
  recorded, never asserted-equal**. Rewrites go through `TestFactGuard`. One YAML emitter
  (`DashboardApi.BuildYaml`). Any contract change → SemVer bump + CHANGELOG migration note.
</content>
</invoke>
