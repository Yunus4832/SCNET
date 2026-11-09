# 结构与分层

> Core、Client、Hosting 与独立服务端入口已完成源码分离。实现边界、验证范围与持续验收要求见[逻辑核心分离方案](LogicCoreSeparation.md)。

## 设计重心

本仓库的重心是生存战争联机的实现与维护，游戏本身的内容请自行探索或通过其他途径了解。

仓库的设计重点不在于游戏本身，而在于：

- 让服务器可以直接以 独立服务器方式运行，不依赖图形界面
- 让桌面端、Android 端和服务端尽量共享同一套游戏逻辑
- 让平台差异尽量集中在启动器和少量共享配置中
- 让联机相关功能的维护无需在多个工程中重复修改

## 概述

仓库的核心思路是将"游戏逻辑"与"平台层"分离：

- `Survivalcraft.Core/` 负责公共游戏规则、实体与世界状态、地形、物理和复制契约
- `Survivalcraft.Hosting/` 负责连接、消息处理目录、权威世界装配、存档、命令与 CPU 模组运行时
- `Survivalcraft.Client/` 负责客户端输入、界面、模型、地形网格与绘制、音频及资源生命周期
- `Survivalcraft/` 负责综合应用装配、平台生命周期、应用服务及启动和管理页面
- `Engine/` 负责渲染、音频、窗口、输入等运行时能力
- `Engine.Network/` 提供独立传输宿主、连接与准备生命周期、无连接端点及邀请/中继路径；不识别玩家或游戏包
- `Rendezvous.Protocol/` 定义信令契约与认证中继编码，`RendezvousServer/` 是独立部署的内存会话/中继服务
- `NetworkProbe/` 通过 Engine 网络 API 验证独立进程连接，不加载游戏世界
- `Engine.Core/` 按 `Mathematics`、`Collections`、`Logging`、`Runtime` 组织基础类型，文本值转换归属 `Engine.Core.Serialization`，运行时类型查找归属 `Engine.Core.Reflection`；XML 模板与类型短名由 EntitySystem 管理
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

桌面端和 Android 端都有自己的启动器工程，但最终都会落到启动会话模型上。`RunningSetting` 只保存 GUI、独立服务器、日志和窗口等可持久化入口设置；`StartupRequest` 表示本次命令行或 Intent 的临时意图；`SessionInfo` 描述要恢复的世界、服务器浏览器或远程服务器。`StartupManager` 负责合并三者并生成只读入口语义的 `StartupContext`，后续流程直接消费其中的有效 session。

- Windows / Linux：通过命令行参数切换 GUI 或 独立服务器
- Android：通过 `config:RunningSetting.xml` 决定常规运行模式，调试时也可通过 Intent Extra 注入同一套临时启动参数
- 启动目标、世界、种子、运行期游戏模式覆盖、端口和远程服务器信息：通过 `config:SessionInfo.xml` 管理

平台文件选择能力定义在 `Engine.FileStorage`。GUI 平台的 Starter 在进入游戏主体前注册
`IFilePicker` 实现；游戏逻辑只接收用户选择目标所提供的读写流，不依赖系统路径、Android URI
或桌面文件对话框类型。独立服务器和尚未提供实现的平台不注册 picker，并通过
`FilePicker.IsAvailable` 表明该能力不可用。远程内容查询与下载使用独立的 ContentServer API，
不实现或复用文件选择接口。

`SessionInfo` 描述可保存、恢复的启动会话。Hosting 的 `GameWorldController` 持有当前 `GameRuntime`，综合应用通过 `GameManager.Runtime` 访问该实例。
`SessionInfo.SessionId` 标识启动记录；`Client.ConnectionInstanceId` 标识一次连接实例，
`Client.ID` 与协议的 `ControllerClientId` 则是权威节点分配的 byte 路由编号。信令协议中的 SessionId 属于信令会话，独立于这些标识。
运行中的游戏由 `GameRuntime` 持有独立的 Authority 和 Client 世界，每个世界分别拥有 Project、
`WorldRuntimeContext`、内容目录、消息收件箱与生命周期。世界代码从所属 Project 获取上下文，
以 `RuntimeSide.Authority/Client` 区分职责；`NetNode.Role` 的 `Authority/Client` 表示消息端点角色，
`Inactive` 表示端点尚未激活，不再使用独立的单机执行角色。GUI、独立服务器和连接阶段分别表达进程能力与连接状态。

