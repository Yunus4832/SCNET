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

## C# code quality

Treat `.editorconfig` as authoritative for C# style. Before completing any change that modifies C#,
use the repository `scnet-code-style` Skill and run
`.agents/skills/scnet-code-style/scripts/validate_changed_csharp.sh`. A successful build does not
replace this changed-file style check. Keep unrelated legacy formatting out of functional changes.

## Build and release workflows

Keep ordinary compilation separate from creation of distributable artifacts:

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
