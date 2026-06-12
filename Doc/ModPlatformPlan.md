# Mod Platform Plan

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

A temporary profile generated for a multiplayer session. It is the only source of
truth for which mods should be loaded when reconnecting to that server.

### Restart Context

A persisted record that explains why the game is restarting and how to resume the
interrupted flow.

## Profiles

Profiles are the center of the loading model.

Suggested profile kinds:

- `SingleplayerProfile`
- `ServerProfile`
- `SessionProfile`

All profile kinds should share one package requirement model.

### Package Requirement

Each required package should declare:

- `modId`
- `version`
- `packageHash`
- `side`
- optional `repository`

Suggested shape:

```json
{
  "modId": "example.downed",
  "version": "1.0.0",
  "packageHash": "abc123...",
  "side": "common"
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
2. Client compares the requirement list with the local cache.
3. Missing packages are downloaded from the configured repository.
4. A session profile is generated.
5. Restart context is written.
6. Game restarts.
7. Startup loads only the session profile.
8. Client resumes connection.

If required packages cannot be resolved:

- multiplayer join is refused

## Singleplayer Startup Model

1. Resolve the selected single-player profile.
2. Check every required package in the local cache or local sources.
3. Download missing packages if a repository is configured.
4. Fail startup if requirements are still unresolved.
5. Load only the selected profile.

If a package is not declared in the profile:

- it is not loaded

## Restart and Resume

Restart must become explicit infrastructure.

### Restart Context Must Include

- restart reason
- target session profile id or path
- target server endpoint if applicable
- password or token if needed
- auto-resume flag

### Restart Triggers

- missing multiplayer packages downloaded
- single-player package install requiring process restart
- profile switch requiring clean relaunch

### Resume Rules

- on startup, check for pending restart context
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
2. Move GUI and headless startup to profile-driven loading.
3. Leave directory scan only as a package discovery utility.
4. Remove `DisabledPackages` from runtime activation logic.
5. Keep a temporary import path for old local package layouts if needed.

## Implementation Phases

### Phase 0: Freeze Design

- finalize profile schema
- finalize cache schema
- finalize repository metadata schema
- finalize restart context schema
- finalize multiplayer flow

### Phase 1: Profile-Driven Runtime

- add profile models
- add profile serialization
- implement `StartFromProfile`
- switch GUI startup to profile loading
- switch headless startup to profile loading

### Phase 2: Cache and Resolution

- add content-addressed cache
- add package resolver
- add missing-package detection
- separate activation from storage

### Phase 3: Restart Infrastructure

- add restart context model
- persist pending restart data
- implement startup resume logic

### Phase 4: Repository MVP

- implement package upload
- implement package index
- implement package download by hash
- implement simple client resolver against repository

### Phase 5: Multiplayer Session Flow

- server declares required mods
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
2. Add `StartFromProfile(...)` in `GameModRuntime`.
3. Refactor GUI and headless startup to stop using direct directory-driven activation.
4. Add `RestartContext` and startup resume support.
5. Replace current download-to-`Mods/` behavior with cache-backed profile fulfillment.

## Summary

The required redesign is large, but the direction is now clear:

- repository-driven distribution
- cache-backed local storage
- profile-driven activation
- explicit restart and resume
- deterministic multiplayer session loading

The current directory-scan plus disabled-list model should be treated as legacy and
replaced, not extended.
