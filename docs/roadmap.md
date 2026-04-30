# Roadmap

## V1.3 — Generic Robo Agent + Symphony Foundation ✅

- generic UI tree discovery (all elements)
- dynamic goals (configurable objectives)
- WORKFLOW.md policy (Symphony-style)
- loop detection (anti-loop sliding window)
- scoring engine (reward/penalty)
- structured logging (key=value)
- run artifacts (JSON + screenshots + markdown)
- multi-strategy element resolution
- extended action types

## V2 — Vision-First & Multi-Modal Perception

- vision LLM integration (Claude 4.7 Opus / Gemini 3.1 Pro / GPT-5.5)
- vision-first perception (replacing local OCR): VLM reads the screen to compensate for poor WinForms DOM semantics
- bounding box overlay generation for precision clicking
- perception fusion (UIA semantics + Visual semantics)
- *All local dependencies MUST strictly remain under MIT license.*

## V3 — Intelligent Agent

- planner / critic split (2-pass LLM)
- self-healing tests (retry with fallback)
- multi-strategy element resolution chain
- more action types (right-click, drag, etc.)

## V4 — Web Cockpit

- ASP.NET Core admin dashboard
- target management
- goal management
- run history viewer
- settings configuration

## V5 — LegacyOps Deployment

- legacy WinForms app deployment management
- database configuration
- Hyper-V provisioning scripts
- Azure VM infra-as-code (Bicep)

## V6 — CI/CD Pipeline

- GitHub Actions CI
- automated smoke tests
- Azure VM deployment
- VHD import/export

## V7 — Durable Orchestration

- Mistral Workflows integration
- human-in-the-loop approval gates
- durable retry with audit trail
- multi-worker execution

## V8 — Agent Swarm

- parallel agent instances
- shared knowledge base
- coverage metrics
- regression detection
