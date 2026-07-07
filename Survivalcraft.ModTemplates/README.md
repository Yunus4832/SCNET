# Templates

This directory contains the template assets and the NuGet packaging project for the
Survivalcraft mod template.

- `Survivalcraft.Mod/` is the template content that `dotnet new` copies into a new mod project.
- `Survivalcraft.ModTemplates.csproj` is the packable project that produces
  `SCNET.ModTemplates.nupkg`.

Build the package with:

```bash
dotnet pack Survivalcraft.ModTemplates/Survivalcraft.ModTemplates.csproj -c Release
```
