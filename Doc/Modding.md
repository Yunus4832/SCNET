# Mod Development

Projects in this solution folder target the new Survivalcraft mod runtime and produce `.scpak` packages.

## Create a project

Install the published template package:

```bash
dotnet new install SCNET.ModTemplates
```

Create and build a mod:

```bash
dotnet new scpakmod -n ExampleMod --modId example.mod
dotnet build ExampleMod/ExampleMod.csproj
```

The package is emitted under:

```text
bin/<Configuration>/<TargetFramework>/packages/<mod-id>.scpak
```

## Package layout

```text
manifest.json
assemblies/*.dll
data/**
assets/<mod-id>/**
```

Generated projects reference `SCNET.Survivalcraft`; its transitive build target
adds the matching compile-time API and creates the `.scpak`. Host runtime assemblies
are not copied into the package.

Inside this repository, the template and verification mod use
`Survivalcraft/Modding/Survivalcraft.Mod.targets` directly so core and mod changes can
be developed together without publishing an intermediate NuGet package.

Template assets live under `Survivalcraft.ModTemplates/Survivalcraft.Mod/`. The only template-related
project in the solution is `Survivalcraft.ModTemplates/Survivalcraft.ModTemplates.csproj`,
which packs those assets into the published `dotnet new` package.
