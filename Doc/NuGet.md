# NuGet Packages

SCNET uses an explicit package allowlist. Projects are not packable unless their
project file sets `IsPackable` to `true`.

## Runtime packages

| Package | Project | Purpose |
| --- | --- | --- |
| `SCNET.Engine.Core` | `Engine.Core` | Foundational utilities with no engine dependency. |
| `SCNET.Engine.Serialization` | `Engine.Serialization` | Serialization support built on Engine.Core. |
| `SCNET.Engine` | `Engine` | Cross-platform graphics, audio, input, storage, and windowing runtime. |
| `SCNET.EntitySystem` | `EntitySystem` | Entity/component/subsystem and template database runtime. |
| `SCNET.Survivalcraft` | `Survivalcraft` | Game runtime, mod contract, and transitive `.scpak` build target. |

These packages follow project dependency boundaries. Consumers normally reference
only the highest-level package they need. A mod references `SCNET.Survivalcraft`;
it should not list the engine packages separately.

## Template package

`SCNET.ModTemplates` is built by `Survivalcraft.ModTemplates/Survivalcraft.ModTemplates.csproj`.
It contains the template assets from `Survivalcraft.ModTemplates/Survivalcraft.Mod/` and
references the matching `SCNET.Survivalcraft` package from generated projects.
Both packages must be published with the same version; the runtime package version in
the template source is updated as part of the release.

The `.scpak` MSBuild target remains in `SCNET.Survivalcraft` under
`buildTransitive`. This keeps package format behavior versioned with the game runtime
instead of copying build logic into each generated project.

## Projects that are not packages

- Platform starters (`Survivalcraft.Windows`, `Survivalcraft.Linux`, Android projects)
  are applications and should be distributed as platform artifacts.
- Test projects are implementation verification only.
- `VerificationBlockMod` is an integration example and produces `.scpak`, not NuGet.
- `Survivalcraft.ModTemplates/Survivalcraft.Mod/` is template source; only its template packaging project
  produces a NuGet package.

## Local packing

```bash
dotnet pack Engine.Core/Engine.Core.csproj -c Release
dotnet pack Engine.Serialization/Engine.Serialization.csproj -c Release
dotnet pack Engine/Engine.csproj -c Release
dotnet pack EntitySystem/EntitySystem.csproj -c Release
dotnet pack Survivalcraft/Survivalcraft.csproj -c Release
dotnet pack Survivalcraft.ModTemplates/Survivalcraft.ModTemplates.csproj -c Release
```

Packages are written to `Publish/NuGet`.

For a clean consumer verification, install the template from that directory, create a
project outside the repository, and restore with `Publish/NuGet` as a package source.
The resulting `.scpak` should contain the mod assembly and its own content, not the
Survivalcraft or engine runtime assemblies.
