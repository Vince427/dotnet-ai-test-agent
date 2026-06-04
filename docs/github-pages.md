# GitHub Pages

The first public docs target is the lightweight static site in `docs/`.
DocFX is intentionally deferred until API reference generation becomes useful.

## What Gets Published

- `docs/index.html`: quickstart and navigation.
- Markdown docs such as `mvp.md`, `architecture.md`, `testzoo.md`, and `roadmap.md`.
- `docs/symphony.html`: generated AgentLoop Workbench demo. The filename is
  still `symphony.html` during the safe rename window; the visible product
  language is AgentLoop.

## Workflow

`.github/workflows/pages.yml` publishes GitHub Pages on:

- manual `workflow_dispatch`;
- pushes to `main` that touch docs, tests, the AgentRunner, the render script,
  or the workflow itself.

The `Configure Pages` step uses `enablement: true`, so the workflow turns Pages on
(build source = GitHub Actions) on first run via its `pages: write` permission — no manual
repo-settings toggle needed. (If an org policy blocks API enablement, enable it once under
**Settings → Pages → Source: GitHub Actions**.) Published at `https://<owner>.github.io/<repo>/`.

The workflow renders the AgentLoop Workbench with:

```powershell
.\scripts\render-ui.ps1
```

Then it uploads the `docs/` folder as the Pages artifact.

## Local Preview

Regenerate the static Workbench before sharing a docs snapshot:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\render-ui.ps1
```

Open `docs/index.html` or `docs/symphony.html` locally.

## Boundaries

- GitHub Pages is documentation only.
- It must not become the source of truth for tests or run state.
- It must not require `.env`, OpenRouter, or a desktop session.
- Real runtime evidence remains in `runs/` locally or CI artifacts.
