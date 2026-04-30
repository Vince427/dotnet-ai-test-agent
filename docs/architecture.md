# V1 Architecture

## Principle

The V1 architecture is based on three blocks:

1. perception,
2. decision,
3. execution.

## Blocks

### 1. Target application

A sample WinForms application is the demo target.
It exposes a very simple login screen.

### 2. Perception

The runner reads the visible UI state through FlaUI.
In V1, that state can stay minimal:

- window title,
- visible elements,
- names or AutomationId values when available.

### 3. Decision

A simple agent chooses the next action.
In V1, this can be controlled logic or a fake LLM hidden behind a clean interface.
The important point is to keep the architecture ready for a real model later.

### 4. Execution

The runner executes UI actions:

- enter text,
- click button,
- wait,
- verify a final state.

### 5. Logging

Each step produces readable logs:

- observed state,
- chosen action,
- expected result,
- final status.

## Flow

1. Start the sample app.
2. Attach FlaUI.
3. Capture a UI state.
4. Ask the agent for the next action.
5. Execute the action.
6. Repeat until success or timeout.
