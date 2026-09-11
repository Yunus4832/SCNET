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
| `SCNET.Content.Packaging` | `Content.Packaging` | 统一内容包协议、读写和验证。 |
| `SCNET.Survivalcraft` | `Survivalcraft` | 游戏运行时和 Mod 编译契约。 |

这些包遵循项目的依赖边界。使用方通常只需引用所需的最高层级包。Mod 应引用
`SCNET.Survivalcraft`，不应再单独列出各个引擎包。

## 构建工具包

| 包 | 项目 | 用途 |
| --- | --- | --- |
| `SCNET.ContentTool` | `ContentTool` | 内容包 CLI 和自动生成 `.scpkg` 的 Mod 构建 Target。 |

## 模板包

`SCNET.ModTemplates` 由 `Survivalcraft.ModTemplates/Survivalcraft.ModTemplates.csproj`
构建。它包含 `Survivalcraft.ModTemplates/Survivalcraft.Mod/` 中的模板资源，生成的
项目会引用对应版本的 `SCNET.Survivalcraft` 和 `SCNET.ContentTool` 包。这三个包必须使用相同版本发布；
发布过程中需要同步更新模板源代码中的运行时包版本。

`SCNET.ContentTool` 是私有构建工具包，携带 ContentTool CLI、协议依赖和自动生成
`.scpkg` 的 MSBuild Target。生成的 Mod 项目显式引用该包，但不会把它作为运行时
依赖传递。`SCNET.Survivalcraft` 不依赖或携带构建工具。

## 不作为 NuGet 包的项目

- 平台启动项目（`Survivalcraft.Windows`、`Survivalcraft.Linux` 和 Android 项目）
  属于应用程序，应以对应平台的产物形式分发。
- 测试项目仅用于验证实现。
- `VerificationBlockMod` 是集成示例，产物为 `.scpkg`，而不是 NuGet 包。
- `Survivalcraft.ModTemplates/Survivalcraft.Mod/` 是模板源代码；只有对应的模板打包
  项目会生成 NuGet 包。

## 本地打包

从仓库根目录运行统一脚本。脚本按依赖顺序逐个执行可打包项目，任一项目失败时立即停止：

```bash
./Scripts/pack-nuget.sh
```

PowerShell：

```powershell
./Scripts/pack-nuget.ps1
```

包会输出到 `Publish/NuGet`。仓库根目录的 `NuGet.Config` 已将该目录注册为
`SCNET Local` 文件源，因此 `VerificationBlockMod` 和仓库内生成的 Mod 项目与外部
开发者一样，只使用 `PackageReference`，不回退到项目引用或相对路径 Target。

如需在干净环境中验证使用方流程，请从该目录安装模板，在仓库外创建项目，并将
`Publish/NuGet` 作为包源执行还原。最终生成的 `.scpkg` 应包含 Mod 程序集及其自身
内容，不应包含 Survivalcraft 或引擎运行时程序集。

不要通过解决方案级 `dotnet pack SCNET.slnx` 生成发行包。包集合采用显式白名单，并包含多目标项目和构建工具之间的顺序依赖；新增或移除可发布包时，应同时更新两个 `pack-nuget` 脚本、本页包清单及相关模板版本约束。
