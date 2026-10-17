# Survivalcraft Mod Template

Install the prepared template package and create a mod (replace the path and version):

```bash
dotnet new install /path/to/SCNET.ModTemplates.<version>.nupkg
dotnet new scpkgmod -n ExampleMod --modId example.mod
dotnet build ExampleMod/ExampleMod.csproj
```

The package is written to `bin/<Configuration>/<TargetFramework>/packages/example.mod.scpkg` and is verified by the shared content-package SDK during the build.

Configure the NuGet feed containing the required SCNET packages before building. The repository
currently uses a local feed; see the
[NuGet guide](https://gitee.com/Yunus0712/scnet/blob/main/Doc/NuGet.md#本地打包).

Generated projects reference the selected `SCNET.Survivalcraft` NuGet package for
the compile-time API and privately reference `SCNET.ContentTool` for `.scpkg` packaging.

- Put code in the project directory.
- Put data contributions under `Data/`.
- Put content assets under `Assets/`; packaging places them under the manifest mod ID namespace.
- Keep `manifest.json`, `SurvivalcraftModId`, assembly name, and entrypoint consistent.
