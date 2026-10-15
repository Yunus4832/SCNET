# SCNET.EntitySystem

SCNET 的实体、组件、子系统及模板数据库运行时，面向 .NET 10。
包名为 `SCNET.EntitySystem`，程序集名为 `EngineSystem`。

- `EntitySystem.Core`：`Project`、`Entity`、`Component`、`Subsystem` 与生命周期。
- `EntitySystem.TemplatesDatabase`：模板数据库、`ValuesDictionary` 及项目数据。
- 依赖 `SCNET.Engine.Serialization` 和 MessagePack，不依赖游戏或窗口运行时。

MessagePack 用于项目数据和网络数据序列化。
使用游戏 SDK 编写模组时会自动获得本包，无需重复引用。
