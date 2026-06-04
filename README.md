# Survivalcraft 联机版

这是一个基于 Survivalcraft 的联机版代码库。它的核心目标不是强化单机客户端，而是让联机服务器能够稳定运行，并尽量复用同一套游戏逻辑，减少客户端与服务端之间的代码分叉。

## 为什么自述文件中有那么多的技术内容？

这是因为这个仓库的重心就是生存战争联机的实现和维护，因此，游戏的本身的内容请自行探索或者通过其他途径了解。

仓库的设计重点不在于游戏本身，而在于：

- 让服务器可以直接以 Headless 方式运行，不依赖图形界面
- 让桌面端、Android 端和服务端尽量共享同一套游戏逻辑
- 让平台差异尽量集中在启动器和少量共享配置中
- 让联机相关功能的维护无需在多个工程中重复修改

## 概述

仓库的核心思路是将“游戏逻辑”与“平台层”分离：

- `Survivalcraft/` 负责游戏规则、世界、网络、存档、模组和界面逻辑
- `Engine/` 负责渲染、音频、窗口、输入等运行时能力
- `Engine.Core/` 和 `Engine.Serialization/` 负责基础类型与序列化
- `EntitySystem/` 提供实体与组件系统
- 平台项目只负责启动方式、资源打包和宿主差异

这种拆分方式的好处在于：

- 服务器启动时，只需加载真正需要的部分
- 客户端更新游戏规则时，服务端无需单独维护另一份逻辑
- 平台差异不会过多地侵入核心代码

## 先看 Headless

Headless 模式是不打开游戏窗口、仅运行服务端逻辑的方式，适用于联机仓库的核心场景：

- 作为独立联机服务器部署
- 在 Linux 服务器、Windows 机器或容器环境中长期运行
- 只需要世界模拟和网络服务，不需要图形界面
- 作为稳定的联机服务器对外提供服务

### 启动方式

#### Windows

Windows 启动器支持以下参数：

- `-d`
- `--server`

示例：

```bash
SurvivalcraftStarter.exe --server
```

或者：

```bash
SurvivalcraftStarter.exe -d
```

#### Linux

Linux 启动器同样支持 `-d` 和 `--server`。

示例：

```bash
./SurvivalcraftStarter --server
```

开发环境也可以直接运行对应项目：

```bash
dotnet run --project Survivalcraft.Linux/Survivalcraft.Linux.csproj -- --server
```

#### Android

Android 端无法命令行切换 Headless，因此使用更加直接通用的方式，直接读取运行配置：

- 配置文件：`config:RunningSetting.xml`
- 将 `RunMode` 设为 `HeadlessServer`

首次启动时，如果配置文件不存在，程序会自动创建默认配置。
Android 进入 Headless 模式后，界面会切换到日志视图，服务端在后台持续运行。

### 常用参数

Headless 模式下，除了 `-d` / `--server`，还支持：

- `--world <名称>`: 指定世界目录名或世界名
- `--seed <种子>`: 新建世界时使用的种子

示例：

```bash
./SurvivalcraftStarter --server --world World --seed 123456
```

### 配置文件

`config:RunningSetting.xml` 用于保存启动设置，常见内容包括：

- `RunMode`
- `World`
- `Seed`

示例：

```xml
<RunningSetting RunMode="HeadlessServer" World="World" Seed="123456" />
```

行为说明：

- `RunMode=HeadlessServer` 时，启动器会直接进入无头服务端模式
- 如果 `World` 对应的世界不存在，程序会自动创建
- 如果提供了 `--seed`，新建世界时会优先使用该种子
- 如果世界已经存在，`--seed` 会被忽略

### Headless 运行时行为

Headless 启动后会：

- 初始化配置、内容、模组、数据库和世界系统
- 自动装载语言资源，优先使用 `zh-CN`，否则回退到 `en-US`
- 启动网络服务，默认监听游戏端口和广播端口
- 运行一个固定节奏的主循环，默认 20 TPS
- 退出时保存世界和设置

可以通过 `Ctrl+C` 终止进程。

## 项目结构

| 项目 | 说明 |
|------|------|
| `Survivalcraft/` | 核心游戏逻辑，联机规则、世界、网络、模组都在这里 |
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

## 结构说明

### 1. 启动层

