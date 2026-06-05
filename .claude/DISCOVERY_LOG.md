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

## 2026-06-05 - claude/capture-foreground - automation

**Observation**: Found by the first real third-party-app test (the OSS `dotnet-winforms-examples`
gallery, driven via `--vision-bridge`). `FlaUiDesktopDriver.CaptureScreenshot` used
`Capture.Element(_window)`, which grabs **screen pixels at the window's bounds** — so when the target
window was **occluded** (another window in front), the screenshot was of the *occluding* window even
though the UIA tree (element bounds/ids) was read correctly. The vision path then "saw" the wrong
window. Our self-authored sample E2E never caught this because the sample was always foreground.

**Why it matters**: vision (and screenshot evidence) is wrong whenever the target isn't on top — a
common real-world condition. Exactly the class of bug only a real-app/real-setup run surfaces.

**Fix applied (this branch)**: `CaptureScreenshot` now calls `_window.SetForeground()` (+ a 150 ms
settle) before capturing, best-effort (never fails the shot). Re-verified live: after the fix the
capture shows the app, and a vision-driven click correctly opened the "Slider Puzzle" window.

**Status**: `CLOSED - fixed`. Follow-up idea: prefer a window-handle/PrintWindow capture that works
even when occluded (no foregrounding needed), if foregrounding ever becomes undesirable.


## 2026-06-05 - claude/dist-tool - ci / runner

**Observation**: A `dotnet tool install -g` global tool can't be produced for AgentRunner. `PackAsTool`
rejects a `net8.0-windows` TFM and `UseWPF`/`UseWindowsForms` on .NET 5+ (SDK `NETSDK1146`), and the
runner references FlaUI + WinForms/WPF (Windows desktop) transitively via `UIAutomation`. So the tool
would have to target plain `net8.0`, which the desktop driver can't.

**Why it matters**: the "one-line install" P2 item can't be a global tool as written. Distribution
pivoted to a published Windows exe (`scripts/publish-release.ps1` → single-file `AgentRunner.exe`,
`docs/install.md`). Fixed in passing: `RunJobManager` used `Assembly.Location` (empty under single-file)
to find the runner to spawn — now falls back to the host process path.

**Suggestion**: a `dotnet tool` only becomes possible if the **manual/key-free CLI** (validate, list,
render, compose-recording, mcp, show-prompt) is split into a `net8.0` core assembly with the FlaUI
desktop driver loaded as an optional/plugin dependency. Consider as a pre-1.0 architecture decision
(it would also sharpen the CONTRACT.md surface). Until then: ship the exe.

**Status**: `OPEN` — exe distribution shipped; global-tool deferred behind a CLI/driver split.

## 2026-06-05 - claude/runner-heal-evidence - runner / workflow

**Observation**: The plan item "V8 inc.2 `--heal-apply` (confirmed YAML rewrite)" assumes the
test YAML carries the selector that drifted, but `TestDefinition` has **no selector field** — the
agent/LLM picks `AutomationId`s at runtime from the live snapshot; YAML only holds goal /
allowed_actions / success_condition / target_window. So a `HealingSuggestion` (oldTarget→newTarget)
has **nothing to rewrite in YAML** today. This session shipped the other half of inc.2 (screenshot
in heal evidence) instead.

**Why it matters**: `--heal-apply` as specified can't land until tests can persist concrete
selectors — i.e. **recording mode (V9.5)**, which would author selector-bearing YAML.

**Suggestion**: defer `--heal-apply` to after V9.5 (recording mode). When it exists, `--heal-apply
--run <id>` can replace the recorded selector in the generated YAML, gated local-only + confirmed.

**Status**: `OPEN` — `--heal-apply` deferred; screenshot-in-heal-evidence done on this branch.

## 2026-06-03 - claude/runner-orchestrator - automation / security

**Observation**: Screenshot secret-masking (V3-A) and the text `SecretRedactor` both key
off the element's *identifier* (`AutomationId`/`Name` matching password/secret/token/…).
A field whose secret lives only in `Value`, or a true password box with a non-sensitive
id, would be left unmasked. QA flagged this as a "false sense of safety" risk (non-blocking;
mirrors existing text-redaction behavior, documented as best-effort).

**Why it matters**: an edge-case secret could appear unmasked in a screenshot artifact.

**Suggestion**: follow-up — also mask UIA controls reporting `IsPassword`/`ControlType=Edit`
with the password style, independent of the identifier. Needs a new `UiElement.IsPassword`
populated by the driver. Small, deferred.

**Status**: `CLOSED - done`. `UiElement.IsPassword` is populated by `FlaUiDesktopDriver` from
the UIA password property and OR'd into `SecretRedactor.IsSensitiveElement` +
`ScreenshotRedaction`, so password controls are redacted/masked regardless of identifier.