`GameRuntime` 在接管世界时同时持有对应的消息节点，世界先释放 Project 或路由资源也不会丢失端点的停机责任。
释放时只停止仍属于该世界代次的节点，避免误停已被复用的端点。GUI 本机玩家退出时仅保留仍存活的权威世界；
权威世界已经关闭时，退出流程释放整个运行实例并返回存档列表。

单机和 GUI 本机玩家均通过进程内连接加入权威世界，走共同的准入、Bootstrap、初始世界快照和玩法消息处理器；
远程客户端通过网络适配器加入。进程内连接不经过网络序列化或区块分片，接收方仍拥有独立的消息数据。
单机信任实例选中的本机身份，不建立网络监听或远程认证事务。GUI 开服与 独立服务器共用权威加载和监听流程。
各模式共用 `PlayerAdmission` 的存档业务检查和 `CharacterCreation` 的角色创建规则，权威世界不构造本机玩家。

权威世界负责模拟、地形生成及保存，客户端世界负责本机输入和表现；同进程客户端也保有独立的地形副本。
生物的眼位、朝向指令与攻击阶段由独立的 `ComponentCreaturePose` 提供，规则和 AI 不查询渲染模型。
派生生物模型只在客户端更新，消费 Pose 的阶段进行动画；第一人称视觉晃动由模型另行叠加，不改变玩法眼位。
首次开场由权威侧保存一次性 `Intro.PlayIntro` 指令，在玩家就绪确认后消费；本机 `ComponentIntroPresentation` 独立运行镜头与临时开场船，观察者和 独立服务器不执行演出。开场计时相对于演出起点，客户端场景船不进入权威世界保存。合成和熔炉提示经角色连接投递标准 Toast，不由规则调用 GUI。
角色输入协调器通过 `ComponentCharacterPresentation` 提供本机骑乘呼喊和创造选块反馈，不绑定音频；发光眼睛只在客户端绑定模型与分配发光点，移除或释放时清理。物件损伤只由权威侧更新，客户端通过消息应用状态与受击、销毁事件，无需运行变化量跟踪。
船与移动组件保留双方运动计算，沉船音效、脚步及鞋子提示由独立客户端表现消费；浸没损伤、强制下船和玩法噪声由权威侧决定。发射器只在权威侧消费库存并生成物品或投射物，客户端接收实际结果的声音事件，不重放生成逻辑。
服装穿着等级检查、磨损和护甲损耗只在权威侧执行，库存同步在两侧更新服装及物理属性。`ComponentClothingPresentation` 独立管理皮肤合成、模型贴图、设备重置和资源释放；护甲音效、破损碎屑及本机提示消费权威服装事件。
经验增长与跨级次数只由 `ComponentLevel` 在权威侧计算，等级结果同步给实体观察者；客户端直接应用精确等级。`ComponentLevelPresentation` 仅为本机玩家更新等级标签、播放经验和升级反馈，延迟反馈在实体移除或释放后失效。双方仍计算玩法属性因子，以支持客户端预测。
挖掘与放置的地形预测仍由 `ComponentMiner` 协调，工具提示、材质音效、碎屑和挥击音效由 `ComponentMinerPresentation` 消费事件。近战命中随机、伤害、击退及近战统计只在权威侧计算，客户端回放仅提供挥击反馈；身体命中音效和浮字消费权威结果消息。
身体碰撞、浮力与浸没状态由 `ComponentBody` 维护；`ComponentBodyPresentation` 订阅浸没更新，在客户端播放入水声音与流体粒子，权威身体不依赖音频和粒子子系统。
鲸类呼吸行为只负责上浮决策与呼吸计时，喷气消息由独立的 `ComponentCetaceanBreathePresentation` 消费并更新，不再由鱼类模型回调规则组件。嚎叫、惊飞振翅及产卵声音通过生物声音事件在客户端播放，玩法噪声与产蛋仍在权威侧执行。
生物声音的权威事件和发送节流由 `ComponentCreatureSounds` 处理，`ComponentCreatureSoundsPresentation` 负责本地播放、播放节流与脚步材质采样。声音不是伤害、噪声或 AI 决策的反馈依据。
生物 AI、行为选择与寻路只在权威世界更新。客户端本机骑乘保留独立的速度和转向预测，不运行生物 AI 状态机，也不因骑乘消息开启整套行为选择。
生物生成调度、掉落提交和最终实体退场也由权威世界推进。客户端保留淡入淡出所需数据与动画，等待权威实体移除消息，不自行完成退场或修改掉落标记。
GUI 与 独立服务器宿主通过同一个游戏运行时更新入口，先处理两侧消息，再更新权威世界和客户端世界；页面不负责推进世界。现有宿主时间步策略保持独立。
运行时先更新权威世界，再更新客户端世界；暂停单机时暂停两侧模拟，消息处理仍可运行，GUI 开服不因本机菜单暂停服务。
菜单、对话框与鼠标输入由界面层继续更新，不依赖模拟是否暂停。单机的进程内连接不代表联机；聊天、玩家列表、邀请及开服管理提示只在存在实际网络监听或远程连接时显示。
应用层通过 `GameManager.ClientProject` 与 `AuthorityProject` 明确选择世界；不存在随页面切换改变指向的全局 Project 或全局世界角色。
世界业务从自己的 Project 获取 `WorldRuntimeContext`，服务器发现快照也使用请求所属的权威世界。
更新对象通过 `IUpdateable.UpdateSide` 显式声明 Authority、Client、Presentation 或 Both；没有未声明类型默认在两侧运行的规则。
Both 表示两个独立实例分别执行共享计算，仍需在实例内部区分预测、权威提交与表现工作。
燃烧、变身、生命与耐久分别由原状态/规则组件和对应的 `ComponentOnFirePresentation`、
`ComponentShapeshifterPresentation`、`ComponentHealthPresentation`、`ComponentDamagePresentation` 组合。
原组件不再管理粒子、音频和 HUD 资源；表现组件只在具有表现能力的客户端加载依赖。
燃烧持续时间和生命/耐久变化仍可在客户端进行状态时钟或差值维护，灭火、伤害提交、死亡统计、掉落与实体退场由权威决定。
耐久表现通过攻击/销毁通知触发，不参与每帧模拟；客户端应用销毁通知不自行删除实体。
疾病恶心与呕吐由 `ComponentSickness` 维护权威决定和效果时长，`ComponentSicknessPresentation` 管理粒子、声音和本机遮罩；客户端快照不重新调用疾病规则。
生存数值、睡眠和流感同样由原状态组件与 `VitalStatsPresentation`、`SleepPresentation`、`FluPresentation` 组合。
饥饿、疲劳、温度、湿度、感染随机性和伤害只在权威侧决定；客户端仅预测耐力并应用快照。耐力使用共享公式，输入为实际运动而非客户端提交的耐力值。

