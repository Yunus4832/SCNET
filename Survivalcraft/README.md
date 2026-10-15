# SCNET.Survivalcraft

SCNET 游戏运行时及模组开发契约，支持 .NET 10 和 .NET 10 for Android，主命名空间为 `Game`。
包含世界、方块、生物、联网、模组和界面等游戏逻辑；平台启动器与内容发布服务不在本包中。

## 模组引用

```xml
<PackageReference Include="SCNET.Survivalcraft" Version="0.0.0.1" ExcludeAssets="runtime" />
<PackageReference Include="SCNET.ContentTool" Version="0.0.0.1" PrivateAssets="all" />
```

游戏 SDK 及引擎用于编译；实际运行时由游戏宿主提供，不应复制进 `.scpkg`。
ContentTool 是独立的构建工具，负责生成内容包，不会成为游戏 SDK 的运行时依赖。
依赖通过 NuGet 传递，模组项目无需逐一引用 Engine、EntitySystem 或原生资产包。

[模组开发](https://gitee.com/Yunus0712/scnet/blob/main/Doc/Modding.md) · [NuGet 包说明](https://gitee.com/Yunus0712/scnet/blob/main/Doc/NuGet.md)
