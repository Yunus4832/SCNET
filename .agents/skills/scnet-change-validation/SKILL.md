---
name: scnet-change-validation
description: "Select proportional static checks, builds, focused tests, broader tests, or runtime checks for SCNET code changes. Use after implementing or reviewing a change when deciding what validation is justified, especially when build and test cost should be minimized; use scnet-debugging instead for runtime reproduction and smoke-test execution details."
---

# SCNET Change Validation

Validation cost must be proportional to the risk and the evidence needed for the user's request. Do not build or run tests mechanically after every edit.

## Start with cheap evidence

Inspect the affected paths and dependency surface. Prefer inexpensive checks when they can answer the relevant question:

- search for remaining or broken references with `rg`;
- inspect the focused diff and run `git diff --check`;
- when C# files changed, load `scnet-code-style` and run its changed-file validator; this is required even when compilation succeeds because builds do not currently enforce every `.editorconfig` diagnostic;
- verify paired registrations, serialization paths, call sites, or configuration entries statically;
- use an existing formatter or purpose-built repository validator when it directly covers the change.

Pure comments, documentation, straightforward private implementation edits, and obvious local renames usually do not justify compilation by themselves. C# style validation remains required for any changed C# file even when compilation is unnecessary.

## When to compile

Compile the narrowest affected project when at least one condition applies:

- the user explicitly asks for a build;
- the user reports a compilation or runtime failure and the fix needs confirmation;
- types, signatures, generic constraints, source files, project references, conditional compilation, `.csproj`, props, targets, or target frameworks changed;
- generated code, platform-specific targets, or cross-project references make static confidence insufficient;
- the final claim depends on the code compiling.

Do not build the full solution when one project or target provides sufficient evidence. Avoid restore unless dependencies changed or the existing assets are unavailable.

## When to run tests

Run the smallest relevant test selection when behavior is deterministic and an existing or new test can materially catch regressions. Tests are especially valuable for parsers, serialization, persistence, registries, permissions, protocol encoding, merge or precedence rules, and bug fixes with a stable reproduction.

Add or update tests when the changed behavior has a durable contract, important edge cases, or a demonstrated regression that can be isolated cheaply. Do not add tests that merely mirror implementation details, assert trivial accessors, or require brittle runtime setup without meaningful protection.

Run broader project tests only when shared behavior has a meaningful blast radius, focused selection is unavailable, or the user requests it. Full-solution tests are reserved for broad cross-project changes or release-level confidence.

## Runtime validation

Use runtime checks when correctness depends on lifecycle, networking, GUI input, rendering, Android behavior, world loading, Headless startup, timing, or external resources. Load and follow `scnet-debugging` for those sessions, including isolated instances, logs, evidence, and cleanup.

If validation is skipped, state why the available static evidence is sufficient. If a check fails for an unrelated or environmental reason, distinguish it from regressions caused by the change and do not claim success without supporting evidence.
