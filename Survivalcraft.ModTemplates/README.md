# Templates

This directory contains the template assets and the NuGet packaging project for the
Survivalcraft mod template.

- `Survivalcraft.Mod/` is the template content that `dotnet new` copies into a new mod project.
- `Survivalcraft.ModTemplates.csproj` is the packable project that produces
  `SCNET.ModTemplates.nupkg`.

Build the package with:

```bash
./Scripts/pack-nuget.sh --project Survivalcraft.ModTemplates
```

Run the command from the repository root after preparing local dependencies. Template packaging
substitutes the independently managed SDK, ContentTool and initial mod versions from
`SharedProperties/Versions.props`; generated projects contain literal package versions.
See [NuGet workflow](https://gitee.com/Yunus0712/scnet/blob/main/Doc/NuGet.md) for local-feed setup.
