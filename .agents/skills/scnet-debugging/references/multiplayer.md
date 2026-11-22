# Multiplayer Debug Lab

## Isolate the lab

Give each server and client a distinct `--instance`. Record which instance directories already
exist. At teardown, stop the processes and preserve logs before removing any disposable instance
created for the run. See [startup sessions](../../../../Doc/StartupSessions.md#数据实例) for paths
and restart behavior.

## Automated startup contract

From the repository root, [prepare local dependencies](../../../../Doc/BuildAndConfig.md#构建), then
build the Linux GUI launcher and standalone server. Start the server with transient ports.
For Windows use the resolved executable in [windows.md](windows.md).

```bash
./Survivalcraft.Server/bin/Debug/net10.0/linux-x64/Survivalcraft.Server \
  --instance debug-server \
  --session debug-server \
  --world DebugWorld \
  --game-mode Creative \
  --server-port 28987 \
  --broadcast-port 28988
```

Start the GUI client with a transient remote session and explicit player automation:

```bash
./Survivalcraft.Linux/bin/Debug/net10.0/linux-x64/SurvivalcraftStarter \
  --instance debug-client \
  --session debug-client \
  --connect 127.0.0.1:28987 \
  --player DebugPlayer
```

The connection target, ports and game-mode overrides apply to the effective session. For the
persistence rules and supported options, see [startup arguments](../../../../Doc/StartupSessions.md#实例会话和世界).
`--player` selects or creates a character for the current identity. Match the identity when validating
rejoins, since character names can be shared across worlds.

## GUI server

Use the repository helper to start a GUI-hosted world with an explicit local player:

```bash
.agents/skills/scnet-debugging/scripts/start-gui-server.sh \
  --instance debug-gui-server \
  --session debug-gui-server \
  --world DebugGuiWorld \
  --player DebugHost \
  --server-port 29987 \
  --broadcast-port 29988
```

The helper launches `--host`. This debugging override forces and persists
`WorldHostingSettings.RunServer=true` for both new and existing worlds, after which startup follows the
normal GUI server path. The local player is created through the normal GUI server player path.
Pass `--http-command-port PORT` to the helper when built-in HTTP automation is needed; it enables the host at that transient port. Close the GUI normally when the lab is done.

## Android client

Android accepts the same transient `--session`, `--connect`, and `--player` options through its
startup Intent. Use a desktop server plus one device or emulator per concurrent Android client.
Follow [android.md](android.md) for the exact ADB launch, port mapping, log collection, and cleanup
procedure. Named instances isolate Android data but do not create concurrent processes within the
same installed package.
