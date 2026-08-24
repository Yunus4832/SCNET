---
name: scnet-documentation-maintenance
description: "Assess and maintain SCNET repository documentation and agent guidance when changes may affect documented behavior, configuration, commands, architecture, persistence, networking, modding, build, deployment, platform support, or development workflows. Use while completing such changes or reviewing their documentation impact; do not invoke for edits that clearly cannot affect existing documentation or instructions."
---

# SCNET Documentation Maintenance

Treat documentation impact as part of completing a behavior, architecture, or repository-workflow change. Project documentation includes `README.md`, files under `Doc/`, component-specific README files, `AGENTS.md`, and the maintained guidance under `.agents/skills/`. Search by affected type, option, subsystem, file format, command, tool, workflow, and user-facing term instead of reading every document or Skill.

## Update documentation when

- command-line arguments, commands, configuration fields, defaults, or precedence change;
- GUI, Headless, instance, startup-session, restart, or deployment behavior changes;
- public APIs, mod contracts, package formats, required assets, or extension points change;
- network compatibility, server authority, protocol behavior, or client requirements change;
- persistence formats, file locations, migration, or world-upgrade behavior change;
- build, publish, dependency, target-framework, or supported-platform behavior changes;
- subsystem responsibilities, ownership boundaries, or documented architecture change;
- repository development rules, validation strategy, commit workflow, or agent expectations change;
- a command, script, path, architecture boundary, or workflow described by an existing Skill changes.

Internal refactors, private implementation changes, localized bug fixes that restore already documented behavior, and visual adjustments not described by repository documents usually need no documentation edit.

## Editing rules

- Update only documents affected by the change; do not perform unrelated rewriting or stylistic cleanup.
- Describe the final supported behavior, not the implementation process or temporary debugging history.
- Keep commands, paths, option names, defaults, examples, and cross-document links consistent with code.
- Preserve the document's language, scope, terminology, and level of detail.
- When removing behavior, remove or revise stale instructions rather than merely appending a warning.
- Keep documentation with the implementation commit when both complete the same functional objective.
- Treat `AGENTS.md` and `.agents/skills/**` as maintained project documentation, not static setup files.
- When a Skill becomes stale, update its trigger description as well as its instructions when needed; keep `agents/openai.yaml` consistent with the Skill.
- Use `skill-creator` for material Skill changes and run its structural validation afterward.

After editing, search for stale names and contradictory descriptions in `README.md`, `Doc/`, component-specific README files, `AGENTS.md`, and relevant Skill files. If no update is needed after inspection, report which documentation or agent guidance was checked and why it remains accurate.
