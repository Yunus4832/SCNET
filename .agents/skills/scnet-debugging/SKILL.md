---
name: scnet-debugging
description: Diagnose and smoke-test SCNET game and Headless server changes with repeatable build, startup-session, stdin-command, log, exception, timeout, and evidence-preservation workflows. Use when reproducing runtime bugs, validating networking or Mod changes, starting a test world, inspecting crashes or hangs, or verifying a fix without relying only on unit tests.
---

# SCNET Debugging

Run from the repository root. Treat runtime logs, exit status, command results, and preserved artifacts as evidence; do not infer success merely because the process stayed alive.

## Workflow

1. Check `git status --short` and preserve unrelated changes.
2. Read [references/runtime.md](references/runtime.md) before starting GUI or Headless sessions.
3. Build the narrowest affected project and run focused tests.
4. For a repeatable Headless check, run:

```bash
.agents/skills/scnet-debugging/scripts/smoke-headless.sh \
  --session codex-smoke \
  --world TestWorld
```

5. Inspect the reported artifact directory and `server.log`.
6. Search for `ERROR:`, unhandled exceptions, command failures, disconnects, and missing readiness markers.
7. Correlate a failure with the operation immediately before it. Preserve the complete exception and relevant preceding log context.
8. Stop all started processes cleanly. Never leave a Headless server running after validation unless the user explicitly asks for an interactive session.
9. Run the focused tests again after a fix, then a broader build/test proportional to the risk.

## Choose the test surface

- Use unit tests for parsers, registries, serialization, permissions, Mod lifecycle, and deterministic state.
- Use Headless smoke tests for startup, world loading, server networking, stdin commands, save/stop, and server-side Mods.
- Use a GUI client only for rendering, input, screen transitions, widgets, and client/server interaction.
- Use both Headless and GUI for network protocol or multiplayer behavior.

## Interactive sessions

Use a PTY when the user needs to interact with a live server. Wait for the exact `Headless server started` marker before reporting readiness. Send stdin commands without a leading slash or with one; both are accepted by the dispatcher. Use `Ctrl+C` for graceful shutdown and verify process exit.

Do not mutate `RunningSetting.xml` with `--save` for temporary tests. Always provide a named `--session`; provide `--world` only together with `--session`.

## Failure rules

Treat any of the following as a failed smoke test until explained:

- non-zero exit;
- missing readiness marker before timeout;
- `ERROR:` or `Unhandled exception`;
- `COMMAND ERROR`;
- process exits before the requested stop;
- server stops producing expected progress or command output.

For evidence contents and reporting, read [references/evidence.md](references/evidence.md).

## Current automation boundary

This Skill can build, start, observe, issue server stdin commands, and preserve evidence. It does not yet provide semantic player input or GUI control. Do not simulate those capabilities with direct world mutation when the test requires realistic player behavior.

