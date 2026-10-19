# SCNET Agent Instructions

## Project stage and compatibility

SCNET is currently unreleased and has no external user base or compatibility ecosystem. Treat the
repository's current source, tests, assets, and documentation as one changeable unit.

Do not preserve compatibility by default. When changing APIs, configuration, serialization,
protocols, storage, commands, or architecture:

- prefer the clean current design over deprecated overloads, aliases, adapters, fallback parsers,
  dual read/write paths, or legacy branches;
- update all in-repository callers, tests, fixtures, tools, and documentation in the same change;
- remove superseded code and data paths instead of retaining them for hypothetical downstream users;
- do not add migration or version-detection logic unless a real supported boundary requires it.

Compatibility work is justified only when the user explicitly requests it, when the repository
documents an existing supported compatibility contract, or when interoperability with an external
format, protocol, service, or released dependency requires it. Identify that boundary explicitly
before implementing compatibility. Prefer a finite migration or upgrade step over indefinite
runtime compatibility when either approach satisfies the requirement.

Reassess and update this section when SCNET begins publishing releases, supporting external users,
or maintaining third-party integrations.

## Version changes

Keep existing release versions unchanged during ordinary development, local builds, and testing.
Decide and apply the appropriate bump once while preparing the corresponding commit; follow
`scnet-commit-discipline`. Do not bump on every edit or test restart. New release units retain their
agreed initial version; an explicitly requested publication may require a version change sooner.

## C# code quality

Treat `.editorconfig` as authoritative for C# style. Before completing any change that modifies C#,
use the repository `scnet-code-style` Skill and run
`.agents/skills/scnet-code-style/scripts/validate_changed_csharp.sh`. A successful build does not
replace this changed-file style check. Keep unrelated legacy formatting out of functional changes.

## Build and release workflows

### Temporary local NuGet prerequisites

SCNET has not yet enabled a remote first-party NuGet feed. As a temporary development workflow,
packages are prepared in the local `Publish/NuGet` feed configured in `NuGet.Config`; do not assume
they are available remotely. Generated packages are not distributed through Git, so pulling source
changes alone may leave required versions missing. This extra preparation step is not the intended
long-term workflow. Once a remote feed is adopted, update this guidance and the build documentation
so ordinary development restores published dependencies through the IDE or `dotnet build`, with
local preparation retained only for developing unpublished packages.

The native asset projects deliberately use NuGet consumption: assets under `runtimes/<rid>/native`
are selected by the SDK for the application's RID. A direct `ProjectReference` to those packaging
projects does not provide equivalent native asset propagation. Without an application RID, NuGet
may copy assets for multiple RIDs into separate runtime directories. Keep ordinary managed source
dependencies as project references where already used; this does not require converting them to packages.

- Before the first build, or after pulling changes that introduce missing package versions, run
  `bash Scripts/pre-build.sh` or `pwsh Scripts/pre-build.ps1` from the repository root. These scripts
  check the required local packages, invoke the canonical pack script when any are missing, and
  restore the solution. This dependency preparation is an intentional exception to ordinary builds
  not generating artifacts under `Publish/`.
- If package contents changed without a version change, use `bash Scripts/pre-build.sh --refresh`
  or `pwsh Scripts/pre-build.ps1 -Refresh` to repack and refresh the affected package-version caches.
  Normal preparation checks package presence, not whether source contents changed.
- Once dependencies are prepared, use normal IDE builds or `dotnet build`. Do not hide package
  generation inside ordinary build/restore targets or require a remote feed for local development.
- The native libraries are checked-in precompiled assets; preparing NuGet packages does not require
  rebuilding C/C++ libraries or installing the NDK.

### Compilation and publishing

Keep ordinary compilation separate from creation of distributable artifacts:

- when the user explicitly requests distributable artifacts, load and follow the repository
  `scnet-release-publishing` Skill; do not use it for ordinary builds or validation;
- use `dotnet build` for development and validation; a build must not copy release artifacts into
  the repository `Publish/` directory;
- use `Scripts/publish.sh` on Bash hosts or `Scripts/publish.ps1` on PowerShell hosts to create
  application and service release artifacts;
- use `Scripts/pack-nuget.sh` or `Scripts/pack-nuget.ps1` to create the explicitly allow-listed
  NuGet packages under `Publish/NuGet`;
- do not replace these scripts with solution-level `dotnet publish` or `dotnet pack`: shared
  multi-target projects are intentionally published or packed through sequential project-specific
  commands, and individual entries may require different parameters.

When adding, removing, or changing a release entry, update both platform variants of the relevant
script and the corresponding documentation in `Doc/BuildAndConfig.md`, `Doc/NuGet.md`, or
`Doc/ContentServer.md` in the same change.
