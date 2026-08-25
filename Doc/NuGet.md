# NuGet 包

SCNET 使用显式的包白名单。只有在项目文件中将 `IsPackable` 设置为 `true`，
项目才允许打包。

## 运行时包

| 包 | 项目 | 用途 |
| --- | --- | --- |
| `SCNET.Engine.Core` | `Engine.Core` | 不依赖引擎的基础工具。 |
| `SCNET.Engine.Serialization` | `Engine.Serialization` | 基于 Engine.Core 构建的序列化支持。 |
| `SCNET.Engine` | `Engine` | 跨平台图形、音频、输入、存储和窗口运行时。 |
| `SCNET.EntitySystem` | `EntitySystem` | 实体、组件、子系统和模板数据库运行时。 |
| `SCNET.Survivalcraft` | `Survivalcraft` | 游戏运行时、Mod 契约和传递式 `.scpak` 构建目标。 |

这些包遵循项目的依赖边界。使用方通常只需引用所需的最高层级包。Mod 应引用
`SCNET.Survivalcraft`，不应再单独列出各个引擎包。

## 模板包

`SCNET.ModTemplates` 由 `Survivalcraft.ModTemplates/Survivalcraft.ModTemplates.csproj`
构建。它包含 `Survivalcraft.ModTemplates/Survivalcraft.Mod/` 中的模板资源，生成的
项目会引用对应版本的 `SCNET.Survivalcraft` 包。这两个包必须使用相同版本发布；
发布过程中需要同步更新模板源代码中的运行时包版本。

`.scpak` 的 MSBuild 目标保留在 `SCNET.Survivalcraft` 包的 `buildTransitive` 下。
这样可以让包格式行为随游戏运行时一同进行版本管理，而不必将构建逻辑复制到每个
生成的项目中。

## 不作为 NuGet 包的项目

- 平台启动项目（`Survivalcraft.Windows`、`Survivalcraft.Linux` 和 Android 项目）
  属于应用程序，应以对应平台的产物形式分发。
- 测试项目仅用于验证实现。
- `VerificationBlockMod` 是集成示例，产物为 `.scpak`，而不是 NuGet 包。
- `Survivalcraft.ModTemplates/Survivalcraft.Mod/` 是模板源代码；只有对应的模板打包
  项目会生成 NuGet 包。

## 本地打包

```bash
dotnet pack Engine.Core/Engine.Core.csproj -c Release
dotnet pack Engine.Serialization/Engine.Serialization.csproj -c Release
dotnet pack Engine/Engine.csproj -c Release
dotnet pack EntitySystem/EntitySystem.csproj -c Release
dotnet pack Survivalcraft/Survivalcraft.csproj -c Release
dotnet pack Survivalcraft.ModTemplates/Survivalcraft.ModTemplates.csproj -c Release
```

包会输出到 `Publish/NuGet`。

如需在干净环境中验证使用方流程，请从该目录安装模板，在仓库外创建项目，并将
`Publish/NuGet` 作为包源执行还原。最终生成的 `.scpak` 应包含 Mod 程序集及其自身
内容，不应包含 Survivalcraft 或引擎运行时程序集。
