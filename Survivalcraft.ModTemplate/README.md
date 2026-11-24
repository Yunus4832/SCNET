# Templates

This directory contains the template assets and the NuGet packaging project for the
Survivalcraft mod template.

- `Survivalcraft.Mod/` is the template content that `dotnet new scpkgmod --scope server|client|both` uses to generate a mod solution.
- `Survivalcraft.ModTemplate.csproj` is the packable project that produces
  `SCNET.ModTemplates.<version>.nupkg`.

Build the package with:

```bash
./Scripts/pack-nuget.sh --project Survivalcraft.ModTemplate
```

Run the command from the repository root after preparing local dependencies. Template packaging
substitutes the independently managed SDK, ContentTool and initial mod versions from
`SharedProperties/Versions.props`; generated projects contain literal package versions.
See [NuGet workflow](https://gitee.com/Yunus0712/scnet/blob/main/Doc/NuGet.md) for local-feed setup.

Install the generated template package using the [creation steps](https://gitee.com/Yunus0712/scnet/blob/main/Doc/Modding.md#创建项目).
The [generated README](Survivalcraft.Mod/README.md) explains where rule code, client content and
package inputs belong.