桌面端和 Android 端都有自己的启动器工程，但最终都是读取配置文件 `RunningSetting` 来决定运行在 Gui 模式还是 HeadlessServer 模式。 这样做的原因是，服务器真正关心的不是启动来源，而是运行模式、世界和种子。

- Windows / Linux：通过命令行参数切换 GUI 或 Headless
- Android：通过 `config:RunningSetting.xml` 决定运行模式

### 2. 运行模式层

运行模式由 `Engine.Core.RunMode` 表示，主要有两种：

- `Gui`
- `HeadlessServer`

很多系统都会根据这个值决定是否初始化图形、声音、粒子、天空、阴影和模型渲染等模块，从而减少服务器端的额外开销，同时保留客户端的完整视觉效果。

### 3. 游戏核心层

`Survivalcraft/` 是最主要的业务层，包含：

- `Managers/`：设置、内容、世界、版本、资源与运行配置
- `Game/`：入口、Headless 启动、世界加载与保存
- `Network/`：服务端、客户端、包协议、序列化和网络调度
- `Subsystems/`：世界更新、天气、光照、爆炸、动画、移动方块等系统
- `Components/`：玩家、实体、行为、交互、装备、生命等组件
- `Screens/`：游戏主菜单、加载、设置、联机界面等 UI

### 4. 引擎层

`Engine/` 提供跨平台运行时能力：

- 图形资源、渲染器、着色器、材质与批处理
- 音频播放与混音
- 窗口系统、输入、手柄、活动宿主
- 文件存储抽象

Headless 模式虽然不需要窗口和渲染，但仍然会复用这套基础库中的类型、时间系统和调度系统。这是为了让服务器和客户端尽可能共享基础能力，而非各自实现。

## 共享配置

仓库把很多跨项目约定集中到几个共享文件里，避免每个工程重复配置。对于联机项目而言，这有助于降低跨平台配置不一致的风险。

### `Directory.Build.props`

这是全局构建属性入口，主要定义了：

- `GlobalTargetFramework`
- `AndroidTargetFramework`
- `MinAndroidApiVersion`
- `LangVersion`
- `Nullable`
- `WarningsAsErrors`
- `ImplicitUsings`
- 解决方案级别的 `Configurations`

这些属性会被大多数项目自动继承，所以单个项目里通常只需要写差异部分。

### `Directory.Build.targets`

这是全局构建目标入口，主要负责：

- 把 `Content/` 打成 `Content.zip`
- 在 `Publish` 后打包桌面发布输出
- 在 Android 构建后重命名 APK

也就是说，资源打包和发布产物的处理不再分散在各个项目中，而是统一由这里接管。

### `SharedProperties/GlobalUsing.props`

这是全局 `using` 的集中入口。它给很多常用命名空间做了隐式导入，例如：

- `Engine.Core`
- `Engine.FileStorage`
- `Engine.Windowing`
- `Game.Managers`
- `Game.Screens`
- `Game.Subsystems`
- `Game.Components`
- `Game.ModManager`

这也是为什么仓库里很多代码文件可以少写一大串 `using`——目的是降低共享代码中的重复成本。

### `SharedProperties/Android.props`

这是 Android 相关的共享属性，集中定义了：

- `ApplicationId`
- 签名相关配置
- `AndroidDexTool`
- `AndroidPackageFormat`
- `AndroidStripILAfterAOT`
- `AndroidUseInterpreter`
- `EnableLLVM`
- `RunAOTCompilation`
- `AndroidLinkMode`
- `LinkAll`

Android 的几个变体项目会复用这份配置，只在自己的 `.csproj` 里覆盖架构、输出名和引用方式。这样一来就可以集中管理移动端的构建差异。

## 文件定位

`Engine.FileStorage.Storage` 负责统一处理文件路径。项目中的大部分文件读写并不是直接使用系统路径，而是先使用带前缀的逻辑路径，再由 `Storage` 映射到当前平台上的实际位置。

### 解析方式

- 先识别路径前缀
- 再根据平台将路径转换为实际文件系统路径
- 最后执行读写、枚举、创建或删除操作

这样做的原因是同一份代码需要同时运行在桌面端和 Android 端，直接使用绝对路径会使跨平台逻辑变得难以维护。

### 桌面端支持的预定义路径

