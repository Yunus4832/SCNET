---
name: scnet-commit-discipline
description: "Prepare, review, split, stage, or commit SCNET changes using the repository's focused commit boundaries and `prefix: 中文描述` message convention. Use when the user asks to commit changes, organize commits, propose commit messages, or review whether a pending commit is coherent; do not invoke for ordinary code editing that does not involve commit preparation."
---

# SCNET Commit Discipline

Inspect the complete working tree and relevant diff before proposing commit boundaries. Preserve unrelated and user-owned changes. Do not stage or commit unless the user authorizes that action.

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
