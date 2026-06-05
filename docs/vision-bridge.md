# Vision bridge — be the VLM, no API key

`--vision-bridge <dir>` runs the agent's **vision** loop with **no provider API key**: instead of
calling a hosted multimodal model, each step the runner writes an annotated screenshot + an
identifiers-only index to a folder, then waits for *you* (or an agent like Claude Code) to read the
image and reply with a box choice. It's the vision counterpart of `--bridge-llm`.

This is **env-bound**: it drives a real window via UI Automation, so it needs an interactive Windows
desktop with the target app running.

## What the runner writes (per step), in `<dir>`

- `vision-req-N.png` — the screenshot with **numbered boxes** over the actionable elements. Secret
  fields are **masked** before the file is written.
- `vision-req-N.json` — `{ "box": null, "prompt": "...goal + context...", "index": [ { "n": 1,
  "automationId": "...", "name": "...", "controlType": "...", "isEnabled": true, "isPassword": false,
  "boundingBox": "x,y,w,h" }, ... ] }`. The `index` is a JSON **array**; pick the element's `n` as your
  `box`. Identifiers only — never a control's typed value.

## What you (the VLM) write back

- `vision-resp-N.json` — your decision:
  ```json
  { "box": 1, "actionType": "EnterText", "value": "admin", "reason": "username field", "confidence": 90 }
  ```
  `actionType` ∈ `EnterText | Click | DoubleClick | Scroll | Wait | Assert | Done | Explore`.
  `box` is the number from the index (or `null` for `Wait`/`Done`). The runner maps the box back to
  the element and executes it. On no reply within the timeout it does a safe `Wait`.

## Run it locally (with Claude Code as the VLM)

1. Launch the target app (e.g. the bundled sample):
   ```powershell
   powershell -ExecutionPolicy Bypass -File scripts/run-demo-login.ps1   # or start your own app
   ```
2. Start the agent in vision-bridge mode (no `.env` needed):
   ```powershell
   dotnet run --project src/AgentRunner/AgentRunner.csproj -f net8.0-windows -- `
     --vision-bridge .\vbridge --window "Sample Login App (.NET 8)" `
     --goal "Log in with admin / password123 and confirm success." --max-steps 8
   ```
3. In a **Claude Code session on the same machine**, point it at `.\vbridge` and ask it to act as the
   VLM: for each `vision-req-N.png` + `vision-req-N.json`, read the image, pick the box + action, and
   write `vision-resp-N.json`. The runner advances on each reply and records normal artifacts under
   `runs/`. No API key is used anywhere.

## Notes

- Secret-safety: values are masked in the PNG and never appear in the index — a password can't leak
  through the bridge files. (Verified by `BridgeVisionDeciderTests`.)
- This complements the hosted path: `--vision` (Tier-2 fallback) uses a real OpenAI-compatible VLM via
  `.env`; `--vision-bridge` swaps that for the key-free agent-in-the-loop bridge.
