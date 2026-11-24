# SCNET Mod Template

Install the `SCNET.ModTemplates` NuGet package and configure a feed containing the SCNET SDK and ContentTool. Then create and build the solution:

```bash
dotnet new scpkgmod -n ExampleMod --modId example.mod --scope both
dotnet build ExampleMod/ExampleMod.slnx
```

| Scope | Projects | Packaging project |
| --- | --- | --- |
| `server` | `ExampleMod` for authority-side behavior | `ExampleMod` |
| `client` | `ExampleMod.Client` for client presentation | `ExampleMod.Client` |
| `both` (default) | `ExampleMod` for shared rules and `ExampleMod.Client` for client presentation | `ExampleMod.Client` |

Build the solution or its packaging project to create
`bin/<Configuration>/net10.0/packages/example.mod.scpkg` inside that project.

- Register behavior in `ModEntry.cs` and block presentation factories in `ClientContentModule.cs`.
- Keep scope, version and entrypoint declarations in the root `manifest.json`.
- Put content assets under `Assets/` in the solution directory. The `both` scope can also use `Data/` for shared game data.

Import the finished `.scpkg` in the game’s Mods screen and add it to the desired profile.

Generated project files contain explicit SDK, packaging-tool and initial mod versions selected when
the template package was built. Configure the local or remote SDK feed as described in the
[NuGet guide](https://gitee.com/Yunus0712/scnet/blob/main/Doc/NuGet.md).
