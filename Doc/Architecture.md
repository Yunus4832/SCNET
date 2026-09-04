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
- `Engine.Core/` 和 `Engine.Serialization/` 负责基础类型与序列化
- `EntitySystem/` 提供实体与组件系统
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

对于已有世界，session 游戏模式的优先级高于存档，但只改变本次运行时状态；世界保存仍写回原模式。这样调试实例可以切换 Creative、Survival 等模式，而不会污染测试存档。

详见 [StartupSessions.md](./StartupSessions.md)。

### 2. 运行模式层

运行模式由 `Engine.Core.RunMode` 表示，主要有两种：

- `Gui`
- `HeadlessServer`

很多系统都会根据这个值决定是否初始化图形、声音、粒子、天空、阴影和模型渲染等模块，从而减少服务端的额外开销，同时保留客户端的完整视觉效果。

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

Headless 模式虽然不需要窗口和渲染，但仍然会复用这套基础库中的类型、时间系统和调度系统，目的是让服务器和客户端尽可能共享基础能力，而非各自实现。
