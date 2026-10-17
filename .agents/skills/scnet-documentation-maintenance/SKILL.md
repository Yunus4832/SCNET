---
name: scnet-documentation-maintenance
description: "Audit and maintain SCNET project documentation, README files, links and agent guidance, or assess documentation impact when changes affect documented behavior, configuration, commands, architecture, persistence, networking, modding, build, deployment, platform support, or development workflows. Use while completing such changes or reviewing their documentation impact; do not invoke for edits that clearly cannot affect existing documentation or instructions."
---

# SCNET Documentation Maintenance

Treat documentation impact as part of completing a behavior, architecture, or repository-workflow change. Project documentation includes `README.md`, files under `Doc/`, component-specific README files, `AGENTS.md`, and the maintained guidance under `.agents/skills/`. Search by affected type, option, subsystem, file format, command, tool, workflow, and user-facing term instead of reading every document or Skill.

Temporary implementation plans, execution logs, and agent drafts are not formal documentation.
Use `scnet-agent-workspace` to keep them under the ignored `.agent-work/<task-id>/` directory;
promote only durable supported behavior into project docs, without links that depend on local notes.

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

## Repository-wide reviews

For an explicit full review, inventory tracked Markdown, project README files, skill entrypoints and
references, plus non-Markdown specifications or examples. Exclude generated outputs, dependency
copies, licenses and game data from prose review, but validate referenced schemas/vectors as data.
Keep coverage and unresolved findings in `.agent-work/`; a successful link scan is not evidence that
the documents were read or their behavior verified.

Use [Doc/README.md](../../../Doc/README.md) as the human-facing topic index. Keep responsibilities clear:
root README for orientation, component README for consuming that component, `Doc/` for durable contracts
and architecture, skills for Agent operation, ignored workspace for environments and execution evidence.
Put detailed API inventories in their reference page rather than mixing them into deployment instructions.
Link to the owning source of a rule instead of maintaining multiple copies of constants and procedures.

Trace claims that commonly drift to their actual owners: project versions and package imports,
MSBuild target timing, CLI parsers, endpoint attributes and response shapes, settings serializers,
storage locations, lifecycle and permission definitions. Distinguish an implemented capability,
a lower-level API without a UI entry, and an unimplemented design. If source contradicts itself,
record the limitation; do not change the business contract merely to make documentation agree.

For references and basic Markdown structure, run from the repository root:

```bash
python3 .agents/skills/scnet-documentation-maintenance/scripts/check_documents.py
```

The Python standard-library helper checks common inline/reference links, local heading fragments,
current-repository `blob/main` targets, fences, heading spacing and trailing whitespace. It reads
tracked and non-ignored untracked Markdown, excluding code-fenced examples. It is not a complete
Markdown parser and does not contact external sites; review unusual markup and external references
separately. Check script behavior with a temporary good/broken fixture after changing it.

Before finishing, reconcile the inventory with coverage, check navigation to every formal topic,
validate modified skills using `skill-creator`, and inspect the final diff. Package/bundle README edits
can alter shipped artifacts; apply the commit skill's version rules when preparing a commit rather
than assuming every documentation edit is outside release versioning.