## 2026-06-04 - main - dashboard / security (global audit)

**Observation**: the dashboard's `HttpListener` POST routes had no Origin/Host check, so any
web page the developer visited while `--dashboard` ran could cross-origin `POST /api/runs`
or `/api/tickets/run` and spawn processes (simple-request CSRF).

**Why it matters**: a localhost dev tool that launches processes is CSRF-triggerable.

**Suggestion**: same-origin allow-list on POST.

**Status**: `CLOSED - done`. `DashboardServer.IsSameOriginPost` requires Origin == the
dashboard URL (or a loopback Host when no Origin); cross-origin POSTs get 403. + 3 tests.

## 2026-06-02 - claude/runner-orchestrator - automation

**Observation**: `FlaUiDesktopDriver.Capture()` derives `UiSnapshot.StatusText` by
returning the **first** element whose AutomationId contains "status". The WinForms
sample has three (`lblStatus`, `lblProfileStatus`, `lblControlsStatus`), so any flow
whose outcome lands in a non-login status region can never be detected via
`success_condition` (the login label "Waiting" always wins). Surfaced while adding
the complex gated-action E2E (`DEMO-PROTECTED-001`), which therefore verifies via an
explicit `Assert` on `lblControlsStatus` instead of a `success_condition`.

**Why it matters**: as target apps get richer (multiple status/result regions —
exactly the direction the samples are heading), the "first status label" heuristic
silently mis-detects success. `success_condition` looks broken for non-login flows
even though the app is fine.

**Suggestion**: keep `Assert` as the robust explicit pattern for multi-region apps
(documented in `tests/examples/demo/protected-action.yaml`). Longer term, consider a
smarter status resolution (e.g. the status nearest the last-acted control, or letting
YAML name the status element) — an automation-domain change; weigh against keeping the
heuristic simple. Not urgent; the Assert path already covers it.

**Status**: `CLOSED - done (A6)`. `UiSnapshot.StatusContains` scans **every** status region
(not just the first label `FindStatusText` returns), and both success-condition checks
(`RunOrchestrator` early-success + `ActionExecutor.Done`) now use it — so a success condition
landing in a non-first status region (e.g. `lblControlsStatus` vs. `lblStatus`) is detected.
`FindStatusText` still returns the first status region (now skipping empty labels — a
logging-only refinement; success checks no longer depend on it). +5 tests (`UiSnapshotStatusTests`). The
explicit `Assert` pattern stays valid and is still the recommendation when an app reuses one
status string across regions.

## 2026-06-01 - claude/workbench-interactive - workbench

**Observation**: `SymphonyWorkbenchGenerator.LoadRuns` deserialized `report.json`
without a `JsonStringEnumConverter`, but `ArtifactWriter` serializes enums as
strings (`"evidenceLevel": "Standard"`). Every real run threw on deserialize and
was silently swallowed by the catch -> `runs=0`. The existing test used a
`report.json` with no `evidenceLevel` field, so it never caught this.

**Why it matters**: the workbench showed zero runs for any real or guard-demo
run; the dashboard looked empty even when artifacts existed.

**Suggestion**: FIXED on `claude/workbench-interactive` (added the converter +
regression test). Audit any other `JsonSerializer.Deserialize<RunArtifact>` /
report-reading sites for the same mismatch.

**Status**: `CLOSED - see branch claude/workbench-interactive`

## 2026-06-01 - claude/session-handoff - ci / observability

**Observation**: For Phase 2 (OpenTelemetry -> Aspire dashboard), the runner
multi-targets net48. gRPC OTLP is unsupported on .NET Framework since the OTLP
exporter `1.12.0` (Grpc.Core removed). Must use `OtlpExportProtocol.HttpProtobuf`
(OTLP HTTP, port 4318), never gRPC (4317), or the net48 build emits nothing.

**Why it matters**: picking the default/gRPC exporter would silently produce no
telemetry on the .NET Framework target.

**Suggestion**: configure `HttpProtobuf` when instrumenting; run the standalone
Aspire dashboard via `aspire dashboard run --allow-anonymous` (Aspire 13.3+,
NativeAOT, no Docker) or the `mcr.microsoft.com/dotnet/aspire-dashboard` image.

**Status**: `CLOSED - addressed in OBS-1`. `RunnerTelemetry.TryStartExport` forces
`OtlpExportProtocol.HttpProtobuf` for both targets; exporter pinned to 1.15.3
(1.12.0 had advisory GHSA-4625-4j76-fww9). Live dashboard view stays a manual step.

## Archive

_Empty._

