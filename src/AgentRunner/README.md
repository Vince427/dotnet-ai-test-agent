# AgentRunner

Contains the observe -> decide -> act -> guard -> score -> record loop.

Manual-only modes such as `--validate-plan`, `--list-tests`, `--render-ui`, and
`--write-guard-demos` must not load `.env`, call an LLM, or require a live
desktop app.
