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

## Tools (this increment: read-only, key-free)

| Tool | Args | Returns |
|---|---|---|
| `list_tests` | — | Catalog from `tests/` (id, title, framework, priority, category, suite, tags). |
| `validate_plan` | `path?` (repo-relative) | Validation result for one plan, or all discovered plans. |
| `list_runs` | — | Run summaries from `runs/` (runId, testId, result, score, timing, steps). |
| `get_run` | `runId` | One run's full `report.json` (path-guarded). |

Tools that spawn a run or need a provider key are intentionally **not** exposed yet — runtime
execution stays on the explicit CLI (`--plan`/`--test-id`), so a host can't trigger a desktop run
implicitly. (A future increment may add an opt-in `run_test` tool.)

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
