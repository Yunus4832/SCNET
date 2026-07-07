# 模组开发

本文档面向模组开发者，说明如何创建项目并打包 `.scpak`。

如果只是配置、下载或启用模组，见 [Mods.md](./Mods.md)。

## 创建项目

Install the published template package:

```bash
dotnet new install SCNET.ModTemplates
```

创建并构建模组：

```bash
dotnet new scpakmod -n ExampleMod --modId example.mod
dotnet build ExampleMod/ExampleMod.csproj
```

包输出位置：

```text
bin/<Configuration>/<TargetFramework>/packages/<mod-id>.scpak
```

## 包结构

```text
manifest.json
assemblies/*.dll
data/**
assets/<mod-id>/**
```

模板项目引用 `SCNET.Survivalcraft`。它的构建目标会加入匹配的编译期 API，并在构建后创建 `.scpak`。宿主运行时程序集不会复制进包。

在本仓库内部，模板和验证模组直接使用 `Survivalcraft/Modding/Survivalcraft.Mod.targets`，这样核心代码和模组代码可以一起开发，不需要先发布中间 NuGet 包。

模板资源位于 `Survivalcraft.ModTemplates/Survivalcraft.Mod/`。解决方案里唯一与模板打包直接相关的项目是 `Survivalcraft.ModTemplates/Survivalcraft.ModTemplates.csproj`，它会把这些资源打包成可发布的 `dotnet new` 模板包。

## 示例模组

`VerificationBlockMod/` 是当前端到端验证模组，展示了：

- 代码、数据和 assets 打进同一个 `.scpak`
- 注册自定义方块
- 生命周期日志
- 玩家伤害拦截
- 方块挖掘、放置和世界更新回调

构建：

```bash
dotnet build VerificationBlockMod/VerificationBlockMod.csproj -c Debug
```
