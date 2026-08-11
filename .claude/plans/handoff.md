# Session Handoff — 2026-07-29

> **What this is.** A dated, *verified* snapshot for the next agent: what is true on
> `main` right now, what will bite you first, and what is ready to execute.
>
> **Expiry rule.** Every fact below was measured, not copied from other docs. If
> `git log --oneline -1` no longer shows `e265d72`, treat this file as a lead, not a
> source of truth, and re-measure with §2. Canonical backlogs stay
> `.claude/plans/current.md` (features) and `.claude/plans/improvement-plan.md`
> (engineering health); this file does not replace them.

## 1. Verified state (measured 2026-07-29)

| Fact | Value | How it was measured |
|---|---|---|
| `main` HEAD | `e265d72` | `git log --oneline -1` |
| Sync | `main` == `origin/main`, working tree clean | `git status -sb` |
| Release tag | **`v1.0.0` exists** — the 1.0 gate is CLOSED | `git tag -l` |
| Tests | **389 passed · 3 skipped · 392 total · 0 failed** (net8.0) | `dotnet test src/AgentRunner.Tests/…` |
| The 3 skips | `LoginE2ETests`, `ProtectedActionE2ETests`, `UiaSessionRecorderE2ETests` — gated on `RUN_E2E_UI=1`, by design | test output |
| Test attributes | 320 `[Fact]` + 13 `[Theory]` + 3 gated | `grep -rho '\[Fact\]\|\[Theory\]' src` |

**Do not hand-copy the test number into prose.** That habit is the root of the drift in
§4/H2 — quote the command, not the count.

## 2. Re-verify in 60 seconds

```bash
git -C . log --oneline -1 && git status -sb && git tag -l
```

```bash
dotnet test ./src/AgentRunner.Tests/AgentRunner.Tests.csproj -v minimal
```

## 3. ⚠️ Read this before running anything

**`dotnet test ./DesktopAiTestAgent.sln` — the command `CLAUDE.md` tells you to run —
fails at restore on this machine.**

```text
Sample.MauiApp.csproj : error NU1015: PackageReference items without a version:
Microsoft.Maui.Controls, Microsoft.Maui.Controls.Compatibility
```

**Cause (diagnosed, not guessed).** `Sample.MauiApp` opts out of Central Package
Management and pins `Version="$(MauiVersion)"`, expecting the workload to supply it.
The installed workload is `maui-windows 10.0.0/10.0.100` (a **.NET 10** manifest) while
the project targets `net8.0-windows10.0.19041.0`, so `$(MauiVersion)` resolves **empty**
→ NU1015.

**Workaround.** Test the test project directly (§2). It builds `AgentRunner` +
`AgentRunner.Tests` and covers the whole suite — the MAUI sample is not needed.

**Worth fixing properly** (it blocks the documented validation path and any solution-wide
CI leg): either install the net8-era `maui-windows` manifest, or give `$(MauiVersion)` an
explicit fallback in the sample's csproj. Not yet logged in `DISCOVERY_LOG.md` — log it
if you touch it.

## 4. Ready to execute now (no desktop required), ranked

### H1 — Fix the confirmed selector-drift dedup bug ★ start here
**Status:** `OPEN` in `.claude/DISCOVERY_LOG.md` (2026-07-02, flagged by Lot B QA,
explicitly "NOT introduced by the P3 work" — pre-existing, still unfixed).

`src/AgentRunner/RunAnalytics.cs:107`:

```csharp
var oldT = heal.OldTarget ?? "";
var newT = heal.NewTarget ?? "";
var key = oldT + "" + newT;      // ← empty separator
```

**Defect.** The key is a bare concatenation, so `("a" → "bc")` and `("ab" → "c")` both
key to `"abc"` and collapse into **one** `SelectorDriftGroup`. `Count` is inflated and
`MaxConfidence` is taken across two unrelated drifts — `--analytics` under-reports the
number of distinct drifts and mis-attributes confidence.

**Fix.** Use a separator that cannot occur in a UIA identifier:

```csharp
const char Sep = (char)1;                 // U+0001 — cannot occur in a UIA id
var key = oldT + Sep + newT;
```

A length prefix works equally well and stays fully printable:
`var key = oldT.Length + ":" + oldT + newT;`.

**Acceptance.** A test feeding two runs with `("a","bc")` and `("ab","c")` asserts
**two** groups of `Count == 1` (today: one group of `Count == 2`). Put it in
`src/AgentRunner.Tests/RunAnalyticsTests.cs`. Small, in-domain (runner), zero contract
impact.

