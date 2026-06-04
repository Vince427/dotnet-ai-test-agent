# Architecture Diagram

A complete map of the components and how data flows. (Renders on GitHub via Mermaid.)
Naming note: the loop is **AgentLoop** (decision D3). The code symbols were renamed off the
legacy `Symphony*` name; the only retained `symphony` token is the generated `docs/symphony.html`
artifact (a possibly-published Pages URL) and the deliberate "Symphony ticket" model name. All
of it is **our own** code, unrelated to `openai/symphony` (a coding-agent orchestrator).

## End-to-end flow

```mermaid
flowchart TB
    subgraph IN["Inputs (source of truth)"]
        YAML["tests/*.yaml<br/>(TestPlanLoader / Validator)"]
        WF["WORKFLOW.md / .env<br/>(WorkflowConfig)"]
    end

    CLI["Program.Main — CLI"]
    YAML --> CLI
    WF --> CLI

    subgraph MANUAL["Manual commands (no .env / no LLM)"]
        V["--validate-plan"]
        L["--list-tests"]
        R["--render-ui → Workbench HTML"]
        J["--to-junit → JUnit XML (JUnitReportWriter)"]
        GD["--write-guard-demos"]
        DASH["--dashboard → DashboardServer"]
        BR["--bridge-llm → BridgeLlmServer"]
    end
    CLI --> MANUAL

    CLI -->|runtime run| ORCH["RunOrchestrator (IRunOrchestrator)"]

    subgraph LOOP["Agent loop: observe → decide → act → score → record"]
        OBS["observe: driver.Capture() → UiSnapshot"]
        DEC["decide: IActionDecider"]
        ACT["act: dispatch AgentAction"]
        GUARD["guard: QualityGuardEngine"]
        SCORE["score: ScoringEngine + LoopDetector"]
        REC["record: ArtifactWriter + AgentMemory"]
        OBS --> DEC --> ACT --> GUARD --> SCORE --> REC --> OBS
    end
    ORCH --> LOOP

    subgraph DECIDERS["IActionDecider implementations"]
        LLM["LlmService<br/>(any OpenAI-compatible endpoint)"]
        HEUR["HeuristicActionDecider<br/>(rule-based, no LLM)"]
        BRIDGE["BridgeLlmServer<br/>(human/agent, no key)"]
    end
    DEC --> DECIDERS
    LLM -->|HTTP LLM_ENDPOINT| EXT["OpenRouter / local proxy / bridge"]
    BR -.serves.-> BRIDGE

    subgraph DRIVER["IAutomationDriver"]
        FLA["FlaUiDesktopDriver (UIA3)"]
        MASK["ScreenshotMasker + ScreenshotRedaction<br/>(mask secret fields at capture)"]
    end
    OBS --> FLA
    ACT --> FLA
    FLA --> APP["Target app: WinForms / WPF / Avalonia / MAUI"]
    REC --> MASK

    SEC["SecretRedactor<br/>(redact text in prompts/logs/artifacts)"]
    DEC --- SEC
    REC --- SEC

    TEL["RunnerTelemetry<br/>(opt-in OTel spans + metrics)"]
    LOOP -. emits .-> TEL --> OTLP["OTLP → Aspire dashboard"]

    subgraph OUT["Artifacts (runs/<id>/)"]
        REP["report.json (incl. traceId, links)"]
        SUM["summary.md"]
        SHOT["screenshots/ (masked)"]
        TREE["ui-tree/ (Full evidence)"]
    end
    REC --> OUT

    subgraph VIEW["Consumers (read-only over artifacts)"]
        WB["Static Workbench (AgentLoopWorkbenchGenerator)"]
        DB["Dashboard (DashboardServer + DashboardApi)"]
        JU["JUnit XML for CI"]
    end
    OUT --> VIEW
    DASH --> DB
    R --> WB
    J --> JU
```

## Function inventory (by file)

| File | Key functions / responsibility |
|---|---|
| `Program.cs` | `Main` (CLI parse + dispatch), `RunDashboard`, `RunBridgeLlm`, `ToJUnit`, `ValidatePlans`, `ListTests`, `WriteGuardDemos` |
| `RunOrchestrator.cs` | `RunAsync` (run-level span + metrics), `RunCoreAsync` (the loop), `IsActionAllowed` |
| `IActionDecider.cs` | `DecideActionAsync` — the decide seam |
| `LlmService.cs` | `DecideActionAsync` → PromptBuilder + OpenAI client + LlmResponseParser |
| `HeuristicActionDecider.cs` | `DecideActionAsync` — fill inputs / click sequence / Done |
| `BridgeLlmServer.cs` | `Start`/`Handle`/`WaitForReply`/`ExtractPrompt` — file-based decider endpoint |
| `FlaUiDesktopDriver.cs` | `AttachToWindow`, `Capture`, `GetAllElements`, `EnterText`, `Click`, `Scroll`, `DoubleClick`, `ReadText`, `CaptureScreenshot` |
| `ScreenshotMasker.cs` / `ScreenshotRedaction.cs` | `MaskRegions` / `SecretRegions` |
| `SecretRedactor.cs` | `IsSensitiveIdentifier`, `RedactText`, `RedactActionValue`, `RedactSnapshot` |
| `ScoringEngine.cs` / `LoopDetector.cs` / `QualityGuards.cs` | `ScoreAction`/`ShouldAbort`; `RecordAndCheck`; `Check` |
| `AgentMemory.cs` | `RecordScreen`, `AddAction`, `AddFact`, `GetFullContextString` |
| `ArtifactWriter.cs` / `RunArtifactLoader.cs` | `WriteReport`/`WriteSummary`/`SaveScreenshot`/`SaveUiTreeSnapshot`; `LoadFromDirectory` |
| `JUnitReportWriter.cs` | `Write`, `ToTestCase`, `BuildProperties` (existing_test/source_*/trace_id) |
| `RunnerTelemetry.cs` | `Source`/`Meter` instruments, `TryStartExport` (OTLP HttpProtobuf) |
| `AgentLoopWorkbenchGenerator.cs` | `Generate`, `RenderHtml`, `BuildDataIsland`, `InteractiveScript`, `LoadTests`, `LoadRuns` |
| `Dashboard/DashboardServer.cs` | `Start`, `Route`, `Handle` (localhost HTTP) |
| `Dashboard/DashboardApi.cs` | `GetTests`, `GetRuns`, `GetRun`, `GetJobs`, `CreateTest`, `LaunchRun`, `GetScreenshot(List)`, `GetFiles`, `GetFile`, `ResolveUnderRoot` |
| `Dashboard/RunJobManager.cs` | `Launch`, `Snapshot`, runId correlation |
| `TestPlanLoader.cs` / `TestPlanValidator.cs` | `DiscoverPlanPaths`, `Load`, `Parse`; `Validate` |

Everything above is original to this repo and operates on its own models (`RunArtifact`,
`UiSnapshot`, `AgentAction`, `AgentGoal`) and FlaUI/UIA.
