# 结构与分层

## 设计重心

本仓库的重心是生存战争联机的实现与维护，游戏本身的内容请自行探索或通过其他途径了解。

仓库的设计重点不在于游戏本身，而在于：

- 让服务器可以直接以 Headless 方式运行，不依赖图形界面
- 让桌面端、Android 端和服务端尽量共享同一套游戏逻辑
- 让平台差异尽量集中在启动器和少量共享配置中
- 让联机相关功能的维护无需在多个工程中重复修改

## 概述

仓库的核心思路是将"游戏逻辑"与"平台层"分离：

- `Survivalcraft/` 负责游戏规则、世界、网络、存档、模组和界面逻辑
- `Engine/` 负责渲染、音频、窗口、输入等运行时能力
- `Engine.Network/` 提供独立传输宿主、连接与准备生命周期、无连接端点及邀请/中继路径；不识别玩家或游戏包
- `Rendezvous.Protocol/` 定义信令契约与认证中继编码，`RendezvousServer/` 是独立部署的内存会话/中继服务
- `NetworkProbe/` 通过 Engine 网络 API 验证独立进程连接，不加载游戏世界
- `Engine.Core/` 按 `Mathematics`、`Collections`、`Logging`、`Runtime` 组织基础类型，文本值转换归属 `Engine.Core.Text.Conversion`，运行时类型查找归属 `Engine.Core.Reflection`；XML 模板与类型短名由 EntitySystem 管理
- `Engine.Media/` 按 `Images`、`Audio`、`Models` 子目录和命名空间组织图像、音频及模型数据，不依赖窗口、GPU 或音频设备
- `EntitySystem/` 提供实体与组件系统
- `Content.Packaging/` 位于解决方案的 Protocol 层，定义与游戏运行时、服务端和工具共享的内容包协议
- `ServerSource.Protocol/` 位于 Protocol 层，定义游戏客户端、ContentServer 和第三方服务器源共同使用的只读 HTTP 契约与校验规则；ContentServer 是带发布者投稿和管理员审核能力的完整参考实现
- 平台项目只负责启动方式、资源打包和宿主差异

这种拆分方式的好处在于：

- 服务器启动时，只需加载真正需要的部分
- 客户端更新游戏规则时，服务端无需单独维护另一份逻辑
- 平台差异不会过多地侵入核心代码

## 结构说明

### 1. 启动层

桌面端和 Android 端都有自己的启动器工程，但最终都会落到启动会话模型上。`RunningSetting` 只保存 GUI、HeadlessServer、日志和窗口等可持久化入口设置；`StartupRequest` 表示本次命令行或 Intent 的临时意图；`SessionInfo` 描述要恢复的世界、服务器浏览器或远程服务器。`StartupManager` 负责合并三者并生成只读入口语义的 `StartupContext`，后续流程直接消费其中的有效 session。

- Windows / Linux：通过命令行参数切换 GUI 或 Headless
- Android：通过 `config:RunningSetting.xml` 决定常规运行模式，调试时也可通过 Intent Extra 注入同一套临时启动参数
- 启动目标、世界、种子、运行期游戏模式覆盖、端口和远程服务器信息：通过 `config:SessionInfo.xml` 管理

平台文件选择能力定义在 `Engine.FileStorage`。GUI 平台的 Starter 在进入游戏主体前注册
`IFilePicker` 实现；游戏逻辑只接收用户选择目标所提供的读写流，不依赖系统路径、Android URI
或桌面文件对话框类型。Headless 和尚未提供实现的平台不注册 picker，并通过
`FilePicker.IsAvailable` 表明该能力不可用。远程内容查询与下载使用独立的 ContentServer API，
不实现或复用文件选择接口。

Windows GUI Starter 在 Windows 项目内直接使用 WinForms `OpenFileDialog`/`SaveFileDialog`，对话框在专用 STA 线程运行，不启动 PowerShell 子进程；Linux GUI Starter 通过会话 D-Bus 调用 XDG Desktop Portal `FileChooser`，具体对话框由桌面 portal backend 提供；Android GameActivity 使用 Storage Access Framework 的 `ACTION_OPEN_DOCUMENT` 与 `ACTION_CREATE_DOCUMENT`，并持久化授权后的 URI 访问。Headless 分支均在注册之前返回或进入独立 Activity，因此不会暴露交互式 picker。Linux GUI 不依赖 `zenity` 可执行文件；会话总线、portal 服务或桌面 backend 不可用时，实际选择请求失败并返回明确错误。

Starter 统一通过 `PlatformManager` 注册平台能力，避免直接操作各子系统的全局入口。GUI 的文本输入和文本剪贴板均由 SDL2 backend 提供，FilePicker 仍使用上述平台原生实现；Headless 不注册这些交互能力。

对于已有世界，session 游戏模式的优先级高于存档，但只改变本次运行时状态；世界保存仍写回原模式。这样调试实例可以切换 Creative、Survival 等模式，而不会污染测试存档。

