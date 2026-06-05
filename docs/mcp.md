# MCP Adapter (`--mcp`)

An optional Model Context Protocol server so an agent host (Claude Desktop, Copilot, other MCP
clients) can use AgentLoop natively. It is **an adapter over the existing CLI contract**, not a
new product surface (per the fixed product rule "MCP/plugins are adapters over CLI commands, not
the core product"): it reuses the same loaders the CLI and dashboard use and adds no new data
model.

## Transport

JSON-RPC 2.0 over **stdio** — start it with:

```powershell
dotnet run --project .\src\AgentRunner\AgentRunner.csproj -f net8.0-windows -- --mcp
```

stdout carries only JSON-RPC; diagnostics go to stderr. No `.env`, no LLM, no target app needed
(the exposed tools are read-only).

## Tools (read-only, key-free)

| Tool | Args | Returns |
|---|---|---|
| `list_tests` | — | Catalog from `tests/` (id, title, framework, priority, category, suite, tags). |
| `validate_plan` | `path?` (repo-relative) | Validation result for one plan, or all discovered plans. |
| `list_runs` | — | Run summaries from `runs/` (runId, testId, result, score, timing, steps). |
| `get_run` | `runId` | One run's full `report.json` (path-guarded). |
| `show_prompt` | `testId`, `path?` | The exact prompt the LLM would receive for a test (key-free preview). |

## Authoring tool (opt-in write — off by default)

The MCP adapter is **read-only by default**. One write tool exists and is enabled only when you
explicitly opt in (so a host can never author files implicitly):

| Tool | Args | Returns |
|---|---|---|
| `create_test` | `id`*, `goal`*, `framework?`, `title?`, `targetWindow?`, `category?`, `successCondition?`, `maxSteps?`, `allowedActions?`, `tags?`, `suite?`, `priority?` | Builds YAML via the same emitter the dashboard uses (`authoring_agent: mcp`), validates it with the CLI validator, and writes `tests/created/<id>.yaml`. Returns `{ ok, id, planPath, warnings }`. On a validation failure or an unsafe id it returns a tool error (nothing is written). |

\* required.

Enable writes with **either**:

```powershell
# CLI flag
dotnet run --project .\src\AgentRunner\AgentRunner.csproj -f net8.0-windows -- --mcp --mcp-allow-write
# or env var
$env:AGENTLOOP_MCP_ALLOW_WRITE = "1"
dotnet run --project .\src\AgentRunner\AgentRunner.csproj -f net8.0-windows -- --mcp
```

When writes are disabled, `create_test` is not advertised in `tools/list` (the read tools still are),
and calling it returns a clear tool error: *"writes are off by default. Enable with `--mcp-allow-write`
or `AGENTLOOP_MCP_ALLOW_WRITE=1`."* The id is guarded with the same safe-segment check the dashboard
uses, so it can never escape `tests/created/`. YAML stays the source of truth — the tool only writes
a plan the CLI validator already accepted.

Tools that spawn a run or need a provider key are intentionally **not** exposed — runtime
execution stays on the explicit CLI (`--plan`/`--test-id`), so a host can't trigger a desktop run
implicitly. (A future increment may add an opt-in `run_test` tool — deferred.)

## Registering with a client

Point your MCP client at the command above. Example (Claude Desktop `mcpServers` shape):

```json
{
  "mcpServers": {
    "agentloop": {
      "command": "dotnet",
      "args": ["run", "--project", "src/AgentRunner/AgentRunner.csproj", "-f", "net8.0-windows", "--", "--mcp"]
    }
  }
}
```

For a packaged build, point `command` at the built `AgentRunner` executable with `--mcp`.

## Quick manual check

```powershell
'{"jsonrpc":"2.0","id":1,"method":"initialize"}' | dotnet run --project .\src\AgentRunner\AgentRunner.csproj -f net8.0-windows -- --mcp
```

You should get one JSON-RPC line back with `serverInfo.name = "agentloop"`.
