# Survivalcraft Mod Template

Install the template package and create a mod:

```bash
dotnet new install SCNET.ModTemplates
dotnet new scpkgmod -n ExampleMod --modId example.mod
dotnet build ExampleMod/ExampleMod.csproj
```

The package is written to `bin/<Configuration>/<TargetFramework>/packages/example.mod.scpkg` and is verified by the shared content-package SDK during the build.

Generated projects reference the matching `SCNET.Survivalcraft` NuGet package.
When a generated mod project is created inside the SCNET repository, it can switch
to the local Survivalcraft project and build target instead.

- Put code in the project directory.
- Put data contributions under `Data/`.
- Put content assets under `Assets/`; packaging places them under the manifest mod ID namespace.
- Keep `manifest.json`, `SurvivalcraftModId`, assembly name, and entrypoint consistent.
