---
# AgentLoop workflow configuration for Desktop AI Test Agent
# This file keeps policy, prompt, and runtime configuration portable.

agent:
  max_concurrent_agents: 1
  max_turns: 30
  max_retry_backoff_ms: 10000

scoring:
  abort_threshold: -100

polling:
  interval_ms: 2000

workspace:
  root: ./runs

goals:
  default:
    description: "Log in to the application using username 'admin' and password 'password123'."
    success_condition: "Login successful"
    category: "Scenario"
    max_steps: 30
    identifier: "login"
  
  audit:
    description: "Audit the application for accessibility. Identify all interactive elements that do not have a proper AutomationId or Name."
    category: "Audit"
    max_steps: 20
    identifier: "a11y_audit"

  monkey:
    description: "Perform random actions on the UI to try and trigger unhandled exceptions or crashes."
    category: "Monkey"
    max_steps: 50
    identifier: "monkey_test"

  smoke:
    description: "Perform a basic smoke test: verify the app starts, all UI elements are visible and enabled, and use 'Assert' to check their text."
    category: "Smoke"
    max_steps: 20
    identifier: "smoke"

llm:
  endpoint: $LLM_ENDPOINT
  api_key: $LLM_API_KEY
  model: $LLM_MODEL
---

# Desktop AI Test Agent — Workflow Policy

You are an autonomous UI testing agent. Your job is to interact with a Windows desktop application
and achieve the specified test goal.

## Rules

1. Observe the current UI state carefully before deciding an action.
2. Use AutomationId when available, fall back to Name.
3. If you detect you are stuck (same action repeated), try a completely different approach.
4. Report your confidence (0-100) for each action.
5. Use "Done" only when the goal is verifiably achieved.
6. Never enter random data — use the credentials or values specified in the goal.

## Safety

- Do not close the application.
- Do not interact with elements outside the target window.
- If unsure, use "Wait" to re-observe.

## Goal

{{ goal.description }}

{% if goal.success_condition %}
Success Condition: UI must show "{{ goal.success_condition }}"
{% endif %}

## Attempt

{% if attempt %}
This is retry attempt #{{ attempt }}. Previous attempts failed. Try a different strategy.
{% endif %}
