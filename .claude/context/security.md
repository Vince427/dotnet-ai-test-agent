# Context: Security, Secrets, And Providers

Owns provider configuration safety and secrets hygiene.

## Files

- `.gitignore`
- `.env.template`
- `WORKFLOW.md` provider settings
- `src/AgentRunner/LlmService.cs`
- `src/AgentRunner/WorkflowConfig.cs`
- scripts or docs that mention secrets or OpenRouter

## Invariants

- Never read, print, stage, commit, or push the real `.env`.
- `.env` and `.env.*` stay ignored, while `.env.template` stays tracked.
- `.env.template` contains placeholders only.
- OpenRouter is configured through the real local `.env`:
  `LLM_ENDPOINT`, `LLM_API_KEY`, `LLM_MODEL`.
- Logs may mention variable names, not secret values.
- Secret values are redacted at the source: text via `SecretRedactor`, and **screenshot
  regions of secret fields are masked before the PNG is written** (V3-A). The dashboard's
  `GetFile` also refuses to serve `.env*` (except `.env.template`).
- No hardcoded real API keys, tokens, endpoints with credentials, or personal
  secrets in tracked files.
- Manual commands must work without provider secrets.
- Runtime fallback can target a local OpenAI-compatible proxy, but docs must
  explain that `localhost:4000` is not the workbench or target app.

## Validation

```powershell
git check-ignore -v .env
dotnet test .\DesktopAiTestAgent.sln --no-restore -v minimal
```

## Cross-Domain Notes

- Provider config touches `runner.md`.
- CI secret handling touches `ci.md`.

