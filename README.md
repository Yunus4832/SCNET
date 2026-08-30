# Survivalcraft 联机版

这是一个基于 Survivalcraft 的联机版代码库。它的核心目标不是强化单机客户端，而是让联机服务器稳定运行，同时尽量复用同一套游戏逻辑，减少客户端与服务端之间的代码分叉。

## 概述

仓库的核心思路是将"游戏逻辑"与"平台层"分离：

- `Survivalcraft/` 负责游戏规则、世界、网络、存档、模组和界面逻辑
- `Engine/` 负责渲染、音频、窗口、输入等运行时能力
- `Engine.Core/` 和 `Engine.Serialization/` 负责基础类型与序列化
- `EntitySystem/` 提供实体与组件系统
- 平台项目只负责启动方式、资源打包和宿主差异

这种拆分方式的好处在于：

- 服务器启动时，只需加载真正需要的部分
- 客户端更新游戏规则时，服务端无需单独维护另一份逻辑
- 平台差异不会过多地侵入核心代码

## 项目结构

| 项目 | 说明 |
|------|------|
| `Survivalcraft/` | 核心游戏逻辑，联机规则、世界、网络、模组都在这里 |
| `ModServer/` | 私有 `.scpak` 模组仓库服务 |
| `VerificationBlockMod/` | 当前模组运行时的端到端示例模组 |
| `Survivalcraft.ModTemplates/` | `dotnet new` 模组模板 |
| `Engine/` | 平台能力层，负责窗口、渲染、音频和输入 |
| `Engine.Core/` | 基础类型和运行模式，给各项目共用 |
| `Engine.Serialization/` | 序列化与数据支持 |
| `EntitySystem/` | 实体、组件和模板数据库 |
| `Survivalcraft.Windows/` | Windows 启动器，支持客户端和服务器模式 |
| `Survivalcraft.Linux/` | Linux 启动器，适合部署 Headless 服务器 |
| `Survivalcraft.Android/` | Android 启动器，支持客户端和配置驱动的服务器模式 |
| `Survivalcraft.Android.Arm32/` | Android ARM32 变体 |
| `Survivalcraft.Android.X64/` | Android x64 变体 |
| `Survivalcraft.Android.X86/` | Android x86 变体 |
| `Engine.Test/`、`EntitySystem.Test/`、`Survivalcraft.Test/` | 单元测试工程 |
| `NetworkDamageTool/` | 本机 UDP 网络损伤代理，用于弱网集成实验 |

## 快速开始

### Headless 启动

Headless 模式在不打开游戏窗口的情况下仅运行服务端逻辑，适合将项目作为独立联机服务器部署。

```bash
# Windows
SurvivalcraftStarter.exe --server

# Linux
./SurvivalcraftStarter --server

# 开发环境
dotnet run --project Survivalcraft.Linux/Survivalcraft.Linux.csproj -- --server
```

详细配置与参数说明见 [Headless 模式](Doc/Headless.md)。

### 模组

当前模组系统使用 `.scpak` 包和 `ModProfile` 控制加载范围。包可以来自本地 `Mods` 目录，也可以从私有 `ModServer` 仓库按 `ModId + Version` 下载。

示例模组构建：

```bash
dotnet build VerificationBlockMod/VerificationBlockMod.csproj -c Debug
```

使用和配置说明见 [模组使用说明](Doc/Mods.md)，开发说明见 [模组开发](Doc/Modding.md)。

### 构建

```bash
# 还原依赖
dotnet restore SCNET.slnx

# Debug 构建
dotnet build SCNET.slnx

# Release 构建
dotnet build SCNET.slnx --configuration Release
```

可选构建配置：`Debug`、`Release`、`ANDROID`、`DESKTOP`。详细说明见 [构建与共享配置](Doc/BuildAndConfig.md)。

### 测试

```bash
dotnet test Engine.Test/
dotnet test EntitySystem.Test/
dotnet test Survivalcraft.Test/
```

测试框架为 xUnit + coverlet，测试项目仅在 `Debug` 配置下编译。

## 详细文档

- [结构与分层](Doc/Architecture.md) — 设计重心、分层架构与各层职责
- [Headless 模式](Doc/Headless.md) — 多实例启动、session 参数、游戏模式覆盖、配置与运行时行为
- [启动会话](Doc/StartupSessions.md) — `Starter`、`RunningSetting`、`StartupRequest`、`StartupContext`、`SessionInfo`、覆盖优先级和模组 profile 的职责
- [构建与共享配置](Doc/BuildAndConfig.md) — 构建流程、共享属性与打包行为
- [模组使用说明](Doc/Mods.md) — `.scpak`、本地缓存、profile、仓库和联机 required mods
- [模组开发](Doc/Modding.md) — 模组模板、`.scpak` 构建、示例模组与 NuGet 包边界
- [模组服务器](Doc/ModServer.md) — 私有 `.scpak` 仓库的上传、索引与匿名分发
- [文件定位](Doc/FileStorage.md) — 逻辑路径系统与跨平台文件访问
- [外部依赖](Doc/Dependencies.md) — 工具链、运行时包与测试依赖
- [网络稳定性与容量测试](Doc/NetworkTesting.md) — UDP 损伤代理、弱网实验与并发容量测试边界
- [地形内容分发架构](Doc/TerrainDistribution.md) — 权威内容、客户端派生、版本门禁与线程所有权
- [ContentServer](Doc/ContentServer.md) — 发布者申请、SQLite 存储、内容审核、匿名下载与游戏端安装
- [内容管理迁移临时实施计划](Doc/ContentManagementMigrationPlan.md) — FilePicker、统一内容安装和 ModServer 退役的阶段门禁；验收后删除

## 版权与法律

请先阅读免责声明：[免责声明](Doc/Disclaimer.md)。

## 贡献

1. Fork 本仓库并克隆
2. 基于主分支创建功能分支
3. 提交代码并推送
4. 创建 Pull Request

> 本文档由 AI Agent 编写润色，由 `Yunus0712` 审阅
