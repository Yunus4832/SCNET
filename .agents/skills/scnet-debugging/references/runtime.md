# SCNET Runtime Reference

## Startup model

- `RunningSetting` selects `Gui` or `HeadlessServer` and the active session.
- `SessionInfo` selects a world or remote target.
- Temporary tests should use command-line overrides and should not pass `--save`.
- Automated runs must use a dedicated `--instance`; never use the default instance.
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
  --instance codex-smoke \
  --server \
  --session codex-smoke \
  --world TestWorld \
  --log-level Debug
```

Transient multiplayer overrides are `--server-port`, `--broadcast-port`, `--connect HOST:PORT`,
and explicit `--player NAME`. Do not add `--save` unless persistence is part of the test.

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
permissions. An unclaimed server prints a process-local claim code. A player must connect and run:

```text
/auth claim <claim-code>
```

The claim grants delegable `game:permissions.manage.standard`. It lets the player manage
standard command permissions without automatically receiving those command permissions. GUI and
Headless use the same online-player claim path; GUI presents a local claim dialog, while Headless
prints the code to stdout. Until a player claims it, the server remains unclaimed.

Headless console recovery and inspection commands:

```text
auth status
auth code
auth regenerate
```

`game:server.stop` is `OperatorOnly` and cannot be granted to a player. `OperatorManaged`
permissions can be granted for use only by a server operator. Permissions are explicit namespaced
resources; wildcard permission nodes are not supported.

Use `grant` for a non-delegable grant. A delegated player can grant or revoke only the same
permission node or a node covered by their wildcard scope:

```text
permission grant "Player Name" game:world.time.set
permission delegate "Player Name" game:world.time.set
permission revoke "Player Name" game:world.time.set
permission list "Player Name"
```

Permission mutations are always executed by the server. Client commands are requests; never mutate
permission state in client-side diagnostic code.

stdin and the Android Headless management UI both execute as `ServerOperator`; they differ only in
their frontend. Commands that require an online player are filtered by `AllowedPrincipals` and do
not appear in server-control suggestions.

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
