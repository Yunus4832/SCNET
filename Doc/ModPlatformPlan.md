# Mod Platform Plan

> Historical design note. This file records the original mod platform plan and
> should not be treated as the current implementation contract. Current startup
> session behavior is documented in [StartupSessions.md](./StartupSessions.md),
> current headless behavior in [Headless.md](./Headless.md), and current
> repository behavior in [ModServer.md](./ModServer.md).

This document defines the target architecture and implementation plan for SCNET's
next-generation mod platform.

Project version is currently `0.0.0.1`. The system is not released yet, there are
no published mods, and compatibility with older behavior is not a requirement.
This allows the loading model to be redesigned directly instead of maintained
through transitional compatibility layers.

## Goals

- Make mod activation fully configuration-driven.
- Separate package storage from package activation.
- Support both single-player and multiplayer with the same package model.
- Support downloading required mods from a central mod repository.
- Support restart and resume flows after downloading missing mods.
- Support server-required multiplayer mod sessions without loading unrelated local mods.
- Keep existing gameplay unchanged when no mod or profile requires a change.

## Non-Goals

- Strong authentication or signed package verification in the first phase.
- Public package moderation workflows.
- Backward compatibility with the current `DisabledPackages`-driven loading model.
- Full UI and operations tooling in the first phase.
- Rich package search and ranking in the first phase.

## Current Problems

The current implementation has working pieces, but the model is wrong:

- Runtime loading is directory-driven.
- `ModSelectionSettings.DisabledPackages` is subtractive, not declarative.
- Multiplayer mod download currently assumes server-provided HTTP access.
- Downloaded mods are mixed into the normal local mod directory.
- Restart exists, but restart context and resume flow are not formalized.
- Single-player and multiplayer do not share one clear activation model.

This must be replaced by a profile-driven system.

## Target Model

The new model has four layers:

1. Package repository
2. Local cache
3. Activation profiles
4. Runtime loader

Package presence does not imply activation.
Download does not imply activation.
Only profiles decide what is loaded.

## Core Concepts

### Package

A `.scpak` package identified by:

- `modId`
- `version`
- `packageHash`
- `side`
- dependency metadata

### Repository

A remote service that stores package metadata and package binaries.

It behaves more like a focused private package repository than a game file mirror.

### Cache

Local immutable package storage keyed by `packageHash`.

Suggested storage shape:

```text
ModCache/<packageHash>.scpak
```

### Profile

A declarative configuration that lists exactly which mods are required for a run.

### Session Profile

A temporary profile generated for a concrete game session. It is the only source of
truth for which mods should be loaded when resuming that session.

### Restart Context

A persisted record that explains why the game is restarting and how to resume the
interrupted flow.

### Running Setting

`RunningSetting` already exists and currently carries host startup parameters such as:

- `RunMode`
- `LogLevel`
- `World`
- `Seed`
- `RemainingArgs`

It should remain the root startup input object, but it needs to grow from a simple
host launch setting into a host-plus-session bootstrap contract.

It is the right place to carry:

- current host mode
- selected world
- selected profile id or path
- optional temporary session id
- optional remaining restart/resume arguments

It should not become the full session state store. The detailed restart steps and
recovery data still belong in persisted session/restart records.

## Profiles

Profiles are the center of the loading model.

Suggested profile kinds:

- `GlobalProfile`
- `SingleplayerProfile`
- `ServerProfile`
- `SessionProfile`

All profile kinds should share one package requirement model.

### Profile Scope and Precedence

Profile scope must be explicit. The system should support these layers:

1. `GlobalProfile`
2. `SingleplayerProfile` or `ServerProfile`
3. `SessionProfile`

Precedence should be:

```text
SessionProfile > World/Server Profile > GlobalProfile
```

Rules:

- `GlobalProfile` defines local defaults and local opt-in packages.
- `SingleplayerProfile` defines the exact mod set for a local world or local play session.
- `ServerProfile` defines the exact mod set for a hosted server process.
- `SessionProfile` fully defines the mod set for a resumable session and takes precedence over lower scopes.

For multiplayer, `SessionProfile` should be authoritative. It should not silently merge
unrelated local mods from lower scopes.

### Package Requirement

Each required package should declare:

- `modId`
- `version`
- `packageHash`
- `side`
- dependency list
- optional `repository`

This record should describe a resolved package requirement, not just a loose request.

Suggested shape:

```json
{
  "modId": "example.downed",
  "version": "1.0.0",
  "packageHash": "abc123...",
  "side": "common",
  "dependencies": []
}
```

