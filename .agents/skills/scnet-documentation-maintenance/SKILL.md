---
name: scnet-documentation-maintenance
description: "Assess and maintain SCNET repository documentation when code changes may affect documented behavior, configuration, commands, architecture, persistence, networking, modding, build, deployment, or platform support. Use while completing such changes or reviewing their documentation impact; do not invoke for edits that clearly cannot affect existing documentation."
---

# SCNET Documentation Maintenance

Treat documentation impact as part of completing a behavior or architecture change. Check `README.md` and the relevant files under `Doc/` before deciding whether an update is required. Search by affected type, option, subsystem, file format, and user-facing term instead of reading every document.

## Update documentation when

- command-line arguments, commands, configuration fields, defaults, or precedence change;
- GUI, Headless, instance, startup-session, restart, or deployment behavior changes;
- public APIs, mod contracts, package formats, required assets, or extension points change;
- network compatibility, server authority, protocol behavior, or client requirements change;
- persistence formats, file locations, migration, or world-upgrade behavior change;
- build, publish, dependency, target-framework, or supported-platform behavior changes;
- subsystem responsibilities, ownership boundaries, or documented architecture change.

Internal refactors, private implementation changes, localized bug fixes that restore already documented behavior, and visual adjustments not described by repository documents usually need no documentation edit.

## Editing rules

- Update only documents affected by the change; do not perform unrelated rewriting or stylistic cleanup.
- Describe the final supported behavior, not the implementation process or temporary debugging history.
- Keep commands, paths, option names, defaults, examples, and cross-document links consistent with code.
- Preserve the document's language, scope, terminology, and level of detail.
- When removing behavior, remove or revise stale instructions rather than merely appending a warning.
- Keep documentation with the implementation commit when both complete the same functional objective.

After editing, search for stale names and contradictory descriptions in `README.md`, `Doc/`, and component-specific README files. If no update is needed after inspection, report that the relevant documentation was checked and why it remains accurate.
