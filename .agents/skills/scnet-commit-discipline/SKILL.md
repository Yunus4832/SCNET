---
name: scnet-commit-discipline
description: "Prepare, review, split, stage, or commit SCNET changes using the repository's focused commit boundaries, automatic release-version updates, and `prefix: 中文描述` message convention. Use when the user asks to commit changes, organize commits, propose commit messages, or review whether a pending commit is coherent; do not invoke for ordinary code editing that does not involve commit preparation."
---

# SCNET Commit Discipline

Inspect the complete working tree and relevant diff before proposing commit boundaries. Preserve unrelated and user-owned changes. Do not stage or commit unless the user authorizes that action.

## Version decisions are part of committing

Keep existing release versions unchanged during ordinary implementation, local builds, and manual
test iterations. Decide and apply a version bump once when preparing the corresponding commit,
using the last committed version as the baseline. Do not increment on each edit, fix, rebuild, or
test-environment restart. A new release unit still needs its agreed initial version. An explicit
publication request can require a new version before committing; identify that release boundary.
Local test content packages can be distinguished by their full package hash; update the test
profile's exact package identity rather than advancing the product version solely to bypass caches.

Before staging, inspect `SharedProperties/Versions.props` and the affected projects' `Version` and
first-party `PackageReference` properties. Automatically update affected release versions when the
commit changes shipped behavior, APIs, dependencies, native assets, or packaging output. Keep version
changes in the same commit as their cause; do not ask for a separate routine version-bump approval.

- During the current unreleased `0.x` phase, ordinary iterations default to the next patch
  (`0.1.0` → `0.1.1`), without promising compatibility. Advance the minor version for an explicitly
  chosen new development stage. After `1.0`, use SemVer: compatible fixes PATCH, compatible additions
  MINOR, breaking public contracts MAJOR.
- Bump the owning release unit, not every property. Also examine consumers: if this commit changes
  their published dependency metadata or bundled binaries, their release artifacts need new versions
  too. Independent version streams must not publish changed artifacts under an unchanged version.
- Documentation/tests/skills that do not change release artifacts need no version bump. A README
  included in a NuGet package, template, or deployment bundle is part of that artifact; inspect the
  packaging rather than deciding solely from file extension.
- If the relevant version was already explicitly updated in the pending change (including an
  intentional initial reset), retain it instead of incrementing it again. Preserve a user-selected
  version. For a prerelease, increment its final numeric identifier rather than silently switching
  to a stable release; do not infer a new channel.
- When Android application artifacts change, increment `AndroidReleaseCode` once as well, unless
  already advanced in the pending change. It is an installation counter, not a SemVer component.
- Do not derive protocol compatibility identifiers from product bumps. Change `GameProtocolVersion`
  only for an actual incompatible wire-contract change, or when explicitly resetting versions.
- Keep numbers in `Versions.props`; update references, template generation and publication names
  through those properties. Verify impacted project/package versions and describe the bumps in the
  final commit summary. Version changes do not themselves authorize publishing or deployment.

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