- `app:`: 应用目录，通常对应程序入口所在目录
- `data:`: 数据目录，通常对应用户本地应用数据目录下的应用子目录
- `config:`: 配置目录，位于应用目录下的配置路径
- `system:`: 直接使用系统路径，不再额外拼接应用目录

### Android 端支持的预定义路径

- `app:`: 应用资源目录，对应 APK 内部资源访问
- `data:`: 应用数据目录，通常对应用户文档或应用可写目录
- `android:`: Android 外部存储目录下的 `scnet` 子目录
- `config:`: 配置目录，位于 Android 运行环境下的配置路径

### 典型用法

仓库中常见的路径有：

- `config:RunningSetting.xml`
- `config:Settings.xml`
- `config:ModSettings.xml`
- `app:Content.zip`
- `data:` 下的世界、缓存和玩家数据

### 说明

- `app:` 适合读取随程序分发的资源
- `data:` 适合保存运行时数据、日志、世界和缓存
- `config:` 适合保存启动设置、游戏设置和模组设置
- `android:` 主要用于 Android 平台上的外部存储访问
- `system:` 仅在桌面端可用，用于直接访问系统路径

## 外部依赖

### 平台与工具链

- [.NET 10 SDK](https://dotnet.microsoft.com/zh-cn/download/dotnet/10.0)
- Visual Studio 2022+ 或 Rider
- Android SDK
- Linux 桌面运行时需要可用的图形桌面环境

### 运行时和第三方包

代码库当前显式引用的主要外部包包括：

- `LiteNetLib`：网络通信
- `Newtonsoft.Json`：JSON 读写
- `Clipboard.CSharp`：桌面剪贴板支持
- `MessagePack`：实体系统相关序列化
- `NAudio.Core`、`NAudio.Flac.Unknown`、`NLayer.NAudioSupport`、`NVorbis`：音频解码与播放
- `Silk.NET.OpenAL`、`Silk.NET.OpenGLES`、`Silk.NET.OpenGL`、`Silk.NET.Input`、`Silk.NET.Windowing`：窗口、输入与图形抽象
- `Silk.NET.OpenAL.Soft.Native`：桌面端音频补充
- `Silk.NET.SDL`：Android 平台窗口/输入适配
- `SixLabors.ImageSharp`：图片处理
- `Xamarin.AndroidX.Core`：Android 兼容支持

### 测试依赖

- `Microsoft.NET.Test.Sdk`
- `xunit`
- `xunit.runner.visualstudio`
- `coverlet.collector`

## 构建

```bash
# 还原依赖
dotnet restore SCNETWORK.slnx

# Debug 构建
dotnet build SCNETWORK.slnx

# Release 构建
dotnet build SCNETWORK.slnx --configuration Release
```

可选构建配置：

- `Debug`
- `Release`
- `ANDROID`
- `DESKTOP`

配置语义：

- `Debug` 和 `Release` 是通用配置，会走完整解决方案编译流程
- `ANDROID` 和 `DESKTOP` 是平台配置，只编译对应平台相关的项目
- `Debug` 更适合开发调试，`Release` 更适合发布打包
- 测试工程在 `Release` 下会被排除，部分 Android 变体也会在 `Release` 下被排除
- 当前代码库大量使用反射，因此不启用 `trim`，也不支持通过裁剪方式进行发布优化

### 打包行为

仓库使用 `Directory.Build.targets` 统一处理资源和发布结果：

- 构建前会把 `Content/` 打成 `Content.zip`
- 桌面发布会将 `$(PublishDir)` 生成的发布输出压缩到仓库根目录下的 `Publish/` 目录
- Android 构建会生成并重命名 APK 到 `Publish/`

这也是为什么不同平台项目里会看到 `UsePackResourceTarget`、`UsePackOutputTarget` 和 `UseRenameApkTarget` 之类的属性。这些属性用于集中处理资源和发布产物。

## 测试

```bash
dotnet test Engine.Test/
dotnet test EntitySystem.Test/
dotnet test Survivalcraft.Test/
```

测试框架为 xUnit + coverlet。
测试项目仅在 `Debug` 配置下编译。

## 版权与法律

请先阅读免责声明：[DISCLAIMER.md](./DISCLAIMER.md)。

## 贡献

1. Fork 本仓库并克隆
2. 基于主分支创建功能分支
3. 提交代码并推送
4. 创建 Pull Request

> 本文档由 AI Agent 编写润色, 由 `Yunus0712` 审阅