### H2 — Reconcile `docs/status.md` (improvement-plan P0-2, still open)
Two provably wrong lines:
- `docs/status.md:11` — "~167 unit + 2 gated UI E2E theories (= 6 cases…)". Real: **389
  passing, 3 gated**. Off by ~2×.
- `docs/status.md:13` — "**Branch**: `claude/runner-orchestrator` (pushed); open the PR
  from the GitHub link." That branch is long merged; `main` is at `v1.0.0`.

Also refresh **Shipped** (missing: `--replay`, `--heal-apply`, MCP, `--retry-once`,
dHash visual diff, baseline triage) and **Remaining** (it still lists V3/V8/V9.5/V11 as
open — all shipped). Prefer linking the §2 command over quoting a number.

### H3 — Close the stale `DISCOVERY_LOG` entry
`.claude/DISCOVERY_LOG.md`, entry *2026-06-05 · claude/runner-heal-evidence*, still reads
`**Status**: OPEN — --heal-apply deferred`. It **shipped** (PR #31, `ba0868e`, "V8 inc.2
complete") and `--heal-apply` is live in the CLI. Mark `CLOSED - shipped PR #31`.
The other two OPEN entries are genuinely open (the H1 dedup bug; the global-tool /
CLI-driver split).

### H4 — God-objects (improvement-plan P2-1, unchanged)
`Program.cs` **926** lines (~18 command handlers + hand-rolled arg helpers),
`Dashboard/DashboardApi.cs` **756**, `Dashboard/DashboardHtml.cs` **698** (large inline
HTML). Safe to decompose behind the frozen `CONTRACT.md` — `ContractTests` is the net.
Do it as its own increment; don't mix with H1–H3.

## 5. Environment-bound (needs a real Windows desktop / infra)

- **P1-2 MAUI gated E2E** — the example YAML landed in PR #36; the wiring
  (exe-locate for the win10 RID path, packaged vs unpackaged launch) is still missing.
  Note §3 first: the MAUI sample doesn't currently restore here.
- **P1-1 self-hosted Windows runner** — the UIA path that *is* the product still has
  **zero CI protection**; the 3 gated tests only run when a human sets `RUN_E2E_UI=1`.
- **P1-4 secret-leak regression test** — assert a recorded secret appears in **no**
  artifact (`session.json`, screenshots, `report.json`). Today redaction is best-effort.
- **P1-5 "Click not captured"** — `--record` may miss clicks (UIA `Invoked` not raised).
- **P4-1 `RunDiffer`** — run-to-run regressions / `fixed_now` diff; named in `current.md`
  as the top transferable RIG-TV idea.

## 6. Already done — do not re-plan these

`v1.0.0` **tagged** · `schema_version` + artifact `version` **shipped** (`46a148f`) ·
GitHub **Pages live** (https://vince427.github.io/dotnet-ai-test-agent/) · real-app case
studies **KeePass** + **GerberViewer (native WinForms .NET 4.8)** · Symphony→AgentLoop
rename · RIG-TV **Lot A** (atomic artifact writes, stale-binary guard) / **Lot B** (dHash
visual diff, `--retry-once`→`Flaky`, `baseline.json` triage) / **Lot C** (process-tree
teardown) · UIA driver hardening (GDI+ screenshot fallback, click fallbacks,
multi-window child resolution).

`improvement-plan.md` items **P0-1, P0-3, P2-3 are closed**; its header still says
"scan of `main` = 5c6c10a" — that scan is two waves old. §1 above supersedes it.

## 7. Guardrails (from `CLAUDE.md` / `project_rules.md`)

- **Portable-first · manual-first · AI-optional · non-intrusive.** Never require target
  apps to change. net48 **and** net8 both stay healthy.
- Deterministic key-free core is golden/contract-tested in CI; **stochastic LLM runs are
  recorded, never asserted-equal**. YAML rewrites go through `TestFactGuard`. One YAML
  emitter (`DashboardApi.BuildYaml`).
- **Post-1.0 SemVer is now binding**: `1.x` is additive-only; anything contract-breaking
  waits for `2.0` and needs a `WARN` deprecation for ≥1 minor. See `CONTRACT.md`.
- Branch per increment (`claude/<domain>-<slug>`), QA via the `code-reviewer` subagent,
  **a human merges**. Do not commit/push unless asked.
- Read only the matching `.claude/context/<domain>.md`; cross-domain surprises go to
  `.claude/DISCOVERY_LOG.md`.

## 8. Suggested first move

H1 → H3 → H2 in one small branch (`claude/analytics-dedup-and-drift`): one real bug fix
with a regression test, plus the two truth-of-docs corrections it lets you make honestly.
That leaves the repo self-consistent for whoever picks up the desktop-bound work in §5.
</content>
