# SCNET project file style

Apply these conventions when editing `.csproj`, `.props`, `.targets`, `.esproj` and `.slnx` files.
Read the current diff first when the user has already reorganized the projects. Preserve the
user's grouping and focus changes on the requested scope.

## Layout and grouping

- Follow `.editorconfig` for indentation, encoding, line endings and the final newline.
- Separate top-level configuration blocks with a blank line. Remove empty groups and runs of
  empty lines inside groups.
- Keep ordinary `PackageReference` and `ProjectReference` items in separate groups. Place package
  references before project references when their evaluation order permits it.
- Group assembly attributes and metadata separately from dependencies and resource declarations.
  Keep embedded resources, linked sources, copied content and NuGet package files grouped by purpose.
- Prefer basic project properties first, followed by dependencies, specialized conditional items,
  metadata/resources and custom targets. Existing property dependencies and import placement
  determine the actual order.
- Keep related platform-specific settings together. A conditional group may contain a platform's
  compile exclusions and its package references, as in `Engine.csproj`.
- Keep short items on one line and use `/>` for empty elements. Wrap long attribute lists with
  continuation attributes aligned. Keep nested metadata indented beneath its owning item.
- Preserve a meaningful existing item order. Alphabetical sorting is optional and requires checking
  whether the consuming target depends on that order.

## Paths

Use `/` for repository-relative paths, imports, globs, `Link`, `PackagePath` and solution project
paths. The NuGet package root is `PackagePath="/"`.

Keep property anchors appropriate to the file's owner. For example, shared imports and assets can
use `$(MSBuildThisFileDirectory)`, while project-owned outputs can use `$(MSBuildProjectDirectory)`.
Preserve trailing separators where later expressions concatenate a filename directly.

Check string operations and `Exec` commands separately when normalizing paths. A Windows API may
return native separators even when its input uses `/`. Prefer a direct anchored path when it removes
the need for separator-dependent trimming. Preserve literal escapes in regular expressions,
program arguments and other non-path text.

## MSBuild semantics

A formatting change must preserve property evaluation, import order, conditions, item metadata,
resource names and build target ordering. Inspect `Include`, `Update`, `Remove` and `Exclude`
individually when consolidating groups or changing globs.

Keep special project-reference metadata with its reference. Examples include Analyzer output,
`ReferenceOutputAssembly`, asset exclusion and host-tool property isolation. Review the current
[build configuration](../../../../Doc/BuildAndConfig.md) when editing RID propagation or atlas
build dependencies.

Keep genuine platform and configuration differences in their owning project or conditional group.
Use existing shared properties for common values. New abstractions need a concrete reuse benefit.

## Review and validation

1. Compare the final diff with the user's starting layout and check for accidental semantic changes.
2. Run `git diff --check` and parse changed XML files to catch malformed markup.
3. For path edits, inspect imports, globs, linked resources, output paths and packing destinations.
4. Select builds through `scnet-change-validation`. Pure whitespace changes usually need static
   validation. Path and metadata changes need the affected build or packaging path exercised.
5. For build-graph or RID changes, include a clean-output scenario. Check parallel `Rebuild` when
   concurrent cleaning or a shared tool is involved, and verify resource/native asset selection.

The C# changed-file checker covers C# rules. Review project XML using this reference and the
applicable build checks.
