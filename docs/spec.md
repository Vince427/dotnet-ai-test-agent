# V1 Spec

## Problem

Legacy .NET desktop applications are hard to test automatically in a robust and modern way.

## V1 goal

Create a simple local demo where an agent:

1. launches a sample WinForms app,
2. observes the UI,
3. decides the next action,
4. fills in the login fields,
5. clicks the submit button,
6. detects success,
7. stops cleanly.

## Constraints

- local-first,
- easy to clone,
- easy to run,
- few dependencies,
- strong visual demo,
- no cloud required,
- no GUI Docker dependency,
- no enterprise infrastructure in V1.

## Out of scope for V1

- distributed orchestration,
- cloud execution,
- production-grade OCR,
- multi-agent systems,
- SaaS portal,
- billing,
- enterprise dashboards.

## Success criteria

- someone can clone the repo,
- run the demo in a few commands,
- watch the login flow run automatically,
- understand the architecture in less than five minutes,
- see a clear path toward V2 and V3.
