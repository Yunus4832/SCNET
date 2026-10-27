# SCNET.EntitySystem

SCNET 的实体、组件、子系统及模板数据库运行时，面向 .NET 10。
包名为 `SCNET.EntitySystem`，程序集名为 `EngineSystem`。

- `EntitySystem.Core`：`Project`、`Entity`、`Component`、`Subsystem` 与生命周期。
- `EntitySystem.TemplatesDatabase`：模板数据库、`ValuesDictionary`、格式类型短名 `TemplateTypeNames` 及项目数据。
- 依赖 `SCNET.Engine.Core` 和 MessagePack，不依赖游戏或窗口运行时。

MessagePack 用于项目数据和网络数据序列化。
使用游戏 SDK 编写模组时会自动获得本包，无需重复引用。

XML 的结构和类型短名由实体系统定义；值的文本转换复用 `Engine.Core.Text.Conversion`。
`ProceduralValue` 的转换器保留在实体系统，游戏特有的 `EntityReference` 转换器仍由游戏实现。