网络收到的材质数据归客户端运行时所有，GPU 纹理由客户端的 `SubsystemBlocksTexture` 加载和释放；自定义纹理随世界释放，内置只读纹理仍由内容缓存共享。
权威世界保留自己的消息历史，收件人范围只控制客户端投递；GUI 与 独立服务器使用同一条记录规则。
世界模拟和自动保存不依赖本机玩家或游戏画面。关闭世界会取消所属异步工作并释放资源，旧世界消息不能转交后续世界。
异步存档写入失败始终记录原世界的异常；GUI 提示标明世界名称，返回菜单后仍可报告本次失败，开启新的游戏运行时后不再显示上一次运行的迟到提示。
权威地形的后台存档容量包含未完成写入和保留的失败快照。容量不足时延迟卸载并保留脏数据；保存时会有限重试失败写入，存储仍不可用时报告错误。
关闭存档协调器先停止接收新卸载请求，再等待已有工作结束。
本地与网络连接均在共同收件箱投递时校验消息阶段：玩法消息要求连接进入 Live，并满足消息所需的玩家状态；
地形请求可在 Bootstrap 已应用后开始，以便加载世界。认证证明、阶段确认和主动断开消息分别由相应处理器验证。
`SessionTarget.World` 表达直接进入游戏，`HostedWorld` 表达仅开服并进入服务器页；它们用于启动与恢复。
GUI 返回目标由 `GameManager.LocalPlayerReturnTarget` 持有：直接游戏退出时关闭世界，从服务器页加入后退出则保留服务器。
客户端菜单通过 `ClientWorldPresentation` 提交保存或退出意图，综合应用绑定相应生命周期操作。启动和服务器管理页面调用应用装配服务，再通过 `ScreensManager.SwitchScreen` 切换页面。
本机玩家移除通过 `LocalPlayerRemoved` 通知当前页面；节点停止通知携带来源运行时，当前游戏运行时只处理属于自身世界的通知。
网络节点不直接切换界面。
世界文件的替换、删除和快照恢复以权威世界的目录归属为准，不能通过 GUI 当前指向客户端副本来绕过保护；关闭后尚未释放的世界资源也仍受保护。
目录保护使用存储层解析后的系统路径，拒绝对所属目录及其上下级目录的破坏式操作；其他未被使用的相邻世界仍可正常管理。
材质和皮肤引用替换先等待已有存档写入结束，再同步持久化引用与两侧内存状态，避免后续权威保存写回旧引用。