### Singleplayer Profile

Purpose:

- Defines exactly which mods are active when launching local gameplay.

Rules:

- If a required package is missing locally, attempt download from the configured repository.
- If download fails, do not start the game session.
- Packages not listed in the profile are not loaded.

### Server Profile

Purpose:

- Defines exactly which mods a headless or hosted server uses.

Rules:

- Server startup checks that all required packages exist locally or can be installed.
- Startup fails if a required package cannot be resolved.

### Session Profile

Purpose:

- Defines exactly which mods a client must load when joining a server.

Rules:

- Generated from the server's declared mod requirements.
- Stored before restart.
- Loaded after restart instead of scanning the local mod directory.

## Local Directory Model

Suggested logical layout:

```text
Mods/
ModCache/
ModProfiles/
ModSessions/
PendingRestart/
```

Recommended responsibilities:

- `Mods/`
  - manual imports, local development packages, optional loose package source
- `ModCache/`
  - content-addressed cached packages
- `ModProfiles/`
  - named single-player or local-host activation profiles
- `ModSessions/`
  - temporary multiplayer activation profiles
- `PendingRestart/`
  - restart resume context

The runtime must not treat `Mods/` as the activation source.

In addition to directories, the design should define the metadata files that make
cleanup and resume deterministic:

- cache index
- profile registry
- session metadata
- restart metadata

## Runtime Loading Rules

The runtime loader should support:

- loading from a profile
- loading from a generated session profile
- loading from explicitly resolved package sources

It should not use directory scanning as the primary activation path.

Required API direction:

- `GameModRuntime.StartFromProfile(...)`
- `GameModRuntime.StartFromSessionProfile(...)`
- `GameModRuntime.StartFromPackageSources(...)`

The new loader entry points should accept resolved package sources produced by
profile resolution. They should not mix profile parsing, repository access, and
runtime loading into one method.

## Repository Design

The first repository version can be intentionally simple.

### Required Capabilities

- publish package
- query package metadata
- download package by hash
- resolve latest or exact version for a mod id

### Minimal Metadata

- `modId`
- `version`
- `packageHash`
- `downloadUrl`
- `side`
- dependency list
- package size

The server handshake model also needs one higher-level identity record:

- `serverModSetHash`

This hash identifies the exact resolved mod set required by the server session and is
used to:

- detect stale session profiles
- invalidate old restart contexts
- distinguish between different server-required mod sets even when package ids overlap

### Suggested API

- `POST /mods/upload`
- `GET /mods/index`
- `GET /mods/{modId}`
- `GET /packages/{packageHash}`

Strict authentication is intentionally deferred.

## Multiplayer Connection Model

The game server should not act as the long-term mod content source.

Instead:

1. Server declares required mods during connection negotiation.
2. Server also declares `serverModSetHash`.
3. Client compares the requirement list with the local cache.
4. Missing packages are downloaded from the configured repository.
5. A session profile is generated.
6. Restart context is written.
7. Game restarts.
8. Startup loads only the session profile.
9. Client resumes connection.

If required packages cannot be resolved:

- multiplayer join is refused

## Singleplayer Startup Model

1. Resolve the selected single-player profile.
2. Materialize or reuse a concrete session profile for this run.
3. Check every required package in the local cache or local sources.
4. Download missing packages if a repository is configured.
5. If missing packages were installed and a clean restart is required, write restart context and restart.
6. Fail startup if requirements are still unresolved.
7. Load only the resolved session profile.

If a package is not declared in the profile:

- it is not loaded

## Restart and Resume

Restart must become explicit infrastructure.

Restart and resume are not multiplayer-only behavior. They are generic session
infrastructure and must work for:

- multiplayer join flows
- single-player profile fulfillment
- hosted server startup
- explicit profile switching

### Session Identity

Every resumable session should have a stable `sessionId`.

Suggested uses:

- identify the active `SessionProfile`
- bind restart context to a concrete session
- allow startup to resume a known flow through `RunningSetting`
- support future session-local logs and cleanup

### Restart Context Must Include

- restart reason
- target session id
- target session profile id or path
- target server endpoint if applicable
- password or token if needed
- auto-resume flag
- resume step or resume action

The stored restart context should define what step must happen after restart, for
example:

- continue single-player startup
- continue multiplayer join
- continue hosted server startup

### Restart Triggers

- missing multiplayer packages downloaded
- single-player package install requiring process restart
- profile switch requiring clean relaunch

### Resume Rules

