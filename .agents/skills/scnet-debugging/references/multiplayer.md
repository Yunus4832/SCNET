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

`--player` activates automation only when explicitly present. The client first accepts an existing
player matching its stable client identity. If none arrives, it sends the existing player-create
protocol once using the requested display name. It never takes over another identity merely because
the display name matches. Without `--player`, the normal PlayerScreen flow remains unchanged.
