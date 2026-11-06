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
