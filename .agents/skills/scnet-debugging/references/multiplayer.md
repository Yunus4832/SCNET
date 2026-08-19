# Multiplayer Debug Lab

## Available foundation

Every process must use a separate data instance:

```text
--instance debug-server
--instance debug-client
```

The Starter creates missing instances under `Instances/<name>`. Settings, identity, worlds, Mods,
caches, and logs are isolated. `--instance` is consumed by the Starter and is not persisted in
`RunningSetting.RemainingArgs`.

Before launching the lab, record which instance directories already exist. During teardown, stop
all server and client processes before deleting only the directories created for that lab. Preserve
logs outside those directories first. Do not keep successful lab instances by default.

Normal restart preserves the process instance. Instance switching is a separate exit action.
Linux restart uses a temporary desktop entry so GUI-to-Headless transitions receive a visible
terminal; Windows uses process arguments and Android uses an Intent extra.

## Automated startup contract

Start the server with transient ports:

```bash
SurvivalcraftStarter \
  --instance debug-server \
  --server \
  --session debug-server \
  --world DebugWorld \
  --game-mode Creative \
  --server-port 28987 \
  --broadcast-port 28988
```

Start the GUI client with a transient remote session and explicit player automation:

```bash
SurvivalcraftStarter \
  --instance debug-client \
  --gui \
  --session debug-client \
  --connect 127.0.0.1:28987 \
  --player DebugPlayer
```

`--connect` modifies only the in-memory effective session. `--save` persists that session when
explicitly requested. Port overrides are also Session fields, take priority over Settings, and
follow the same transient-by-default rule.

`--game-mode MODE` follows the same session precedence. New worlds are created in that mode.
Existing worlds run in the session mode, while automatic and shutdown saves preserve the mode
stored in the world. Use one of `Creative`, `Harmless`, `Survival`, `Challenging`, `Cruel`, or
`Adventure`.

`--player` activates automation only when explicitly present. The client first accepts an existing
player matching its stable client identity. If none arrives, it sends the existing player-create
protocol once using the requested display name. It never takes over another identity merely because
the display name matches. Without `--player`, the normal PlayerScreen flow remains unchanged.

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

The helper launches `--gui --host`. This debugging override forces and persists
`WorldSettings.RunServer=true` for both new and existing worlds, after which startup follows the
normal GUI server path. The local player is created through the normal GUI server player path.
Close the GUI normally when the lab is done.

## Android client

Android accepts the same transient `--session`, `--connect`, and `--player` options through its
startup Intent. Use a desktop server plus one device or emulator per concurrent Android client.
Follow [android.md](android.md) for the exact ADB launch, port mapping, log collection, and cleanup
procedure. Named instances isolate Android data but do not create concurrent processes within the
same installed package.