Windows GUI Starter 在 Windows 项目内直接使用 WinForms `OpenFileDialog`/`SaveFileDialog`，对话框在专用 STA 线程运行，不启动 PowerShell 子进程；Linux GUI Starter 通过会话 D-Bus 调用 XDG Desktop Portal `FileChooser`，具体对话框由桌面 portal backend 提供；Android GameActivity 使用 Storage Access Framework 的 `ACTION_OPEN_DOCUMENT` 与 `ACTION_CREATE_DOCUMENT`，并持久化授权后的 URI 访问。独立服务器分支均在注册之前返回或进入独立 Activity，因此不会暴露交互式 picker。Linux GUI 不依赖 `zenity` 可执行文件；会话总线、portal 服务或桌面 backend 不可用时，实际选择请求失败并返回明确错误。

Starter 统一通过 `PlatformManager` 注册平台能力，避免直接操作各子系统的全局入口。GUI 的文本输入和文本剪贴板均由 SDL2 backend 提供，FilePicker 仍使用上述平台原生实现；独立服务器不注册这些交互能力。

对于已有世界，session 游戏模式的优先级高于存档，但只改变本次运行时状态；世界保存仍写回原模式。这样调试实例可以切换 Creative、Survival 等模式，而不会污染测试存档。

详见 [StartupSessions.md](./StartupSessions.md)。

### 2. 应用入口与世界能力

Windows/Linux Starter 和 Android GameActivity 固定装配图形程序，`Survivalcraft.Server` 固定装配 Hosting 的服务端循环。入口不由持久化配置或全局标志切换。Android 当前不提供独立服务器 APK。

世界的模拟与表现职责由 `WorldRuntimeContext` 决定。只有具备表现能力的客户端世界才创建图形、声音、粒子、天空、阴影和模型渲染资源；GUI 进程内的权威世界同样不创建这些资源。表现对象应在构造之前检查所属世界的能力。图形资源底层按实际图形设备是否可用处理离线工具场景，不读取业务入口模式。

### 3. 游戏核心层

游戏业务与应用装配分为：

- `Survivalcraft.Core/`：世界规则、实体、地形、物理与不依赖设备的计算。
- `Survivalcraft.Hosting/`：权威世界装配、持久化、网络、模组宿主与共享命令服务。
- `Survivalcraft.Client/`：输入、界面、资源表现、地形网格、声音及其他客户端表现。
- `Survivalcraft/`：图形入口 `GameEntry`、应用级管理器与需要装配宿主服务的界面。
- `Survivalcraft.Server/`：独立服务器应用；编译器自动识别唯一的 `ServerStarter.Main` 入口，由应用装配配置、模组、世界、网络、控制台与 HTTP，并协调运行循环、停止和重启。

业务目录与名称空间随所属模块组织，不为历史路径保留空门面；完整边界见 [逻辑核心拆分](LogicCoreSeparation.md)。

配置数据与管理服务分别归属：`Game.Configuration` 只保留实例偏好数据 `Settings`，
分辨率模式位于 `Rendering`，角色列表筛选位于 `Characters`。配置类不调用 Manager 或发出运行时通知；
运行时亮度调整通过 `SettingsManager.SetBrightness` 完成钳制、变更判断和通知。
`AppConfigManager` 管理 `Settings.xml` 中的扩展字符串配置。