- on startup, check for pending restart context
- bootstrap `RunningSetting` with the pending session id if present
- if found, resolve required profile first
- load only that profile
- continue the interrupted flow

## Side and Runtime Role Model

Current manifest side selection is:

- `Common`
- `Client`
- `Server`

This is enough for the next phase if profiles determine activation.

`RunMode` checks inside mod code are still allowed, but runtime activation should
primarily be controlled by:

- profile contents
- declared `ModSide`
- selected host role

Future work may refine roles further for local GUI-hosted server logic, but that is
not required to begin the platform redesign.

### Runtime Role Matrix

The design should assume at least these host situations:

| Host situation | RunMode | Expected active role |
| --- | --- | --- |
| GUI single-player | `Gui` | client-facing runtime, local world session |
| GUI local-host / integrated server | `Gui` | client-facing runtime plus hosted world session |
| GUI remote multiplayer client | `Gui` | client-facing runtime, remote session |
| Headless dedicated server | `HeadlessServer` | hosted server runtime |

The plan does not need to solve every mixed-role implementation detail immediately,
but it must not assume that `Gui` always means "client-only gameplay logic".

## Configuration Serialization

Profiles and restart contexts should be serialized in JSON.

Reasons:

- readable during development
- easy to diff
- easy to inspect after failed startup
- easy to evolve while the project is pre-release

Suggested file groups:

- `ModProfiles/*.json`
- `ModSessions/*.json`
- `PendingRestart/*.json`

`RunningSetting` remains a startup contract and can persist a temporary session id or
resume-oriented startup arguments, but detailed session and restart data should stay
in their own JSON documents instead of being expanded into a large XML state file.

## Failure Policy

### Singleplayer

- Missing required package:
  - attempt download
  - fail startup if still missing

### Multiplayer

- Missing required package:
  - attempt download
  - restart if download succeeds and session profile is created
  - refuse join if unresolved

### Hash mismatch

- treat the package as invalid
- do not activate it
- redownload if repository is available

## Migration Strategy

No compatibility obligation exists yet, but implementation still needs an ordered cutover.

Recommended transition:

1. Introduce profile model and new loader APIs.
2. Introduce restart/session context and connect it to `RunningSetting`.
3. Move GUI and headless startup to profile-driven loading.
4. Leave directory scan only as a package discovery utility.
5. Remove `DisabledPackages` from runtime activation logic.
6. Keep a temporary import path for old local package layouts if needed.

## Implementation Phases

### Phase 0: Freeze Design

- finalize profile schema
- finalize profile scope and precedence
- finalize cache schema
- finalize repository metadata schema
- finalize server mod set identity
- finalize restart context schema
- finalize session model
- finalize `RunningSetting` responsibilities
- finalize single-player and multiplayer resume flow

### Phase 1: Profile-Driven Runtime

- add profile models
- add profile serialization
- implement `StartFromProfile`
- implement `StartFromSessionProfile`
- switch GUI startup to profile loading
- switch headless startup to profile loading

### Phase 2: Restart and Session Infrastructure

- add session profile model
- add restart context model
- persist pending restart data
- extend `RunningSetting` with session bootstrap fields
- implement startup resume logic

### Phase 3: Cache and Resolution

- add content-addressed cache
- add package resolver
- add missing-package detection
- separate activation from storage

### Phase 4: Repository MVP

- implement package upload
- implement package index
- implement package download by hash
- implement simple client resolver against repository

### Phase 5: Multiplayer Session Flow

- server declares required mods
- server declares `serverModSetHash`
- client generates session profile
- client downloads missing packages
- client restarts and resumes connection

### Phase 6: Management Tooling

- profile editor UI
- cache inspection and cleanup
- package publishing CLI
- repository admin tools

## Immediate Engineering Tasks

The next implementation tasks should be:

1. Add `ModProfile` and `SessionModProfile` models.
2. Define profile scope and precedence rules.
3. Add `StartFromProfile(...)` and `StartFromSessionProfile(...)` in `GameModRuntime`.
4. Add `RestartContext` and startup resume support.
5. Extend `RunningSetting` to carry session bootstrap data.
6. Refactor GUI and headless startup to stop using direct directory-driven activation.
7. Replace current download-to-`Mods/` behavior with cache-backed profile fulfillment.

## Summary

The required redesign is large, but the direction is now clear:

- repository-driven distribution
- cache-backed local storage
- profile-driven activation
- explicit restart and resume
- deterministic multiplayer session loading

The current directory-scan plus disabled-list model should be treated as legacy and
replaced, not extended.
