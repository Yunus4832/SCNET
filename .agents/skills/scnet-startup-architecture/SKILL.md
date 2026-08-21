---
name: scnet-startup-architecture
description: Design, review, or modify SCNET startup arguments, startup sessions, run modes, instance configuration, restart restoration, Settings persistence, or .runtime state while preserving the repository's object boundaries and precedence rules.
---

# SCNET Startup Architecture

Use this Skill before changing startup parsing, instance selection, GUI or Headless startup,
restart behavior, session persistence, settings defaults, or any feature that adds a startup-time
parameter.

## Establish the current model

Read [Doc/StartupSessions.md](../../../Doc/StartupSessions.md) completely before editing. Then read
the current definitions and the manager that owns the affected transition:

- `Survivalcraft/Game/Data/RunningSetting.cs`
- `Survivalcraft/Game/Data/StartupRequest.cs`
- `Survivalcraft/Game/Data/SessionInfo.cs`
- `Survivalcraft/Game/Data/StartupContext.cs`
- `Survivalcraft/Managers/StartupManager.cs`
- `Survivalcraft/Managers/SessionInfoManager.cs`
- `Survivalcraft/Managers/RunningSettingManager.cs`
- `Survivalcraft/Managers/SettingsManager.cs`
- `Survivalcraft/Managers/StarterInstanceManager.cs` when instances or `.runtime` are involved

Do not infer ownership from a field's current location alone. Trace where it originates, how it is
merged, which object consumers read, and which manager serializes it.

## Object boundaries

### Starter instance and `.runtime`

`StarterInstanceManager` runs before the game instance storage roots are registered. It selects an
isolated `Instances/<id>` directory and registers the process against that instance.

`Starter.xml` stores instance selection state such as current and next instance. The instance id is
not a `RunningSetting` or `SessionInfo` field.

`Instances/<id>/.runtime` is reserved for process-liveness registration needed by instance
management. It may contain PID/start-time markers used to determine whether an instance is active.
Do not store startup parameters, effective configuration, credentials, HTTP discovery data,
session data, test artifacts, or logs there. Exclude `.runtime` when cloning an instance.

### `RunningSetting`

`RunningSetting` is the serializable application-entry configuration in
`config:RunningSetting.xml`. It owns entry concerns such as run mode, log level, GUI window state,
default/pending session pointers, and unconsumed launcher arguments.

Do not place world targets, seeds, game-mode overrides, connection targets, or other recoverable
session payloads in `RunningSetting`. It points to sessions; it does not duplicate them.

### `Settings`

`Settings` is the instance-wide defaults and user preferences stored in `config:Settings.xml`.
Examples include default game/broadcast ports and default HTTP command configuration. These values
belong to the selected data instance and apply when an effective session does not override them.

`SettingsManager.Initialize()` occurs after `StartupManager.Load()` in normal startup. Do not make
session resolution depend on already-loaded `Settings`. Resolve fallbacks at the consumer after
settings initialization, for example:

```csharp
var effectiveValue = startup.Session.OptionalOverride
                     ?? SettingsManager.Current.InstanceDefault;
```

Missing generated instance secrets may be created after settings load and saved once. Invalid
explicit configuration must follow the feature's documented failure behavior; do not silently
choose a different port, target, or credential merely to keep an optional service running.

### `StartupRequest`

`StartupRequest` is the non-serialized parse result for this process invocation. It records raw
intent from command-line arguments or Android Intent extras, including whether an override was
explicit and whether `--save` was requested.

Keep a value only in `StartupRequest` when it is genuinely one-shot and must not be restored by a
named session. Do not make downstream subsystems repeatedly merge request values themselves.

### `SessionInfo`

`SessionInfo` represents a complete, recoverable startup session, not merely a world selection.
Its `Target` can restore startup into `MainMenu`, `WorldList`, `World`, `ServerBrowser`, or
`RemoteServer`. A named session may therefore contain any parameter that should be restored when
that session is selected again, including optional service overrides.

The effective session is produced by loading the selected session and applying the current
`StartupRequest` overrides. The selection order is:

1. explicitly named session;
2. pending session;
3. default session;
4. a new temporary session.

For session-scoped values, the effective precedence is:

1. explicit value from the current `StartupRequest`;
2. value restored from the selected `SessionInfo`;
3. instance-wide value from `Settings`, when the feature defines such a fallback;
4. the code default used to initialize missing instance settings.

If a request field is merged into `SessionInfo` because it is recoverable, implement both XML write
and XML read paths. `--save` persists the merged effective session; without `--save`, the override
remains in memory for this launch. Do not put a field in `SessionInfo` while intentionally omitting
its serialization unless it is explicitly documented as derived runtime state.

### `StartupContext`

`StartupContext` is the handoff object containing the resolved `RunningSetting`, original
`StartupRequest`, and effective `SessionInfo`. It does not introduce another persistence layer.

Consumers should read:

- `Session` for the resolved, recoverable startup target and session overrides;
- `Request` only for one-shot intent that deliberately remains request-only;
- `SettingsManager.Current` for instance defaults after settings have initialized;
- `Settings` for the already-resolved application-entry settings supplied to startup.

## Decide where a new field belongs

Answer these questions before adding it:

1. Does it select the data instance or describe whether that instance is running? Use
   `StarterInstanceManager` or the liveness-only `.runtime` marker.
2. Is it an application-entry preference or a pointer to a session? Use `RunningSetting`.
3. Is it an instance-wide default or user preference? Use `Settings`.
4. Should a named session restore it on a later launch? Parse it into `StartupRequest`, merge it into
   `SessionInfo`, and serialize it when the session is saved.
5. Is it intentionally one-shot and not recoverable? Keep it in `StartupRequest` and document why.
6. Is it only the composed handoff to GUI or Headless? Read it through `StartupContext`; do not add a
   new storage location.

When two locations participate, define one as the override and one as the fallback. Do not copy the
same value into `RunningSetting`, `Settings`, `SessionInfo`, and `.runtime` for convenience.

## Change checklist

For every startup-related change:

1. Record the source, owner, merge point, effective consumer, persistence trigger, and restoration
   path for each new field.
2. Preserve the `StartupManager` flow: parse request, select session id, load/create session, apply
   overrides, create `StartupContext`, then persist only when requested.
3. Keep serialization symmetric: add write, read, legacy-default, and normalization behavior
   together. A missing value may use the documented fallback; an invalid explicit value must not be
   mistaken for a missing value.
4. Verify that `--save` changes later named-session startup and that omitting `--save` does not.
5. Verify GUI, Headless, restart, pending-session, and multi-instance consumers in proportion to the
   affected field.
6. Inspect `.runtime` after runtime validation. It must still contain only instance-liveness state.
7. Update `Doc/StartupSessions.md` and any mode-specific document such as `Doc/Headless.md` in the
   same change.

Use the `scnet-debugging` Skill when runtime smoke testing is required. Preserve temporary-instance
and evidence-cleanup rules from that Skill.

## Common architecture errors

- Treating `SessionInfo` as only a world descriptor even though it can restore application screens.
- Keeping a recoverable override only in `StartupRequest`, so a named session cannot restore it.
- Adding a field to `SessionInfo` but omitting XML serialization, bypassing `--save` semantics.
- Reading `SettingsManager.Current` while `StartupManager` is still resolving the session, before
  settings initialization.
- Putting session or service configuration into `.runtime` because it is convenient for a tool to
  discover.
- Silently repairing an invalid explicit port by choosing a default or another free port.
- Making each GUI or Headless consumer merge request, session, and settings independently.
