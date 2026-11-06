# Survivalcraft 联机版

这是一个基于 Survivalcraft 的联机版代码库。项目以联机服务器的稳定运行和跨平台共享游戏逻辑为重点，减少客户端与服务端之间的代码分叉。

## 概述

仓库的核心思路是将"游戏逻辑"与"平台层"分离：

- `Survivalcraft.Core/` 负责游戏规则、实体、权威世界与客户端副本算法
- `Survivalcraft.Hosting/` 负责非图形宿主服务，包括网络、存档、内容及模组装配，由图形程序和独立服务器共享
- `Survivalcraft.Client/` 负责渲染、输入与客户端表现
- `Survivalcraft/` 负责图形应用装配、界面流程及本机世界运行
- `Survivalcraft.Server/` 负责独立服务器的启动、更新循环和进程生命周期
- `Engine/` 负责渲染、音频、窗口、输入等运行时能力
- `Engine.Core/` 负责基础类型、文本值转换和通用运行时类型查找
- `EntitySystem/` 提供实体与组件系统
- 平台项目只负责启动方式、资源打包和宿主差异

这种拆分方式的好处在于：

- 服务器启动时，只需加载真正需要的部分
- 客户端更新游戏规则时，服务端无需单独维护另一份逻辑
- 平台差异不会过多地侵入核心代码

## 项目结构

| 项目 | 说明 |
|------|------|
| `Survivalcraft.Core/` | 无图形依赖的游戏规则、实体、地形和副本算法 |
| `Survivalcraft.Hosting/` | 图形程序和独立服务器共享的非图形宿主服务 |
| `Survivalcraft.Client/` | 客户端渲染、输入和表现组件 |
| `Survivalcraft/` | 图形应用装配与界面流程 |
| `Content.Packaging/` | 位于 Protocol 层的统一 `.scpkg` 协议、校验和 payload codec |
| `ServerSource.Protocol/` | 位于 Protocol 层的服务器源 HTTP 契约、校验器和参考客户端 |
| `ContentServer/` | 统一 `.scpkg` 内容发布服务及内置服务器源参考实现 |
| `ContentWebUI/` | ContentServer 的发布者和管理员 Web 工作台 |
| `Mods/Beacon/`、`Mods/TerritoryStone/` | 独立开发和测试的示例模组 |
| `Survivalcraft.ModTemplate/` | `dotnet new` 模组模板 |
| `Engine/` | 平台能力层，负责窗口、渲染、音频和输入 |
| `Engine.Core/` | 基础类型和运行模式，给各项目共用 |
| `Engine.Media/` | 独立的图像、音频、模型数据与编解码，使用流接口，无窗口和设备依赖 |
| `Native.ImageCodecs/` | 图像原生资产 NuGet 包，按 RID 提供依赖 |
| `Native.OpenAL/` | Windows、Linux 和 Android 的 OpenAL 原生资产 NuGet 包 |
| `Native.SDL/` | Windows、Linux 和 Android 的 SDL 原生资产包及 Android Java 资源 |
| `EntitySystem/` | 实体、组件和模板数据库 |
| `Survivalcraft.Windows/` | Windows 图形程序启动器 |
| `Survivalcraft.Linux/` | Linux 图形程序启动器 |
| `Survivalcraft.Server/` | Windows/Linux 独立游戏服务器入口 |
| `Survivalcraft.Android/` | Android 图形程序启动器，支持游戏内开服 |
| `Survivalcraft.Android.Arm32/` | Android ARM32 变体 |
| `Survivalcraft.Android.X64/` | Android x64 变体 |
| `Survivalcraft.Android.X86/` | Android x86 变体 |
| `Engine.Test/`、`EntitySystem.Test/`、`Survivalcraft.Test/` | 单元测试工程 |
| `Engine.Network/` | 独立网络宿主、连接生命周期、邀请与中继路径 |
| `Game.Protocol/` | 游戏消息契约、帧格式和显式编解码 |
| `Authentication.Protocol/` | 外部认证的签名事务、授权和准入契约 |
| `Authentication.Server/` | 可独立部署的外部认证参考服务 |
| `Rendezvous.Protocol/` | 邀请信令及认证中继报文契约 |
| `RendezvousServer/` | 独立信令与中继服务 |
| `NetworkProbe/` | 基于 Engine API 的网络验证驱动器 |
| `NetworkDamageTool/` | 本机 UDP 网络损伤代理，用于弱网集成实验 |

## 快速开始

完整解决方案构建需要 Node.js 与 npm；首次构建先安装 ContentWebUI 的前端依赖，见[外部依赖](Doc/Dependencies.md)。

首次从源码运行前，先按下方构建步骤生成本地原生资产和 Mod SDK 包。

### 独立服务器启动

