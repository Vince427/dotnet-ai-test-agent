# Project Rules & Guidelines

*This document consolidates the guidelines, architecture, and workflow rules derived from the project's documentation and prompts.*

## 1. Mission and Core Priorities
- **Mission**: Build a local, simple, demo-first MVP of an AI desktop testing agent for .NET.
- **Absolute priorities**:
  1. Runnable code
  2. Clarity
  3. Demo value
  4. Simplicity
  5. Lightweight evolvability

## 2. Constraints and Exclusions
- **Hard constraints**: C#, .NET 8 (with .NET 4.8 legacy support), local-first, simple and readable code, few dependencies, no over-engineering. The result must be runnable quickly.
- **V1 Exclusions**: Do not add microservices, Kubernetes, Temporal, Azure, complex auth, complex databases, distributed multi-agent architectures, enterprise dashboards, or advanced cloud infrastructure without an explicit reason.

## 3. Work Loop & Definition of Done
- **Work Loop**:
  1. Read specs and architecture.
  2. Implement the smallest runnable increment.
  3. Run verification scripts.
  4. Fix issues before adding another layer.
- **Definition of Done**:
  - The code builds.
  - The demo run works.
  - Files stay aligned with the spec.
  - No refactor introduces unnecessary complexity.

## 4. Fix Build Guidelines
- Do not massively refactor.
- Fix compilation errors first, then runtime issues.
- Do not add unnecessary architecture.
- Keep the demo runnable.

## 5. Architecture & Folder Roles
- `src/Core`: Models, interfaces, and lightweight abstractions.
- `src/UIAutomation`: FlaUI integration layer.
- `src/AgentRunner`: Observe → Decide → Act loop.
- `src/Samples`: Demo target applications.
