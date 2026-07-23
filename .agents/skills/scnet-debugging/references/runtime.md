# SCNET Runtime Reference

## Startup model

- `RunningSetting` selects `Gui` or `HeadlessServer` and the active session.
- `SessionInfo` selects a world or remote target.
- Temporary tests should use command-line overrides and should not pass `--save`.
- `--world` and `--seed` are ignored unless `--session` is also supplied.
- Read `Doc/Headless.md` and `Doc/StartupSessions.md` when changing startup behavior.

## Development commands

Build Linux:

```bash
dotnet build Survivalcraft.Linux/Survivalcraft.Linux.csproj
```

Start Headless:

```bash
Survivalcraft.Linux/bin/Debug/net10.0/linux-x64/SurvivalcraftStarter \
  --server \
  --session codex-smoke \
  --world TestWorld \
  --log-level Debug
```

The readiness marker is:

```text
Headless server started. Press Ctrl+C to stop.
```

The server stdin uses the same `CommandDispatcher` as players and Mods. Safe diagnostic commands include:

```text
help
permission
time get
stop
```

Player command permissions are explicit; `ServerMaster` and `ServerManager` do not grant command
permissions. Bootstrap a connected player from the server console:

```text
permission delegate "Player Name" *
```

Exception: the local owner player of a GUI-hosted server receives hard-coded `*` with delegation
so that a GUI server has a bootstrap path without stdin. This does not apply to remote players or
to Headless player identities.

Use `grant` for a non-delegable grant. A delegated player can grant or revoke only the same
permission node or a node covered by their wildcard scope:

```text
permission grant "Player Name" world.time.set
permission delegate "Player Name" world.*
permission revoke "Player Name" world.time.set
permission list "Player Name"
```

Permission mutations are always executed by the server. Client commands are requests; never mutate
permission state in client-side diagnostic code.

For stdin discovery, enter `permission` to print usage plus the current player and permission-node
lists. `permission players` and `permission nodes` print each list separately.

Expected result lines start with:

```text
COMMAND OK
```

## Process control

- Start long-running processes in a PTY when interaction is required.
- Record the exact PID/session returned by the tool.
- Send commands only after readiness.
- Prefer `Ctrl+C` for graceful stop so the world is saved and the Mod runtime is disposed.
- For redirected/background stdin sessions, issue `stop`; background processes may inherit ignored `SIGINT`.
- Poll for exit; use termination only if graceful stop fails.
- Never use broad process-kill patterns.

## Log inspection

Inspect the full log and a bounded context around failures:

```bash
rg -n -C 8 'ERROR:|Unhandled exception|COMMAND ERROR|failed|disconnect' <log>
```

Do not discard stack traces. A message-only exception is insufficient evidence for a precise fix.
