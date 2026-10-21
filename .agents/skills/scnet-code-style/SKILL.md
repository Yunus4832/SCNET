---
name: scnet-code-style
description: "Apply and verify SCNET C# code style, file organization, namespace placement, project ownership, Roslyn style checks, and JetBrains formatting when creating, editing, moving, or reviewing C# and project files; use before completing any C# change."
---

# SCNET Code Style

Treat the repository `.editorconfig` as the source of truth for mechanically expressible style. A successful build does not prove code-style compliance because SCNET does not yet enforce every IDE diagnostic during compilation.

## Formatter responsibilities

`.editorconfig` separates basic EditorConfig options, Microsoft/Roslyn C# rules, and JetBrains
`resharper_` formatting extensions. Rider, ReSharper and CleanupCode understand the latter;
`dotnet format` ignores them. JetBrains is the canonical layout formatter; Roslyn is used for
syntax/naming style diagnostics, not as a second whitespace formatter. Their layout defaults differ
for constructs such as LINQ, initializers and anonymous methods; do not alternate them or claim
a Roslyn check validates all formatting preferences.

For field/property spacing and declaration wrapping, use a JetBrains formatter. Keep short signatures
compact; long or already multiline ordinary/primary constructor and record parameter lists use
one parameter per line, with the closing parenthesis on its own line. Apply the configured rules
rather than manually calculating platform- or editor-specific layouts. Long method/constructor calls
also use one argument per line, including target-typed `new(...)`, with the closing parenthesis
on its own line; short calls remain compact. Long binary-expression chains are chopped one operand
per line, with the operator at the end of the preceding line; in boolean chains this keeps
each `&&` / `||` condition separate. Short expressions remain compact. Method expression bodies (`=>`) are reserved for methods that
fit on one line including the declaration; expand longer ones to ordinary block bodies. Roslyn
`when_on_single_line` checks expression layout, not total declaration width, so it is not enabled
as an enforcement rule; review that width explicitly; do not mistake this for a lambda rule or merely move the expression below the arrow.

Rider MCP `reformat_file` can be tried, but verify actual output: returning `ok` alone does not prove
that C# formatting has completed. Changes may appear after the response; wait for files to settle
before inspecting or starting another formatter. If expected changes still do not appear, use CleanupCode. File mode
(`jb cleanupcode File1.cs File2.cs`) performs formatting only without loading the solution. If using
solution mode, explicitly select `--profile="Built-in: Reformat Code"`; the default Full Cleanup
can make unrelated syntax changes. Keep formatting separate from semantic/code-style fixes such
as primary-constructor conversion, and review those fixes independently.

For bulk formatting, save the pre-format working tree (including user edits) under `.agent-work/`,
exclude generated files and dependency copies, inspect representative output before expanding,
and verify that formatting does not change code tokens, literal values or preprocessor directives.
Compare against that saved baseline, not just HEAD when earlier edits are pending. Check stability
with a second formatting pass; inspect conflicts between formatters instead of alternating them.

## Enum values

Enum members must declare explicit constant values, including the first/zero member. Keep existing
numeric values when adding initializers; preserve aliases, signed/unsigned ranges and intentional
Flags expressions. Do not renumber protocol or persisted enums as a style change. This is a syntax
rule, not a built-in `.editorconfig` formatting option.

The changed-file gate invokes the skill-owned .NET 10 file-based Roslyn checker
`scripts/check_explicit_enum_values.cs`; no analyzer dependency is added to product projects.
It checks conditional compilation branches as well. For a repository-wide check, run:

```bash
dotnet run --file .agents/skills/scnet-code-style/scripts/check_explicit_enum_values.cs -- --all
```

Run `bash .agents/skills/scnet-code-style/scripts/test_explicit_enum_values.sh` after changing
the checker to verify explicit values, Flags/aliases, integer boundaries, non-code text and inactive branches.

The checker reports `SCNETSTYLE001` through the command line. It does not install a live IDE diagnostic.
The first invocation restores its pinned Roslyn package; subsequent runs use the .NET/NuGet caches.

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

The helper checks explicit enum initializers, validates JetBrains formatting on temporary copies,
then verifies Roslyn syntax/naming style for changed files. The formatting check leaves the working
tree untouched and preserves nested EditorConfig scopes. The JetBrains CLI is required for this gate but not ordinary builds. Install it as documented in
`Doc/BuildAndConfig.md`; set `SCNET_JB` when
its `jb` executable is not on PATH. A missing formatter is reported as a failed check, not silently
skipped. Do not run `dotnet format whitespace` as an additional gate against JetBrains-owned layout.
Run the narrowest affected build and tests separately under `scnet-change-validation`; compilation
is not a substitute for these checks.

If validation fails, fix the changed files rather than weakening `.editorconfig`, suppressing the diagnostic, or formatting unrelated files. Use a dedicated cleanup change when intentionally paying down existing repository-wide style debt.

For new or moved `.csproj` files, also inspect the focused diff to confirm target frameworks, platform properties, package references, and project references remain local to the intended project; the C# formatter does not validate project ownership.
