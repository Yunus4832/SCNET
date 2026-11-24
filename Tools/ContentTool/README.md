# SCNET.ContentTool

SCNET 内容包命令行工具，以及模组构建时生成 `.scpkg` 的 MSBuild 集成，面向 .NET 10。

## 模组项目引用

```xml
<PackageReference Include="SCNET.ContentTool" Version="CONTENT_TOOL_VERSION" PrivateAssets="all" />
```

将 `CONTENT_TOOL_VERSION` 替换为包源中可用的版本。仓库版本由 `SharedProperties/Versions.props` 的 `ContentToolVersion` 管理。

NuGet 自动导入 `build/SCNET.ContentTool.targets`。默认在模组构建后读取项目的
`manifest.json`，收集程序集、`Data` 和 `Assets`，按清单标识和版本生成 `<mod-id>-<version>.scpkg`。
共享规则模组通过 `SCNET.Survivalcraft.Hosting` 引用 SDK，客户端表现项目使用 `SCNET.Survivalcraft.Client`。构建工具作为私有依赖引用。

## 命令行

```text
dotnet <包目录>/tools/net10.0/any/SCNET.ContentTool.dll inspect <package.scpkg>
dotnet <包目录>/tools/net10.0/any/SCNET.ContentTool.dll verify <package.scpkg>
dotnet <包目录>/tools/net10.0/any/SCNET.ContentTool.dll pack <manifest.json> <payload-directory> (<output.scpkg> | --output-directory <directory>)
```

工具目录包含完整运行依赖，包括 `.deps.json` 和图像原生库；无需游戏安装目录，
也无需借用模组项目的依赖。当前支持的桌面执行平台为 Linux x64 和 Windows x64。
工具依赖和游戏自带的宿主程序集不应进入最终模组包。

[模组开发](https://gitee.com/Yunus0712/scnet/blob/main/Doc/Modding.md) · [NuGet 包说明](https://gitee.com/Yunus0712/scnet/blob/main/Doc/NuGet.md)

## 共享打包输入

默认从打包项目目录读取 `manifest.json`、`Data/` 和 `Assets/`。客户端打包项目可用以下属性指向解决方案根目录的共享输入：

| 属性 | 输入 |
|---|---|
| `SurvivalcraftModManifest` | 包清单 |
| `SurvivalcraftModDataDirectory` | 规则数据目录 |
| `SurvivalcraftModAssetsDirectory` | 内容资源目录 |
