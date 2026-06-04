# Context: Runner

Owns the executable orchestration loop and manual CLI surface.

## Files

- `src/AgentRunner/Program.cs` (CLI parse + manual commands; wires the runtime loop)
- `src/AgentRunner/IRunOrchestrator.cs` + `src/AgentRunner/RunOrchestrator.cs`
  (observe → decide → act → score → record loop; injectable driver + decider)
- `src/AgentRunner/IActionDecider.cs` (the "decide" seam). Implementations: `LlmService`
  (OpenRouter/OpenAI), `HeuristicActionDecider` (rule-based, no LLM), the
  `BridgeLlmServer` HTTP endpoint (`--bridge-llm`) for a human/external-agent decider, and
  `VisionActionDecider` (V3 Tier-2: wraps a Tier-1 decider, escalates to a VLM via `IVisionClient`
  + `VisionResponseParser` only when the Tier-1 UIA target is unresolvable). The VLM gets the
  masked, numbered-box overlay + identifiers-only index; the chosen box maps back to an element.
- `src/AgentRunner/ActionExecutor.cs` (the "act" seam: `IActionExecutor` +
  `ActionExecutionResult`). Validates the action (allow-list + target existence) then
  dispatches the verb to the driver; returns the outcome the loop records. Extracted out of
  `RunOrchestrator` so the dispatch is unit-testable in isolation (`ActionExecutorTests`).
- `src/Core/ActionVocabulary.cs` (single source of truth for the action verbs). The dispatch,
  `PromptBuilder`'s default "Allowed actions" line, `TestPlanValidator`, and
  `AgentActionValidator` all derive their verb set from it — they cannot drift apart.
- `src/AgentRunner/RunnerTelemetry.cs` (OBS-1: opt-in OpenTelemetry spans + metrics)
- `src/AgentRunner/RunnerOptions.cs`
- `src/AgentRunner/LlmService.cs`
- `src/AgentRunner/WorkflowConfig.cs`
- `src/AgentRunner/ArtifactWriter.cs`
- `src/AgentRunner/RunArtifact.cs`
- `src/AgentRunner/GuardFailureDemoFactory.cs`
- `src/AgentRunner/ScoringEngine.cs`
- `src/AgentRunner/LoopDetector.cs`
- `src/AgentRunner/QualityGuards.cs`
- `src/AgentRunner/ManualCommandOutput.cs`
- `src/AgentRunner.Tests/**`
- `src/Core/AgentGoal.cs`

## Invariants

- Manual modes `--validate-plan`, `--list-tests`, `--render-ui`, and
  `--write-guard-demos` must not require `.env`, LLM access, FlaUI, or a target
  app.
- `TestPlanLoader.DiscoverPlanPaths` excludes `tests/archived/` everywhere (so
  `--list-tests`/`--suite`, the dashboard catalog, and CI all skip archived tests).
  Archiving a test = moving its YAML under `tests/archived/`; it stays in Git, reversible.
- `--format json` must keep stdout parseable JSON.
- `Done` is not success unless the configured success condition is visible or
  the test has no success condition by design. Visibility is tested with
  `UiSnapshot.StatusContains`, which scans **every** status region (not just the first
  label) so multi-region apps work without explicit `Assert`s.
- Unsupported actions and missing targets must fail visibly, not become no-op
  successes.
- The act dispatch lives in `ActionExecutor`, not inline in the loop. New verbs are added
  to `ActionVocabulary.All` (which feeds plan/prompt/target validation) and given a branch
  in `ActionExecutor.ExecuteAsync` — never by hardcoding the verb string in a fourth place.
- Loop detection records real actions, not synthetic pending markers.
- Runtime artifacts stay human-readable and machine-readable.
- At `full` evidence the run also emits `overlay/step_NNN.png` (numbered element boxes) and
  `overlay/step_NNN.json` (box number → element identifiers) — the V3 Tier-2 artifact contract.
  Built on the masked screenshot; index is identifiers-only (secret-safe). `RunStep` carries
  `OverlayPath`/`OverlayIndexPath`; the summary evidence list shows `overlay`.
- `RunArtifact` carries the YAML's `ExistingTests`/`SourceIssue`/`SourcePr` so
  `--to-junit` can emit them (plus `trace_id`) as `<testcase>` `<property>` links.
  `--to-junit` treats both `Passed` and `Succeeded` as passing (not `<error>`).
- Failure steps should expose stable `failureCode` and `failureMessage` values
  in `report.json` and `summary.md` when the runner can name the failure.
- Thread sleeps in async runtime paths should use `Task.Delay`.
- The runtime loop lives in `RunOrchestrator`, not `Program`. Drive it in tests
  with a fake `IAutomationDriver` + a scripted `IActionDecider` (see
  `RunOrchestratorTests`); pass `interStepDelayMs: 0` to keep tests fast. The
  injected driver is owned by the caller (`Program` disposes the FlaUI one).
- Telemetry (OBS-1) is opt-in and manual-first: `RunnerTelemetry.TryStartExport`
  returns null unless `OTEL_EXPORTER_OTLP_ENDPOINT` is set, and `StartActivity`
  is a no-op with no listener — runs stay dependency-free by default. Span/metric
  tags must stay secret-free (use the redacted action value, never raw `Value`).
  On net48 the OTLP exporter must use `HttpProtobuf` (gRPC unsupported).

## Validation

```powershell
dotnet test .\DesktopAiTestAgent.sln --no-restore -v minimal
dotnet run --project .\src\AgentRunner\AgentRunner.csproj -f net8.0-windows -- --validate-plan --format json
dotnet run --project .\src\AgentRunner\AgentRunner.csproj -f net8.0-windows -- --list-tests --format json
```

## Cross-Domain Notes

- CLI flags that select YAML tests also touch `workflow.md`.
- Artifact shape changes also touch `workbench.md` and docs.
- Provider config or secret logging changes also touch `security.md`.
