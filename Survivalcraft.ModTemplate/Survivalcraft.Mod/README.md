# SCNET Mod Template

Create and build a complete mod solution after configuring the NuGet feed containing the SCNET SDK.

```bash
dotnet new scpkgmod -n ExampleMod --modId example.mod
dotnet build ExampleMod/ExampleMod.slnx
```

The solution contains `ExampleMod/ExampleMod.csproj` for shared rules and
`ExampleMod.Client/ExampleMod.Client.csproj` for client content. Rules compile against the Core and
Hosting SDKs. Client content references the rule project and the Client SDK.

The client project packages both assemblies into
`ExampleMod.Client/bin/<Configuration>/net10.0/packages/example.mod.scpkg` inside the generated directory.

- Register shared behavior in `ExampleMod/ModEntry.cs`.
- Register block presentation factories in `ExampleMod.Client/ClientContentModule.cs`.
- Keep metadata, version and both entrypoint declarations in the root `manifest.json`.
- Put shared package data under `Data/` and content assets under `Assets/` in the solution directory.
- Build the solution or its client project to create the complete package.

The shared entrypoint runs in game hosts. The client content entrypoint is loaded by the graphical
application. Both entrypoints belong to the same mod ID and package.

Generated project files contain explicit SDK, packaging-tool and initial mod versions selected when
the template package was built. Configure the local or remote SDK feed as described in the
[NuGet guide](https://gitee.com/Yunus0712/scnet/blob/main/Doc/NuGet.md).
