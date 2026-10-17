# SCNET.ContentTool

SCNET 内容包命令行工具，以及模组构建时生成 `.scpkg` 的 MSBuild 集成，面向 .NET 10。

## 模组项目引用

```xml
<PackageReference Include="SCNET.ContentTool" Version="0.1.0" PrivateAssets="all" />
```

NuGet 自动导入 `build/SCNET.ContentTool.targets`。默认在模组构建后读取项目的
`manifest.json`，收集程序集、`Data` 和 `Assets`，调用包内工具生成 `.scpkg`。
游戏 SDK 通过独立的 `SCNET.Survivalcraft` 包引用，工具不会成为模组的运行时依赖。

## 命令行

```text
dotnet <包目录>/tools/net10.0/any/SCNET.ContentTool.dll inspect <package.scpkg>
dotnet <包目录>/tools/net10.0/any/SCNET.ContentTool.dll verify <package.scpkg>
dotnet <包目录>/tools/net10.0/any/SCNET.ContentTool.dll pack <manifest.json> <payload-directory> <output.scpkg>
```

工具目录包含完整运行依赖，包括 `.deps.json` 和图像原生库；无需游戏安装目录，
也无需借用模组项目的依赖。当前支持的桌面执行平台为 Linux x64 和 Windows x64。
工具依赖和游戏自带的宿主程序集不应进入最终模组包。

[模组开发](https://gitee.com/Yunus0712/scnet/blob/main/Doc/Modding.md) · [NuGet 包说明](https://gitee.com/Yunus0712/scnet/blob/main/Doc/NuGet.md)