详见 [StartupSessions.md](./StartupSessions.md)。

### 2. 运行模式层

运行模式由 `Engine.Core.Runtime.RunMode` 表示，主要有两种：

- `Gui`
- `HeadlessServer`

很多系统都会根据这个值决定是否初始化图形、声音、粒子、天空、阴影和模型渲染等模块，从而减少服务端的额外开销，同时保留客户端的完整视觉效果。

### 3. 游戏核心层

`Survivalcraft/` 是最主要的业务层，包含：

- `Managers/`：设置、内容、世界、版本、资源与运行配置
- 项目根目录：保留 `GameEntry`、`HeadlessEntry` 和 `GamePaths`，作为启动交接入口与路径定义，不再设置额外的 `Game/` 目录
- `Authentication/`、`Admission/`、`Identity/`、`Players/`、`Teams/`：对应 `Game.Authentication` 等子名称空间的独立职责模块
- `Network/`：服务端、客户端、包协议、序列化和网络调度
- `Subsystems/`：世界更新、天气、光照、爆炸、动画、移动方块等系统
- `Components/`：玩家、实体、行为、交互、装备、生命等组件
- `Screens/`：游戏主菜单、加载、设置、联机界面等 UI

模块目录相对于 `Survivalcraft/` 项目根组织，与 `Game.<模块>` 名称空间对应。`RootNamespace=Game` 是项目默认前缀，
不表示类型应全部位于根名称空间。没有 `Game.Data`、`Game.Enums` 或 `Game.Interface` 这样的通用集合：
值对象、枚举、查询和接口随其职责模块放置。

| 模块 | 负责的类型与边界 |
|---|---|
| `Startup` / `Configuration` / `Platforms` | 启动请求及会话、实例设置、平台类别识别；不保存游戏角色状态 |
| `World` / `Terrains` / `Spawning` | 世界设置和物体、空间查询与更新约定、地形规则、生成数据 |
| `Characters` / `Creatures` / `Controls` | 玩家角色与角色统计、生物类别及行为数据、操控输入；不混同持钥玩家身份 |
| `Inventories` / `Crafting` / `Furniture` | 库存协议与槽位布局、配方数据、家具设计及集合；家具面板仍归 `Widgets` |
| `Blocks` / `ElectricElements` | 方块几何及放置、可编辑方块数据、电路连接及逻辑数据 |
| `Rendering` / `ParticleSystems` / `Widgets` | 游戏专用模型与天空渲染、粒子数据、界面控件和输入描述 |
| `Entities` / `Content` / `Servers` | 实体引用与转换、内容来源和公开内容资料、游戏服务器来源及房间配置 |
| `Diagnostics` / `Help` | 游戏日志和 Headless 统计、帮助页面资料 |

不依赖游戏的算法不留在这些模块中：A* 及其存储/世界接口位于 `Engine.Core.Pathfinding`，
几何、噪声与曲线位于 `Engine.Core.Mathematics`，集合、状态机、性能统计和文本工具分别归属
`Engine.Core.Collections`、`Runtime`、`Diagnostics`、`Text`。通用子纹理、图集、路径和斜面绘图及基础天空 shader 归 `Engine.Graphics`；
游戏天空/模型渲染和依赖游戏内容管理器的资源缓存仍归游戏。
引擎基础类型不得反向引用游戏或序列化项目。

全局导入只为已有的共享游戏表面提供便利；它不建立职责或绕过模块依赖。窄领域在调用处显式导入。
模板数据库、XML 类型字符串、反射与人类可读序列化使用当前实际全名，不维护旧根名称别名。
本项目未发布，不提供旧开发存档的名称空间迁移层。

内容分发在游戏核心中分成四个边界：`ContentRepositoryManager` 维护多个持久来源；`ContentServerClientPool` 按持久或连接作用域复用客户端；
目录服务负责分页、跨仓库聚合、版本历史和冲突；下载服务只按精确 PackageHash 写入统一缓存。Screen 不创建单地址客户端，
Profile 也不保存仓库地址。联机临时仓库是握手提供的短生命周期候选源，不进入 Settings 或 Profile。

内容 UI 同样按职责拆分：仓库页只管理来源，在线页只浏览版本和下载，Mod 页只管理缓存及 Profile，内容包页只管理非 Mod
缓存，安装后的世界、材质、皮肤和家具仍由各自资产页面管理。这样缓存制品、Profile 声明、远程来源和安装资产具有独立生命周期。

### 4. 引擎层

`Engine/` 提供跨平台运行时能力：

- 图形资源、渲染器、着色器、材质与批处理
- 音频播放与混音
- 窗口系统、输入、手柄、活动宿主
- 文件存储抽象

Headless 模式虽然不需要窗口和渲染，但仍然会复用这套基础库中的类型、时间系统和调度系统，目的是让服务器和客户端尽可能共享基础能力，而非各自实现。