身份持久化数据为 `Game.Identity.LocalIdentitySettings` 与 `StoredLocalIdentity`，后者包含私钥，
不用于 UI 或诊断输出。`LocalIdentityManager` 负责创建、默认选择、重命名、删除、备份恢复及事务保存，
向界面提供不含私钥的 `LocalIdentityEntry` 快照。保存回调可以注入，配置数据本身不执行操作。
准入的 `ServerAdmissionPolicy` 是不可变运行时策略，`ServerAdmissionOverrides` 是可恢复的覆盖数据；
`ServerAdmissionManager` 校验、合并和解析策略，`WorldRepository` 读写世界的准入扩展，
`AdmissionAttempt` 构造连接挑战。启动解析只读取参数及环境输入，不读取密码文件或访问认证服务。


| 模块 | 负责的类型与边界 |
|---|---|
| `Startup` / `Configuration` / `Platforms` | 启动请求及会话、实例设置、平台类别识别；不保存游戏角色状态 |
| `World` / `Terrains` / `Spawning` | 世界设置和物体、空间查询与更新约定、地形规则、生成数据 |
| `Characters` / `Creatures` / `Controls` | 玩家角色与角色统计、生物类别及行为数据、操控输入；不混同持钥玩家身份 |
| `Inventories` / `Crafting` / `Furniture` | 库存协议与槽位布局、配方数据、家具设计及集合；家具面板仍归 `Widgets` |
| `Blocks` / `ElectricElements` | 方块几何及放置、可编辑方块数据、电路连接及逻辑数据 |
| `Rendering` / `ParticleSystems` / `Widgets` | 游戏专用模型与天空渲染、粒子数据、界面控件和输入描述 |
| `Entities` / `Content` / `Servers` | 实体引用与转换、内容来源和公开内容资料、游戏服务器来源及房间配置 |
| `Diagnostics` / `Help` | 游戏日志和 独立服务器统计、帮助页面资料 |

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

独立服务器虽然不需要窗口和渲染，但仍然会复用这套基础库中的类型、时间系统和调度系统，目的是让服务器和客户端尽可能共享基础能力，而非各自实现。

### 逻辑核心依赖边界

`Survivalcraft.Core` 承载共享规则、世界状态、地形生成与内容数据库。`Survivalcraft.Hosting` 承载无图形的世界装配、连接与消息目录、命令、CPU 模组与资源目录、存档和启动服务。`Survivalcraft.Client` 承载输入与表现，使用 Hosting 的连接、命令和 CPU 内容能力。综合应用装配这三个程序集，独立服务端入口只使用无图形依赖。通用内容包和 `.scpkg` 保留双方一致的格式与散列，可以包含客户端模块 DLL 及资产；无图形进程按执行侧跳过这些入口与模块加载。

