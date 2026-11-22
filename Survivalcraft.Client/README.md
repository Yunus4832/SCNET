# SCNET Survivalcraft Client

Client input, views, widgets, presentation components, terrain meshing, rendering, audio,
particle effects and GPU resource ownership. The assembly targets desktop and Android.
It depends on Engine, Core, Hosting and the protocol contracts. Hosting supplies shared
connection, command and CPU content services and has no dependency on Client.

`ClientApplication` owns client content, world presentation, frame telemetry and platform URI
requests. Each client world has its own presentation resources and mod services. Application
startup, platform registration and screens that assemble application services belong to the
integrated application. Content resources are installed before CPU content data initializes
preview rendering, and are released before the owning CPU mod environment is retired.

## Development entry points

- Use [UiPreviewTool](../Tools/UiPreviewTool/README.md) to inspect isolated widgets and localized layouts.
- Use [game automation](../Doc/Automation.md) to verify input, focus and interaction in a running GUI client.
- See [terrain distribution](../Doc/TerrainDistribution.md#线程所有权) for CPU mesh work, GPU upload and the thread ownership boundary.
- Client-side mod entrypoints and block presentation registration are described in the [modding guide](../Doc/Modding.md#模组定义与世界状态).
