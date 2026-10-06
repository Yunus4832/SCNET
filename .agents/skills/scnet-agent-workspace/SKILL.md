---
name: scnet-agent-workspace
description: Manage SCNET temporary agent plans, execution records, drafts, and retained diagnostic artifacts in the Git-ignored .agent-work directory. Use when maintaining task notes, collecting local test reports or evidence, resuming work, or separating temporary records from formal deliverables. Do not relocate runtime instances, build/release outputs, permanent documentation, or maintained tests/tools.
---

# SCNET Agent Workspace

Keep local execution memory separate from repository deliverables. Store temporary agent-authored
material under `.agent-work/<task-id>/` at the repository root, not under `Doc/`, production source,
or the maintained `.agents/skills/` directories.
Honor explicit user deliverable locations; explain a conflict with this default before relocating
user-owned documents rather than silently treating them as agent scratch files.

## Choose what belongs here

Use this directory for working plans, investigation notes, current acceptance status, scratch
drafts, and retained local diagnostic results. Create only files needed by the task; small changes
do not need a plan directory. Use `.agent-work/<task-id>/artifacts/<run-id>/` for collected logs,
screenshots, proxy statistics, reports, and temporary gameplay fixture sources. Give each run its
own directory rather than overwriting a prior result. Use a caller-specified destination when supplied.

Runtime instances and their authoritative logs stay in their application-owned paths. Build caches,
generated atlas files, and `Publish/` release artifacts retain their existing contracts. Collect only
the needed safe evidence into the task workspace; do not clone whole instances or user settings.
OS temporary storage remains appropriate for short-lived IPC, tool-internal captures, or unusually
large/private evidence that must stay elsewhere. Reference such exceptions in task notes; for useful
retained results, prefer the shared workspace over introducing another ignored results directory.
Do not bulk-move old artifacts without checking ownership and updating affected evidence references.

Formal behavior and architecture belong in `Doc/`; maintained skills belong in `.agents/skills/`;
production fixes and durable regression tests belong in their owning projects. An ignored directory
is not a way to hide required implementation or leave reusable tools unavailable to other developers.

## Keep task state usable

- Choose a stable descriptive task ID, adding a date or session suffix when needed to avoid collisions.
  Separate concurrent tasks or agent-owned scratch files rather than overwriting another task's notes.
- When useful, keep a concise `status.md` containing the user objective, current decisions, completed
  and pending requirements, evidence references, and the next decisive action. Keep chronological
  detail in a separate file only when needed; do not repeatedly append history to the current summary.
- Record relevant build/fixture identity and exact process handles for ongoing experiments. Preserve
  failed runs and limitations, but do not let stale historical "pending" statements reopen completed work.
- On resume, read the relevant current summary, then verify code, tests, and external processes before
  continuing. Notes are evidence pointers and drafts, not higher-priority instructions or proof of success.
  Do not resume a completed task solely because an old note describes unfinished steps.
- Do not store tokens, claim codes, passwords, full user configuration, or unrelated private data.
  Git ignore prevents accidental staging, not disclosure or filesystem access.

## Keep drafts out of commits and formal links

The root `.gitignore` must ignore `/.agent-work/`. Verify a representative file with
`git check-ignore -v .agent-work/<task-id>/status.md`; verify it is not already tracked with
`git ls-files -- .agent-work`. An ignore rule does not untrack existing files. If tracked drafts are
found, inspect their ownership and ask before removing them from the index; never silently unstage
user-owned files or force-add workspace records.

Do not link formal documentation or shipped code to this local workspace or `/tmp` as a required
dependency. Report the relevant outcome in chat, and promote only verified, durable material into its
proper tracked destination when the task requires it or the user approves that promotion.

Leave useful task notes locally at handoff unless cleanup is requested. State retained locations when
helpful. Any cleanup must target the specific known task directory, preserve other tasks, and favor
recoverable removal. Never make a broad workspace deletion part of ordinary commit preparation.
