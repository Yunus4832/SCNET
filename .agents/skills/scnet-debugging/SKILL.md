---
name: scnet-debugging
description: Diagnose and smoke-test SCNET game, Android client, and Headless server changes on Linux, Windows, or Android with repeatable build, startup-session, command, log, exception, timeout, and evidence-preservation workflows. Use when reproducing runtime bugs, validating networking or Mod changes, starting a test world, inspecting crashes or hangs, or verifying a fix without relying only on unit tests.
---

# SCNET Debugging

Run from the repository root. Treat runtime logs, exit status, command results, and preserved artifacts as evidence; do not infer success merely because the process stayed alive.

## Workflow

1. Check `git status --short` and preserve unrelated changes.
2. Read [references/runtime.md](references/runtime.md) before starting GUI or Headless sessions. On Windows, also read [references/windows.md](references/windows.md) and use its PowerShell commands instead of Bash scripts. For an Android client, read [references/android.md](references/android.md) before using ADB.
3. Build the narrowest affected project and run focused tests.
4. On Linux, run the repeatable Headless helper:

```bash
.agents/skills/scnet-debugging/scripts/smoke-headless.sh \
  --session codex-smoke \
  --world TestWorld \
  --game-mode Creative
```

The script creates a dedicated data instance by default. It records whether the instance existed
before the run, copies instance logs into the artifact directory, and deletes a newly created
instance after a successful run. It preserves failed runs for diagnosis. Pass `--keep-instance`
only when a successful run must remain reproducible. Never run automated checks against the default instance.
On Windows, follow the equivalent log-driven procedure in [references/windows.md](references/windows.md).

5. Inspect the reported artifact directory and complete instance runtime logs. Treat the files under
   `Instances/<instance>/Logs` as the authoritative runtime record on both platforms.
6. Search for `ERROR:`, unhandled exceptions, command failures, disconnects, and missing readiness markers.
7. Correlate a failure with the operation immediately before it. Preserve the complete exception and relevant preceding log context.
8. Stop all started processes cleanly. Never leave a Headless server running after validation unless the user explicitly asks for an interactive session.
9. Review every instance created during the task. Delete it after evidence has been copied unless it
   is still required for reproduction, comparison, or explicit user inspection. Report the path and
   reason for every retained instance. Never delete an instance that existed before the task.
10. Run the focused tests again after a fix, then a broader build/test proportional to the risk.

## Choose the test surface

- Use unit tests for parsers, registries, serialization, permissions, Mod lifecycle, and deterministic state.
- Use Headless smoke tests for startup, world loading, server networking, stdin commands, save/stop, and server-side Mods.
- Use a GUI client only for rendering, input, screen transitions, widgets, and client/server interaction.
- Use an Android client with ADB for Android startup, networking, lifecycle, graphics, and device-specific failures.
- Use both Headless and GUI for network protocol or multiplayer behavior.

## Multi-instance sessions

Use a distinct `--instance` for every concurrently running process. The Starter consumes this
argument before loading game settings and maps each process to `Instances/<name>`.

```bash
SurvivalcraftStarter --instance debug-server --server --session debug-server --world DebugWorld --game-mode Creative
SurvivalcraftStarter --instance debug-client --gui
```

Read [references/multiplayer.md](references/multiplayer.md) before starting a GUI + Headless lab.
Record whether each server and client instance existed before the lab. At teardown, stop every
process, preserve required logs, and remove only the instances created by the current lab. A retained
instance requires an explicit reason in the final report.

## Interactive sessions

On Linux, use a PTY when the user needs to interact with a live server. On Windows, read the instance
log for observation and use the allocated Headless console only for command input. Wait for the exact
`Headless server started` marker in the runtime log before reporting readiness. Send stdin commands
without a leading slash or with one; both are accepted by the dispatcher. Stop gracefully and verify process exit.

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

## HTTP command host

The HTTP command host is disabled by default. For HTTP integration debugging, explicitly pass
`--http-command` and, for parallel instances, a unique `--http-command-port`. The session's
`HttpCommandAccessToken` overrides the token in the instance `Settings.xml`; otherwise use the
generated instance token. Do not expect `.runtime` to contain HTTP endpoint settings or credentials:
it is only the Starter's process-liveness registry. Do not pass `--save` unless persistence of the
HTTP session override is itself under test.

After authenticating, call `GET /commands` before sending automation commands. It returns the
currently executable HTTP command identities and their declared argument contracts. Use the returned
identity and arguments with `POST /commands`; do not infer a command contract from text-command help.

For an automated multiplayer startup, use `--server-port`, `--broadcast-port`, `--connect`, and
the explicit `--player` option described in [references/multiplayer.md](references/multiplayer.md).
These overrides are transient unless `--save` is explicitly supplied.

For a GUI-hosted server on Linux, use `scripts/start-gui-server.sh`. On Windows, invoke the Windows
Starter with the equivalent arguments from [references/windows.md](references/windows.md).
For an Android client, inject the same transient startup options through the
`Survivalcraft.Android.CommandLine` Intent extra as described in [references/android.md](references/android.md).