独立服务器在不打开游戏窗口的情况下仅运行服务端逻辑，适合将项目作为独立联机服务器部署。

```bash
# Windows
Survivalcraft.Server.exe

# Linux
./Survivalcraft.Server

# 开发环境
dotnet run --project Survivalcraft.Server/Survivalcraft.Server.csproj
```

详细配置与参数说明见 [独立服务器](Doc/Server.md)。

### 模组

当前模组运行时使用统一 `.scpkg` 内容包和带精确 PackageHash 的 `ModProfile` 控制加载范围。包统一进入内容寻址缓存；
客户端可以配置多个匿名 ContentServer 仓库，启动补全和在线内容页共用缓存优先、同 hash 来源回退的下载服务。

领地石模组构建：

```bash
dotnet build Mods/TerritoryStone/TerritoryStone.slnx -c Debug
```

首次构建或更新 Mod SDK 后，需要先按 [NuGet 包](Doc/NuGet.md#本地打包) 将当前版本
生成到仓库本地包源。

使用和配置说明见 [模组使用说明](Doc/Mods.md)，开发说明见 [模组开发](Doc/Modding.md)。

### 构建

```bash
# 首次构建：准备本地包并还原依赖
npm --prefix ContentWebUI ci
./Scripts/pre-build.sh

# Debug 构建
dotnet build SCNET.slnx

# Release 构建
dotnet build SCNET.slnx --configuration Release

# 顺序生成应用和服务发行产物
./Scripts/publish.sh

# 生成 Mod SDK 和模板 NuGet 包
./Scripts/pack-nuget.sh
```

PowerShell 首次构建运行 `./Scripts/pre-build.ps1`。脚本检查当前版本的本地包，缺包时按顺序打包，
随后还原解决方案；已有包时直接还原。修改 SDK 或原生依赖后，运行 `./Scripts/pre-build.sh --refresh`
（PowerShell 为 `./Scripts/pre-build.ps1 -Refresh`），重新打包并刷新这些包的当前版本缓存。
准备完成后可在 IDE 中构建和调试。

PowerShell 发行脚本分别为 `./Scripts/publish.ps1` 和 `./Scripts/pack-nuget.ps1`。可选构建配置为 `Debug`
和 `Release`；平台开发和运行通过选择对应的桌面或 Android 启动项目完成。详细说明见
[构建与共享配置](Doc/BuildAndConfig.md) 和 [NuGet 包](Doc/NuGet.md)。

### 测试

```bash
dotnet test Engine.Test/
dotnet test EntitySystem.Test/
dotnet test Survivalcraft.Test/
```

测试框架为 xUnit + coverlet。解决方案的 `Release` 构建排除测试项目；
协议、服务、桌面更新事务和工具各有对应的测试项目，可按改动范围选择运行。

## 详细文档

完整分类导航见 [文档索引](Doc/README.md)，下列链接提供常用入口。

- [结构与分层](Doc/Architecture.md) — 设计重心、分层架构与各层职责
- [独立服务器](Doc/Server.md) — 多实例启动、session 参数、游戏模式覆盖、配置与运行时行为
- [启动会话](Doc/StartupSessions.md) — `Starter`、`RunningSetting`、`StartupRequest`、`StartupContext`、`SessionInfo`、覆盖优先级和模组 profile 的职责
- [构建与共享配置](Doc/BuildAndConfig.md) — 构建流程、共享属性与打包行为
- [模组使用说明](Doc/Mods.md) — `.scpkg`、本地缓存、profile、仓库和联机 required mods
- [模组开发](Doc/Modding.md) — 模组模板、`.scpkg` 构建、示例模组与 NuGet 包边界
- [ContentServer](Doc/ContentServer.md) — 内容发布、审核、匿名目录、包分发、公告与游戏版本
- [信令服务部署](Doc/RendezvousDeployment.md) — 邀请、中继、可移植发行物和网络探针
- [ContentServer 部署](Doc/ContentServerDeployment.md) — 发行物、容器、HTTP/HTTPS、持久化与升级
- [文件定位](Doc/FileStorage.md) — 逻辑路径系统与跨平台文件访问
- [外部依赖](Doc/Dependencies.md) — 工具链、运行时包与测试依赖
- [网络稳定性与容量测试](Doc/NetworkTesting.md) — UDP 损伤代理、弱网实验与并发容量测试边界
- [地形内容分发架构](Doc/TerrainDistribution.md) — 权威内容、客户端派生、版本门禁与线程所有权

## 版权与法律

请先阅读免责声明：[免责声明](Doc/Disclaimer.md)。

## 贡献

1. Fork 本仓库并克隆
2. 基于主分支创建功能分支
3. 提交代码并推送
4. 创建 Pull Request
