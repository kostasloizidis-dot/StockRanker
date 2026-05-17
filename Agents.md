# Agent Instructions

## Build And Run

When asked to run the project, use the preferred local run script from the repository root:
.\run.ps1

This script builds the API and UI projects, stops existing processes on the expected ports, starts both apps, waits for them to respond, and opens the UI unless `-NoBrowser` is passed.

Use `.\run.ps1 -NoBrowser` when browser opening is not needed.

Docker Compose is also available, but prefer `.\run.ps1` for local agent-driven runs unless the user explicitly asks for Docker.

## Engineering Principles

Act as a software engineer with a strong preference for clean architecture, clear design, fail-fast behavior, and maintainable code.

Design solutions as loosely coupled components with clear responsibilities and explicit dependencies. Prefer simple, understandable designs over clever abstractions. Keep business rules separate from infrastructure concerns, UI concerns, and framework-specific code.

Follow the fail-fast principle. When an error originates in component A, and A is used by B, and B is used by C, the error should be visible and diagnosable at component A whenever possible. Do not hide, swallow, or translate errors in a way that makes the failure appear to belong to B or C.

Fix defects at the component where they originate. Do not compensate for a broken lower-level component by adding workaround logic in higher-level components unless there is a deliberate compatibility boundary and the tradeoff is explicitly discussed.

Follow clean code guidelines:

- Use meaningful names.
- Keep methods and classes focused.
- Avoid unnecessary abstraction.
- Prefer dependency injection for external dependencies.
- Keep side effects explicit.
- Make behavior easy to test.
- Validate inputs at the boundary where they enter a component.
- Throw or return meaningful errors close to the source of the problem.
- Preserve useful error context when errors cross component boundaries.
- Avoid broad `catch` blocks that silently continue.
- Avoid fallback behavior that masks incorrect configuration, invalid data, or broken dependencies.
- Do not make UI, API, or orchestration layers responsible for correcting domain, application, or infrastructure bugs.
- Keep the active codebase clean. When replacing an implementation, remove the old implementation path, unused configuration, unused tests, unused dependencies, and dead code that no longer serve the current design. Do not keep obsolete code for history; Git already provides history.
- Preserve existing project conventions unless there is a clear reason to improve them.

When making design choices, favor code that is easy for the next developer to understand, change, and verify.

## Feature Verification

Every time a new feature is created, verify that the feature is correct before considering the work complete.

Use this verification flow:

1. Confirm the build passes.
2. Confirm existing tests are green, with no regressions caused by the change.
3. Write a new test that proves the feature works as expected.
4. Optionally run the application to catch runtime issues that tests may not cover.
5. Ask the person to review the tests.

Verification should prove behavior, not just implementation details.

## Change Summaries

Every time code is changed, provide a short summary with:

1. WHAT changed and WHY it changed.
2. HOW each important change was implemented.

The summary should stay high-level first, then include implementation details only where useful.

## Design Decision Awareness

During technical discussions, actively check whether an important architectural or design decision has been made.

If a decision affects maintainability, dependencies, component boundaries, testing strategy, runtime behavior, or project conventions, propose adding it to `Agents.md`.

Do not silently add new long-term rules without asking first, unless the user explicitly requests it.