Core、Hosting、Client 在各自 `.csproj` 中声明禁止引用的程序集，构建时检查已解析的引用。Core 禁止引用综合应用、Hosting、Client、Engine 和 Engine.Media；Hosting 禁止引用 Client、综合应用和混合 Engine。Client 可以引用 Hosting，Hosting 保持单向依赖。最终发行依赖和完整运行场景仍按[分离验收清单](LogicCoreSeparation.md#验收清单)验证。

`WorldRuntimeContext` 本身属于 Core，只负责世界身份、明确的执行侧与表现能力、Project 绑定、取消、定时任务及资源生命周期。网络节点、消息收件箱和处理器缓存由 `WorldNetworkBinding` 所有，待安装的客户端内容字节由 `ClientBootstrapContent` 所有，规则目录由 `WorldContentBinding` 所有；Hosting 和客户端工厂分别装配这些所有者，Core 不反向查找它们。关闭世界立即取消投递，绑定 Project 的资源在 Project 释放时清理，内容目录最后释放。

世界工厂使用 `WorldProjectComposition` 根据模板的 `ConstructionSide`（`Shared`、`Authority`、`Client`、`Presentation`）选择实例组成。该选择发生于 ECS 类型解析及构造之前，与 `UpdateSide` 的更新调度相互独立；选择不删除模板定义。内置表现组件显式声明 `Presentation`，装配还须显式启用表现能力，权威世界不创建这些实例。未知的构造侧值会报错，不能静默丢弃内容。

启动会话、实例路径、配置 XML 和世界加载由 `Survivalcraft.Hosting` 所有。`GameWorldController` 加载权威世界并绑定网络与时间发布，`WorldProjectPersistence` 保存权威快照；应用入口选择有效输入并处理界面导航。共享库存契约和分页规则属于 Core；楼梯的24组碰撞定义由 Core 初始化，不依赖视觉模型加载。

地形数据与保存实现（`Terrain`、`TerrainChunk`、`TerrainSerializer24`、保存队列）属于 Core。权威区块的待保存数据恢复、磁盘读取和四阶段内容生成状态机也属于 Core，具体生成器、存储访问与模组生成回调由地形子系统显式装配；光照和网格不由该状态机推进。`HasContents`、`HasCurrentHeightData` 和 `HasCurrentLight` 分别表达内容、高度与光照能力，不等待客户端网格；`IsContentInitialized` 单独控制权威内容对外发布。客户端通过 `SubsystemTerrain.Environment` 查询环境，不装配区块写入生成器。`SubsystemTerrain` 以短世界更新顺序装配需求、驻留、内容接收、环境观察和阶段计算服务；渲染缓冲、网格及上传调度属于每个客户端世界的独立表现存储。详细的[地形生命周期与线程合同](TerrainDistribution.md#线程所有权)由地形分发文档维护。基础文件根与同步存储位于 `Engine.Core`，文件选择器仍属于平台引擎。世界规则与宿主设置分别由 `WorldSettings` 和 `WorldHostingSettings` 所有；磁盘由各自写入所属字段，网络不广播宿主配置。

游戏信息、时间、昼夜和季节子系统属于 Core。时钟在延迟动作执行后读取实际角色列表的睡眠人口事实，宿主更新器在同一更新相位采用返回的更新次数；客户端不根据兴趣范围内的角色推断全服睡眠。Core 发布时间与季节事实，由每个世界的网络装配对象发送同步消息。`SubsystemWorldHosting` 只在权威世界构造，拥有服务器设置及启动临时覆写账本，通过 ECS 的保存快照事件贡献持久化字段，不回写运行状态或共享模板，也不将宿主配置放入客户端快照。

`StartupProfileStore` 是共享的启动配置文件与会话选择入口，由显式文件路径构造；综合程序和独立服务端通过 `StartupManager` 装配它，界面导航留在综合应用，世界文件准备使用 Hosting 的 `WorldRepository`。方块放置规则接收真实视线方向，不再读取 Miner 或界面对象；拒绝结果由调用侧经统一消息服务反馈。

天空与天气分别由规则子系统和 `Presentation` 子系统处理：权威侧负责雷击伤害、点燃、天气调度及地形变化，客户端负责天空绘制、降水粒子和声音；雷击规则不依赖绘制推进。拾取物的物理、拾取和生命周期保留在规则侧，绘制、灯光缓存及效果消费属于独立表现子系统；绘制不回写拾取物状态。拾取物、告示牌文本、生成记录和位置标记等纯数据属于 Core，相关界面及运行装配仍由上层所有。移动方块的运动、碰撞、同步和保存保留在规则子系统；独立表现子系统持有网格、GPU 资源和移除残影。方块集合的内容修订会使客户端网格缓存失效，残影寿命由客户端更新推进，绘制不修改规则集合。

告示牌的文本、颜色与链接存储、权限校验及区块同步属于共享规则子系统；字体、纹理槽、灯光与距离缓存、设备重置及查看和编辑界面属于独立表现子系统。表现层读取存储副本并订阅数据变化，关闭后停止更新与编辑提交，不能通过缓存修改规则文本。

投射物的碰撞、伤害、运动和尾迹描述属于规则状态；独立表现子系统消费效果事实，并持有尾迹粒子与灯光缓存。规则不保存粒子系统实例，表现更新不回写投射物的运动状态。声音传播的距离和可听范围使用 Core 的同一数值算法，客户端音频消费与服务端兴趣分发保持一致。

武器规则保留发射、弹药与库存、伤害、玩法噪声及角色姿态，瞄准表现、武器编辑面板、音效和枪烟由客户端武器表现子系统消费事实。规则先计算原有随机音高再发布事件，表现订阅是否存在不改变规则随机序列。输入组件只在表现世界构造；创造飞行的视线方向选择通过现有角色快照传递，传送后由本地客户端收到权威位置更新时取消导航和视线自动控制。这不等于实现了服务端移动输入重放，现有移动权威模型保持原状。

容器的共享方块行为只处理实际交互请求与规则，箱子、工具台、熔炉和发射器面板由客户端库存表现子系统消费已授权回复后打开。两种打开请求在服务端统一校验库存访问权限，Adventure 模式下拒绝发射器；拒绝时不会先建立观察关系或发送实体基线。

流体传播与火焰燃料、扩散和火点记录由权威规则更新。水、岩浆和火焰的环境声扫描，以及烧毁粒子和声音由客户端表现子系统处理；火焰表现读取实时火点集合，区块状态重置不会让表现缓存回写模拟状态。

世界规则通过 `WorldRuleHooks` 调用明确绑定的模组规则分发器，不再从 `CurrentModRuntime` 读取规则回调。它复用原 ModHost 的注册、优先级、异常隔离与卸载机制，只接受所属世界的活动上下文，关闭后禁用并解除绑定。应用层仍可使用当前模组环境准备资源与预览；该环境不决定一个正在运行的世界使用哪套规则回调。CPU 模组加载、注册和内容目录属于 Hosting，规则钩子契约属于 Core。客户端内容入口与表现资源分别由 Client 和综合应用装配，完整模组框架设计仍单独讨论。

生物自然生成预算与网络兴趣范围由宿主在创建世界时捕获有效设置，分别作为不可变的
`CreatureSpawnPolicy`、`NetworkInterestPolicy` 在子系统加载前显式绑定。不同世界不共用静态预算；
这些设置的修改在下次创建世界时生效，已加载的世界不重新绑定。网络视距仍由原有
`NetworkTerrainPolicy` 钳制。地形 CPU 编解码、异步编码队列、缓存和分片服务属于
`Survivalcraft.Hosting`，核心状态与协议数据不反向依赖这些服务。

连接的身份、控制角色、执行阶段、epoch、递增代际和实时存活投影由 Core 的 `WorldConnection` 与 `WorldConnections` 所有。Host 的 `Client` 是同一逻辑记录的物理传输特化，注册、移除和 Self/Authority 关联由实际事务同时维护。消息受众保存逻辑对象身份，复用路由编号不会使旧受众命中新连接。Core 的发送入口通过唯一世界消息口进入原有节点队列、兴趣分发、挂载基线和传输回压流程，不另建进程内传输捷径。

空间声音请求属于 Core，只描述位置、音量、音高、资源选择与延迟意图；真正的监听、选片、播放队列和设备对象属于客户端音频系统。请求源本身不自动广播，现有各类声音消息仍保留明确的路由，避免重复播放。篝火粒子与环境声使用客户端 owner，雨与燃料更新保留在权威侧。

爆炸的声源从真实压力场选择峰值格，同压时按坐标稳定选择；玩法噪声由权威侧在该格中心产生，不取决于任何视角。权威侧保留原音高随机求值并发送结果，客户端按自身监听位置计算传播延迟。爆炸粒子和声音由客户端表现子系统消费，客户端不重放玩法噪声。

引信由权威侧独占倒计时与爆炸，客户端只接收活跃点的持续状态；区块进入发送当前状态，重置清除副本，表现 owner 负责循环声音和粒子释放。发声器音符则使用瞬态效果消息，不存入电压或区块基线，避免重新进入区域时重播。

规则中的生物名称、伤害原因、死亡历史和系统消息保留结构化文本事实，不由当前进程语言决定其存储内容。字面值、资源、数据库名称和嵌套格式参数使用单一文本树，客户端与控制台边界负责解析；聊天仍按原文字面内容校验与过滤。

消息收件箱的容量、字节预算、发送者轮转、非重入投递及已接管消息的释放由 Core 的 `OwnedMessageInbox<TMessage>` 统一实现，不依赖网络节点、客户端连接或具体协议包。运行层的 `RuntimeMessageInbox` 负责目标世界校验、协议数据校验与内存计费，并使用该调度器；本地与远程消息走同一套限制和投递规则。

ECS 支持在子系统构造与初始化后、加载前显式绑定外部输入，并在绑定失败时清理已构造子系统。客户端内容目录可由客户端工厂通过此装配入口传入表现子系统，Core 无需保存图形对象或引用客户端类型。

规则碰撞几何使用 Core 的只读顶点、节点变换及独立构建器；CPU 模型读取属于 Hosting，可依赖无窗口的 `Engine.Media`。Core 继续禁止媒体和图形程序集引用，Hosting 继续禁止混合 `Engine` 及客户端程序集。
