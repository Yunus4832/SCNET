---
name: scnet-commit-discipline
description: "Prepare, review, split, stage, or commit SCNET changes using the repository's focused commit boundaries, release-intent-based version decisions, searchable version-change bodies, and `prefix: 中文描述` message convention. Use when the user asks to commit changes, organize commits, propose commit messages, or review whether a pending commit is coherent; do not invoke for ordinary code editing that does not involve commit preparation."
---

# SCNET Commit Discipline

Inspect the complete working tree and relevant diff before proposing commit boundaries. Preserve unrelated and user-owned changes. Do not stage or commit unless the user authorizes that action.

## Version decisions follow release intent

Keep existing versions during ordinary development, fixes, refactoring, builds, tests, commits,
and pushes. A commit is not a release boundary. Preserve current version numbers; do not reset
or advance them to reduce local package clutter. New release units use their agreed initial version.

The user determines when to make a new release; the agent evaluates which release units and version
increments it needs. An explicit request to release a new version or prepare a new distribution
triggers that evaluation once for the release batch, covering changes since the previous release.
Rebuilding or redeploying an already selected version does not trigger another bump. When a request
such as “package/deploy the latest code” leaves new-release intent unclear, explain the proposed
version decision and clarify that intent before changing versions. A plain commit request never
requires this clarification and keeps versions unchanged.

- At a new release boundary, inspect `SharedProperties/Versions.props`, affected project versions,
  first-party package references, and bundled outputs. Bump affected release units and consumers
  whose released dependencies or bundled binaries changed; do not cascade bumps on every commit.
- During unreleased `0.x` development, a new release batch normally advances PATCH without promising
  compatibility. Advance MINOR for an explicitly chosen development stage. After `1.0`, apply SemVer:
  compatible fixes PATCH, compatible additions MINOR, breaking public contracts MAJOR.
- Preserve user-selected versions and versions already advanced for the same release batch.
  For prereleases, advance the final numeric identifier without silently changing release channels.
- Increment `AndroidReleaseCode` once when preparing a new Android distribution, shared by all ABIs
  in that batch. It is an installation counter, not a SemVer component; ordinary builds do not bump it.
- During the current unreleased stage, wire-protocol and file-format compatibility integers remain
  at `1`, independently of product or package releases, as specified in `AGENTS.md`.
- Local unpublished packages may reuse a version during development. After changing package contents,
  use `Scripts/pre-build.sh --refresh` or `Scripts/pre-build.ps1 -Refresh` to refresh packages and
  caches. Local mod test packages use their full package hash. Do not bump solely to bypass caches.
  Once a package is published to a remote feed, treat that package version as immutable.
- Documentation/tests/skills alone do not require a new release. At release preparation, account for
  README files and other documentation actually bundled in changed artifacts.
- Keep version numbers centralized in `Versions.props`, record changes in the corresponding commit,
  and verify dependency references and artifact names. Explain the release decision and affected
  units. Version changes do not themselves authorize publishing, tagging, or deployment.

## Release changelog

Whenever release versions advance, update the root `CHANGELOG.md` in the same change; create it
on the first such release. Group entries by release batch and date, listing each affected release
unit with its exact version. Summarize cumulative changes since that unit's previous release,
using Git history and diffs rather than only the latest commit or copying commit titles.

Include relevant features, fixes, breaking changes and required user/deployment actions. Separate
independent release units so their version numbers are unambiguous. Group dependency-only changes
concisely. Do not include temporary test artifacts, credentials, or unsupported claims of validation.
If the previous release boundary is unclear, establish it from version records/tags and state any
remaining uncertainty rather than inventing release history. Ordinary commits and same-version
local repacks do not require changelog entries. Historical backfilling is not required by this rule.

The changelog explains the release to users; the commit's `Versions:` block remains the precise,
searchable record of changed version properties. Keep both consistent with `Versions.props`.

## Commit messages

Use this format:

```text
prefix: 中文描述
```

Choose the prefix from the actual change:

- `feat`: add user-visible behavior or capability.
- `fix`: correct a defect.
- `refactor`: restructure without intentionally changing behavior.
- `chore`: maintenance, cleanup, tooling, or repository work not covered above.
- `test`: add or reorganize tests without a corresponding production change.
- `docs`: documentation-only changes.

Follow established history when a more specific existing prefix clearly fits. Write a concise Chinese description of the completed outcome; avoid vague messages such as “更新代码”, “优化内容”, or file-name-only descriptions.

### Searchable version records

When a commit adds, changes, or removes a property in `SharedProperties/Versions.props`, include a
`Versions:` block in its body. Derive entries from the **staged** old/new file, not the whole working
tree or a remembered version. Use the exact property names, including installation counters or
protocol identifiers only when they actually changed. Keep the title focused on the behavior.

```text
feat: 重构领地石范围与玩家授权流程

Versions:
- TerritoryStoneVersion: 0.1.2 -> 0.1.3
```

New properties use `initial -> VALUE`; removed properties use `VALUE -> removed`. List each changed
property once in file order, and record every affected release unit in the same block. For example:

```text
Versions:
- UiPreviewToolVersion: initial -> 0.1.0
```

Do not add a `Versions:` block when the commit has no version-property changes. Do not list unchanged
versions or advance a version merely to populate the block. Before committing, check that the body
matches the staged version diff, especially when multiple commits share `Versions.props` hunks.

A version can then be located without relying on its behavioral commit title:

```bash
git log --all --grep='TerritoryStoneVersion: .* -> 0\.1\.3$'
git show <commit>:SharedProperties/Versions.props
```

Release tags can additionally identify published source when publishing is explicitly requested;
do not create tags for every development commit. Do not rewrite already-pushed history just to add
metadata. Amend unpublished commit messages only when authorized, preserving their source trees and
reporting changed commit identities.

## Commit boundaries

A commit should be independently understandable, focused, and complete:

- Keep one behavioral objective and its necessary implementation together.
- Include tests and documentation that complete that same objective when appropriate.
- Separate unrelated features, opportunistic refactors, formatting, generated artifacts, and cleanup.
- Do not split a change so narrowly that an intermediate commit is broken or misleading.
- If the working tree contains multiple objectives, propose an explicit file or hunk grouping before staging. Use patch staging only when a file genuinely contains separable changes and the split can be made safely.

Before committing, inspect `git status --short`, the staged diff, and the unstaged diff. Verify that the staged set matches the intended boundary and contains no secrets, temporary evidence, build outputs, or unrelated files. Report anything intentionally left unstaged.

Temporary agent plans and drafts belong in the ignored `.agent-work/` directory under
`scnet-agent-workspace`, not in documentation commits. Do not force-add them or delete them merely
to make a commit clean; maintained skills and formal documentation remain normal tracked deliverables.
