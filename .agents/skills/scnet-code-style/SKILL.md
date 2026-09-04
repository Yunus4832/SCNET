---
name: scnet-code-style
description: "Apply and verify SCNET C# code style, file organization, namespace placement, project ownership, and formatting when creating, editing, moving, or reviewing C# and project files; use before completing any C# change."
---

# SCNET Code Style

Treat the repository `.editorconfig` as the source of truth for mechanically expressible style. A successful build does not prove code-style compliance because SCNET does not yet enforce every IDE diagnostic during compilation.

## Editing

- Read the root `.editorconfig` before editing C# or project files. Follow its brace, `var`, namespace, using, naming, encoding, line-ending, indentation, and final-newline rules while writing code; do not postpone obvious cleanup until validation.
- Preserve the style of surrounding code where `.editorconfig` is silent. Do not reformat unrelated legacy files or mix repository-wide cleanup into a functional change.
- Keep a file named after its primary top-level type. Small private implementation types may remain with their sole owner; split independently reusable or independently testable types into their own files.
- Place code in the narrowest project and directory that owns it. Shared contracts belong in the appropriate lower-level shared project, platform APIs stay in their platform project, and tests mirror the production subsystem they cover.
- Before introducing a project reference or moving a type, inspect nearby project references and the solution order. Do not create an upward dependency from infrastructure or protocol projects into game, UI, server, or platform layers.
- Prefer extending an established subsystem directory over creating generic `Helpers`, `Common`, or `Utils` dumping grounds. Name directories and namespaces for responsibility, not implementation technique.

## Validation

After any C# edit, run the repository helper from the repository root:

```bash
.agents/skills/scnet-code-style/scripts/validate_changed_csharp.sh
```

The helper performs non-mutating Roslyn style and whitespace checks only for changed C# files, so existing unrelated style debt does not hide or block the current change. Run the narrowest affected build and tests separately under `scnet-change-validation`; compilation is not a substitute for this check.

If validation fails, fix the changed files rather than weakening `.editorconfig`, suppressing the diagnostic, or formatting unrelated files. Use a dedicated cleanup change when intentionally paying down existing repository-wide style debt.

For new or moved `.csproj` files, also inspect the focused diff to confirm target frameworks, platform properties, package references, and project references remain local to the intended project; the C# formatter does not validate project ownership.
