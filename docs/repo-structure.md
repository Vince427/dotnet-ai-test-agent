# Repo structure

```text
src/
  AgentRunner/
  Core/
  UIAutomation/
  Samples/
    Sample.WinFormsApp/

docs/
  spec.md
  architecture.md
  repo-structure.md
  roadmap.md
  github-web-upload.md
  repo-metadata.md

prompts/
  master-agent.md
  fix-build.md

scripts/
  run-demo.ps1
  check.ps1

README.md
WORKFLOW.md
.gitignore
LICENSE
THIRD_PARTY_NOTICES.md
```

## Folder roles

- `src/Core`: models, interfaces, and lightweight abstractions.
- `src/UIAutomation`: FlaUI integration layer.
- `src/AgentRunner`: observe → decide → act loop.
- `src/Samples/Sample.WinFormsApp`: demo target application.
- `docs/`: project source-of-truth documents.
- `prompts/`: reusable prompts for AI dev agents.
- `scripts/`: local demo and verification scripts.
