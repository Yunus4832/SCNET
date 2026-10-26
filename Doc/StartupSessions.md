# 启动会话

启动会话把“怎么启动游戏”分为以下职责明确的配置和对象：

- `Starter.xml`: 选择程序数据实例
- `RunningSetting`: 可序列化的启动入口设置
- `StartupRequest`: 本次命令行或 Intent 提供的临时请求
- `SessionInfo`: 可恢复的具体目标，例如世界、服务器浏览器或远程服务器
- `StartupContext`: 启动解析完成后提供给 GUI 或 Headless 的有效上下文

这样 GUI、Headless、远程联机重启和模组下载后的恢复都可以走同一套流程。

## RunningSetting

路径：`config:RunningSetting.xml`

职责：

- 记录 `RunMode`
- 记录日志级别
- 记录 GUI 窗口模式与尺寸
- 指向默认或 pending session
- 保存未消费的启动参数

它不保存世界名、种子、游戏模式覆盖、远程服务器地址或密码。这些属于 session 状态。

当前字段：

- `RunMode`
- `LogLevel`
- `WindowMode`
- `WindowWidth`
- `WindowHeight`
- `DefaultSessionId`
- `PendingSessionId`
- `RemainingArgs`

`RunningSettingManager` 只负责读取、规范化和保存这些字段，不再解析命令行，也不承载 session 临时字段。

## StartupRequest 与 StartupContext

`StartupManager` 读取 `RunningSetting` 并解析命令行，生成一次性的 `StartupRequest`。其中包含 `SessionName`、`World`、`Seed`、`GameMode`、连接地址、玩家名、端口和 `Save` 等临时意图；该对象不会序列化。

随后 `StartupManager` 按“显式 session、pending session、default session、临时新 session”的顺序选出 session id，将请求覆盖合并到 `SessionInfo`，最终生成 `StartupContext(Settings, Request, Session)`。GUI、Headless、玩家初始化和模组加载只消费这个已经解析好的上下文，不再各自重复合并启动参数。

命令行参数：

- `--instance <实例名>`: 在加载 `RunningSetting` 之前选择或创建数据实例
- `--connect <主机:端口>`: 将本次有效 session 覆盖为远程服务器
- `--player <名称>`: 显式请求自动使用当前身份角色；若服务器没有该身份角色则按名称创建
- `--host`: GUI 模式下强制将目标世界设置为 `WorldSettings.RunServer=true`
- `--server-port <端口>` / `--broadcast-port <端口>`: 覆盖本次运行端口
- `--p2p true|false`: 覆盖本次会话的世界P2P开关
- `--rendezvous <URL>`: 覆盖信令控制地址，单独指定不启用P2P
- `--room-name <名称>` / `--room-description <简介>`: 覆盖房间资料，空简介表示清除
- `--room-public true|false`: 覆盖房间公开状态
- `--admission open|password|external`: 覆盖本次开服准入方式
- `--server-password-file <路径>`: 密码模式的私有密码文件，不接受明文密码参数
- `--authentication-url <HTTPS URL>` / `--authentication-key <SPKI base64url 公钥>`: 外部模式的公开服务地址与固定公钥
- `--authentication-name <名称>`: 外部服务显示名称
- `--http-command`: 为本次有效 session 启用 loopback HTTP 命令宿主
- `--no-http-command`: 为本次有效 session 禁用 loopback HTTP 命令宿主
- `--http-command-port <端口>`: 覆盖 loopback HTTP 命令宿主的默认端口 `28889`
- `--http-command-access-token <Token>`: 覆盖本次运行使用的 HTTP Bearer Token
- `-d` / `--server`: 设置 `RunMode=HeadlessServer`
- `--gui`: 设置 `RunMode=Gui`
- `--session <名称>`: 按名称选择或创建 session
- `--world <名称>`: 和 `--session` 一起使用，设置 session 的目标世界
- `--seed <种子>`: 和 `--session` 一起使用，设置新世界种子
- `--terrain <类型>`: 和 `--session` 一起使用，设置新世界地形，可取 `Continent`、`Island`、`FlatContinent`、`FlatIsland`
- `--terrain-level <高度>`: 和 `--session` 一起使用，设置新世界平坦地形高度，范围 2–252，默认 64
- `--game-mode <模式>`: 和 `--session` 一起使用，覆盖 session 游戏模式
- `--log-level <级别>`: 设置日志级别
- `--window-mode <模式>`: GUI 窗口模式，可取 `Resizable`、`Borderless` 或 `Fullscreen`
- `--window-size <宽x高>`: GUI 窗口尺寸，例如 `1280x720`
- `--save`: 保存合并后的启动设置

`--world`、`--seed`、`--terrain`、`--terrain-level` 和 `--game-mode` 没有 `--session` 时会被忽略。
地形类型和高度随命名会话通过 `--save` 保存；只作用于新世界，已有世界会报告忽略，不修改存档地形。

`--window-mode` 和 `--window-size` 只在 GUI 模式生效；Headless 会忽略它们，也不会因 `--save` 将其覆盖值持久化。

`--connect`、`--host`、`--player`、`--game-mode` 和端口覆盖不写入 `RunningSetting.xml`。玩家名和 HTTP 命令覆盖由 `StartupRequest` 解析后合并进本次有效 `SessionInfo`，以便模组准备触发进程重启时完整恢复；使用 `--save` 时也会随命名 session 写入 `SessionInfo.xml`。HTTP 命令宿主的实例默认开关、端口和 access token 保存在 `Settings.xml`，其中开关默认关闭；session 覆盖具有更高优先级。`.runtime` 目录只用于 Starter 的实例进程存活登记，不承载 HTTP 配置或凭证。`--host` 是强制调试参数：新世界直接创建为联机世界，已有非联机世界则更新并持久化其 `WorldSettings.RunServer=true`，之后统一走正常联机世界启动流程。未传 `--player` 时保持现有角色界面流程。`--connect` 与 `--host` 同时出现时按远程连接处理并忽略 `--host`。

`--save` 会保存 `RunningSetting` 中适用的入口设置，并把 `StartupContext.Session` 保存到 `SessionInfo.xml`。因此 `--connect`、端口和 `--game-mode` 默认只存在于 `StartupRequest` 和本次有效 session 中，与 `--save` 同用时才成为具名 session 的后续默认值。

## 数据实例

Starter 使用两阶段 Storage 注册：先将程序基础目录注册为 `starter:`，读取 `starter:Starter.xml` 并选择实例；随后将 `starter:Instances/<实例名>` 注册为该进程的 `external:`、`data:` 和 `config:`。

```xml
<Starter CurrentInstance="default" NextInstance="" />
```

实例选择优先级为：

1. 启动参数 `--instance <实例名>`；不存在时自动创建
2. `NextInstance`；消费后写入 `CurrentInstance` 并清空
3. `CurrentInstance`
4. `default`

命令行选择实例不会修改 `CurrentInstance`，因此可以同时启动互不干扰的 GUI 和 Headless 调试实例。普通应用重启保持当前进程的实例；`GameExitAction.SwitchInstance` 则消费 `NextInstance` 并进入目标实例。实例内部的 `RunningSetting` 和 session 不保存实例 ID。

桌面端实例管理可将未运行的 GUI 实例在新窗口中启动。启动器向新进程显式传入 `--gui --instance <实例名>`，保留当前窗口，也不改写 `Starter.xml` 的当前或下次实例。当前实例、已运行实例和 Headless 实例不提供此操作；Headless 实例仍可通过切换或命令行启动。

Starter 还负责注册平台能力。交互式文件打开与保存统一通过 `Engine.FileStorage.FilePicker` 暴露，
具体 `IFilePicker` 由 Windows、Linux 或 Android Starter 提供；它不参与实例选择、Storage 根目录
注册或 session 持久化。未注册实现时 `FilePicker.IsAvailable` 为 `false`，Headless 不提供交互式
文件选择。

Windows 和 Linux 只在 `RunMode.Gui` 分支注册；Linux 实现通过会话 D-Bus 调用 XDG Desktop Portal `FileChooser`，不依赖 `zenity`，portal 或桌面 backend 不可用时在发起选择请求时报错。Android 仅由 GameActivity 注册 Storage Access Framework 实现，ServerActivity 不注册。

所有平台能力通过 `PlatformManager` 这一入口注册。GUI 同时注册 SDL2 文本输入、SDL2 文本剪贴板和平台 FilePicker；Windows/Linux Headless 与 Android ServerActivity 不注册这些交互能力。

### 实例身份

`LocalIdentityManager` 在实例 Storage 就绪后的 GUI/Headless 初始化阶段读取
`config:LocalIdentities.xml`，在界面、会话恢复和网络启动之前准备玩家及服务器身份。
每个实例分别持有一份玩家密钥和服务器密钥，两种用途互相独立；首次缺失的身份自动生成并一次保存。
游戏和联网代码只读取已初始化的身份，不在进入存档或开启服务器时创建密钥。
已有身份缺少私钥、地址与私钥不匹配或配置损坏时启动报错，不生成另一身份顶替。

文件直接保存公开地址与 Base64 编码的未加密 PKCS#8 私钥，不使用平台秘密存储、密钥环、DPAPI 或
Android Keystore，也不写入 `Settings`、session 或 `.runtime`。项目的安全边界是信任玩家设备和实例目录访问权限；
Unix 新建文件限制为 `0600`，保存使用临时文件、刷盘和同目录原子替换。
复制或克隆整个实例会同时复制身份，删除实例会同时删除其私钥。实例配置属于敏感数据，不能作为普通诊断附件分享。
旧的外部秘密引用配置不再支持，不自动迁移。

身份地址保留完整 32 字节公钥指纹，统一使用 `scnet` 前缀的 64 字符 Bech32m；规范输出为小写，输入允许全大写并归一化，拒绝大小写混用、错误校验和及非零填充位。旧 Base64URL 地址不再接受。

设置中的“本地身份”页面分别管理玩家和服务器身份。导出仍使用至少八字符的密码加密 PKCS#8 备份，密码不保存。
导入先校验备份并展示新旧地址，确认后原子替换所选身份；必须退出世界并停止网络连接。
导入只恢复持钥身份，不转移角色、权限或其他存档资产；另一用途的身份不受影响。


### 客户端认证服务与授权

GUI 在本地身份准备完成后初始化 `AuthenticationManager`，读取独立的 `config:AuthenticationServices.xml`
和 `config:Private/AuthenticationGrants.xml`。初始化不访问认证服务、不打开网页，也不改变命令行会话选择或账户密钥。
安装记录保存公开端点、公钥、来源与启用状态；私有文件保存按玩家身份、公钥指纹及已确认端点隔离的授权，
支持 POSIX 模式的文件系统中新文件权限为 `0600`。未安装或端点不再匹配的孤立授权在初始化时清理。
桌面 POSIX 文件系统的新建私有目录限制为 `0700`；Android 的共享实例存储不保证支持 POSIX 模式，
沿用实例目录的设备存储访问权限，不强制对共享目录执行 `chmod`。
这些数据不放入 `Settings`、存档、普通 session 或 `.runtime`。
Headless 开服不初始化客户端授权存储，服务器自身的准入配置与客户端安装、登录状态独立。

远程连接在游戏加载页处理准入：开放模式自动确认，密码在游戏内的遮蔽输入框提交，外部认证使用浏览器及签名轮询。
首次未知服务提供“登录并保存 / 仅此次登录 / 拒绝并返回”，停用记录不被服务器描述重新启用。
网页完成仅表示认证服务批准，存档封禁、人数等游戏检查仍可拒绝入场。取消或离开加载页会终止操作，
迟到结果不再保存安装/授权或提交入场批准。

浏览器认证期间，移动系统可能暂停后台联网并使游戏传输超时。此时加载页继续等待原认证事务，
轮询仅在原期限内重试临时网络故障。得到有效 grant 后，对原逻辑目标重新准备路径和安全连接，
申请绑定新挑战的批准，不重用旧连接的批准。临时授权仍只属于当前加入；取消或离开页面会丢弃它。
认证方没有返回可用 grant 时明确提示重新加入，不自动循环打开网页。
Android 在游戏仍活动时从应用图标返回，会把已有 GameActivity 移到前台，不再次注册实例存储或创建
第二个 SDL 运行时。显式带命令行或实例参数的启动仍走原启动流程；调试脚本应先停止旧进程。
GUI 运行结束后由当前启动控制器统一执行退出动作，等待游戏保存和释放世界后再退出进程或重启。
Android 内部重启仅传递实例 ID；有效目标和覆盖从 pending session 恢复，不重播原始命令行，
以免原来的显式 session 或运行模式覆盖本次重启意图。
Android 主动退出先关闭游戏视图并执行保存和资源释放，不提前结束 GameActivity。窗口循环返回后，
由 Starter 统一处理退出、重启、切换实例或更新，避免清理期间露出启动 Activity 和系统导航栏。

“仅此次登录”不安装服务或写入长期授权库。经当前准入流程验证后，临时授权由当前连接在内存中持有，
离开、失败或停止连接时丢弃；下次准入仍须取得绑定新挑战和安全连接的新批准。
重启不保存或恢复当前连接的认证上下文，也不建立与 pending session 关联的认证接续文件。
模组/profile 重启只恢复连接目标和模组配置；恢复后重新建立安全连接并执行正常准入。
已保存且有效的 grant 可直接用于申请新挑战的批准，无需再次打开网页；已记住的服务器密码也按普通连接使用。
“仅此次登录”的临时授权随进程结束丢弃，重启后可能需要再次确认服务并完成网页认证。
GUI 主机快照恢复可在同一进程、同一存档范围内使用内存中的临时授权重新准入，不跨进程持久化。

“设置 → 认证服务 → 服务目录”从已启用 ContentServer 读取独立认证服务目录，支持仓库筛选和名称、地址、说明搜索。
目录条目只包含来源信息，不携带认证公钥或身份。点击安装后，客户端直接向来源地址匿名发现公钥，玩家确认实际地址、公钥和来源后才安装；安装不登录，不发送玩家密钥或授权。
同一服务在多个仓库收录仍以公钥识别，重复安装不启用已停用服务；同一公钥换地址需要确认并清除旧授权，
地址与已安装的另一公钥冲突则拒绝覆盖。仓库失败单独提示，不影响已安装服务的直接使用。

“设置 → 认证服务”提供手动发现及确认公钥后安装、提前登录、查看指纹/来源和授权到期时间、停用、
更新地址、退出登录及移除。安装不强制登录；地址更新必须保持同一公钥，更新后清除本地旧授权并重新登录。
退出登录先清除本地授权，再尽力向原固定服务撤销；失败会说明尚未确认远端撤销，不恢复本地登录状态。
移除仅删除本地服务及其授权；若希望远端撤销，应先退出登录。身份密钥、存档角色和权限均不受这些操作影响。
GUI 主机启动先从世界私有扩展数据读取准入策略，使用与远端相同的密码或外部批准流程完成本地玩家准入，
此时尚未恢复角色或开放游戏监听。本地可信流程不为自己建立 UDP 连接；其批准绑定玩家/服务器密钥、世界目录、
策略指纹及本次随机上下文，不能用于另一个世界或变更后的策略。通过后才加载世界、恢复角色并开监听。
普通单机不进入该流程。Headless 没有可玩本地主机玩家，不要求客户端账号登录。

世界选择、新建世界和命令行会话恢复统一交给加载页启动监听。快照恢复会关闭旧连接并重新准入；恢复时保留
实际主机会话的端口/P2P覆盖。准入覆盖同样参与恢复，实际监听使用与主机预认证相同的有效策略。


### 开服准入配置

新建或修改世界的“服务器设置”可选择开放、密码、外部认证。密码通过游戏内确认输入框设置，
存档只保存随机盐和 PBKDF2 验证值；外部模式选择本机已安装并启用的认证服务，只复制公开地址和公钥，
不复制用户授权。修改现有非开放策略需要确认，主体、权限与角色资料保留。世界编辑作用于下次开服，
不会修改正在运行的服务器或踢出已进入的会话；普通单机不执行联机准入。

GUI 与 Headless 共用命令行覆盖。优先级为命令行、环境变量、已保存 session、世界策略。
环境变量为 `SCNET_ADMISSION`、`SCNET_SERVER_PASSWORD_FILE`、`SCNET_AUTHENTICATION_URL`、
`SCNET_AUTHENTICATION_KEY`、`SCNET_AUTHENTICATION_NAME`。显式切换模式会清除较低优先级的凭证覆盖，
避免密码文件与外部模式混用。未给出覆盖时继承存档策略；外部模式必须具有公开 URL 和公钥，
服务器启动不需要客户端安装记录、用户授权或向认证服务注册、联网发现。

密码文件最多 8192 字节，包含一行 1–1024 字符的密码；允许文件末尾一个换行，密码两端空格保留，
拒绝控制字符。支持绝对路径、解析为绝对路径的相对路径以及 `config:` 等 Storage 路径。
文件内容只在开服时读取，不能通过 `--server-password` 或 `SCNET_SERVER_PASSWORD` 提供。
密码文件缺失、无效参数以及无法读取或损坏的准入会话配置会使启动失败，不会回退到开放模式。
Windows/Linux Starter 保留 Headless 返回的进程退出码：正常停止为 `0`，初始化或运行异常为 `1`，监听启动失败为 `3`。
应限制密码文件及实例目录的访问权限，不把文件放入源码或日志。

覆盖默认仅用于本次运行；`--save` 将公开服务资料及密码文件引用写入命名 session 的
`AdmissionOverrides` 节点，不保存原始密码，也不重写世界策略。必要重启的 pending session 保留相同引用，
`RunningSetting` 不承载准入配置。容器的 `server.conf` 使用 `ADMISSION`、`SERVER_PASSWORD_FILE`、
`AUTHENTICATION_URL`、`AUTHENTICATION_KEY`、`AUTHENTICATION_NAME`，部署脚本映射到上述环境变量；
密码文件路径必须在容器内可访问，例如放入已挂载的实例目录。


## SessionInfo

路径：`config:SessionInfo.xml`

文件根节点为 `<Sessions>`，其中可以包含多个 `<SessionInfo>`。旧版单个 `<SessionInfo>` 根节点仍可读取，下一次保存时会迁移为多 session 容器。

一个 session 包含：

- `SessionId`
- `Name`
- `Target`
- `World`
- `Seed`
- `GameMode`（可选的运行期覆盖）
- `RemoteTarget`（远程地址或邀请会话的逻辑目标）
- `ServerPort`
- `BroadcastPort`
- `AutoJoinCharacterName`（可选的自动进入角色名）

`HttpCommandEnabled`、`HttpCommandPort` 和 `HttpCommandAccessToken` 是可选的 session 覆盖。未设置时回退到当前实例的 `Settings.xml`；使用 `--save` 保存后，可以通过同名 session 恢复。

`Target` 可取：

- `MainMenu`
- `WorldList`
- `World`
- `ServerBrowser`
- `RemoteServer`

Headless 使用 `World` session。远程联机重启使用 `RemoteServer` session。

### World session 优先级

世界目标按以下顺序解析：

1. 本次命令行覆盖，例如 `--world`、`--seed`、`--game-mode`
2. 已保存的具名 `SessionInfo`
3. 世界存档自身设置或创建新世界时的默认值

`Seed` 只参与新世界创建；已有世界会忽略 session seed。`GameMode` 不同：新世界直接以 session 模式创建，已有世界则在运行期使用 session 模式。运行期自动保存和退出保存会继续写回存档原有模式，因此调试 session 不会永久修改已有世界。若希望下次仍使用相同覆盖，应传 `--save` 将它保存在 session 中。

## Pending Session

当 GUI 客户端为了切换到某个 world 或远程服务器所需的模组组合，需要重启进程时，会创建一个临时 session，并把它的 id 写入 `RunningSetting.PendingSessionId`。

下一次启动时：

1. `PendingSessionId` 被解析为 `StartupContext.Session`
2. 启动流程读取对应 session 和 session profile
3. session profile 被解析成运行时有效 profile，并用于初始化 `GameModRuntime`
4. session 中的玩家名和 HTTP 命令覆盖随远程目标一起恢复
5. session 被消费后清理 pending 状态

Headless 不使用这个机制准备模组。它在启动阶段直接解析 profile、下载缺失包并启动 runtime。

## 应用生命周期命令

主菜单的电源菜单通过应用域命令请求重新启动、切换实例、切换运行模式或退出，不直接关闭窗口。`GameExitManager` 统一产生 `Exit`、`Restart`、`SwitchInstance` 或 `Update` 动作，再由 Windows、Linux 或 Android Starter 完成平台级退出和重启。

`SetRunModeCommand` 可以携带可选的重启 session。提供 session 时，下一次启动恢复到其中描述的世界或目标；不提供时，不会临时询问用户，而是继续使用 pending、default 或 Headless 自身的默认解析逻辑。主菜单若让用户选择了 Headless 存档，会在执行命令前构造对应的 `World` session。

主菜单左下角提供独立的实例管理入口。实例管理界面通过应用域命令创建、删除或切换实例；当前实例和其他进程正在使用的实例禁止删除，删除未运行实例会递归删除其完整数据目录。电源菜单仍保留快速切换入口。

Starter 会在实例的 `.runtime` 目录登记当前进程的 PID 和启动时间。实例管理器通过两者共同验证进程身份，避免 PID 复用造成误判，并在查询时清理异常退出遗留的无效标记。正常进程退出时也会主动移除自己的登记文件。

实例管理界面还可以克隆实例。当前实例在没有其他进程共同使用时可以直接克隆；其他实例必须停止。克隆生成新的隔离实例并排除 `.runtime` 进程登记，可作为修改、升级或调试前的完整副本。

## Mod Profile

模组 profile 决定本次运行加载哪些模组。

路径：

- 全局：`config:ModProfile.xml`
- 世界：`<world>/WorldModProfile.xml`
- 会话：`config:SessionProfiles/<sessionId>.xml`

`ModProfile.xml` 中的每个 `Package` 保存：

- `ModId`
- `Version`
- `PackageHash`（规范的小写 64 位 SHA-256）

这三个字段共同构成精确 requirement。Profile 不保存 ContentServer 地址，缺失或非法 hash 不会退化为按版本模糊匹配。

启动完成后，代码应当以 `CurrentModRuntime.Value.EffectiveProfile` 作为当前有效 profile。`SessionProfiles/<sessionId>.xml` 只是启动前的恢复输入，不能在运行期当作“当前已生效模组”的判断依据。

持久内容仓库保存在实例的 `ContentRepositories.xml` 中，由仓库集合统一维护稳定 ID、名称、基础地址、启用状态和顺序。GUI 与 Headless
启动都先按精确 PackageHash 查询 ContentPackageCache，本地缺失时再使用启用的持久仓库；调用方不区分一个或多个仓库。
内容仓库和服务器目录分别由自身的 Manager 初始化、修改和保存配置；网络请求由 ContentServer Client 和服务器源客户端执行。`SettingsManager` 只读取 `Settings.xml` 中的通用实例设置。

远程联机握手中的 `RequiredModProfile` 只包含 Mod requirement；匿名临时候选仓库作为 `ServerInfoPackage` 的独立字段传输。
连接准备时临时仓库优先，持久仓库随后回退；临时集合不会写入实例配置、Profile、缓存索引或 pending session。GUI 只有在精确包
全部准备完成后才创建 pending session 并重启，非法 requirement 或全部来源失败不会留下待恢复状态。


### 方块目录与世界映射

Profile 决定加载哪些模组，不能替代方块编号映射。GUI 启动后构建 `PreviewCatalog`，
用于帮助、图鉴和创建世界预览；进入游戏后这些页面仍使用预览目录。
`WorldCatalog` 在读取世界数据前根据存档 `Project.xml/BlockRegistry` 建立，
退出世界后释放。Headless 只建立世界目录。两个目录分别持有方块实例和解析后的配方，
共享资源定义、模型与纹理；预览编号不写入世界。

同一 Profile 下切换不同编号映射的世界不需要重启。模组集合变化仍走现有 Profile 准备流程。
新世界先保存空映射，加载时分配模组编号并在任何世界数据写入之前持久化完整映射。
联机在传输连接建立后的 Bootstrap 中接收服务器映射，初始化世界目录后才解析 Project，
不通过启动参数或实例 Settings 保存服务器的临时世界编号。

本地存档缺少 `BlockRegistry` 时按空映射初始化。若没有模组编号需要分配，空节点随下一次正常保存
写入，不在加载时写盘；若分配了模组编号，则在子系统写入地形前保存完整映射。地形方块值的编号位宽不变，内置固定编号仍由内置表定义。
这只补齐缺失的元数据，不会把旧模组的硬编码编号自动转换为当前资源；后者仍需明确的旧编号对应关系。

### 远程目标与连接准备

远程 session 的 `RemoteTarget` 以独立 XML 子元素保存目标类型与逻辑定位字段。
地址目标保存 DNS 名称或 IP 和目标端口；邀请准备后保存信标控制地址、会话 ID 和中继策略。
`ServerPort` 继续表示监听端口覆盖，不再承担远程目标端口。不会保存 NAT 映射、中继分配端口、参与凭据或一次性票据。

NetPlay 每次连接或恢复都会重新准备路径并获取 ServerInfo，同一个宿主 socket 保持到正式握手。
随后核对模组并下载精确包；需要重启时先关闭准备资源，保存逻辑目标与临时 profile。
恢复后重新协商并重新核对模组要求，不复用旧端点或旧预检结果。无须重启时将宿主和路径一次性转交 NetNode。

### P2P房间会话覆盖

`P2POverrides` 随会话XML保存及重启恢复。有效值按本次请求、已保存会话、世界复制配置的顺序确定；未提供的值保留世界设置。覆盖只用于当前开服，不回写世界，也不读取实例安装列表。`--save` 保存覆盖到会话；世界选择信令服务时仍直接复制地址和名称。

## 桌面程序更新

Windows/Linux Starter 在注册 Storage、选择实例和加载游戏之前检查更新任务。`--apply-update <绝对任务路径>`
和 `--recover-update <绝对任务路径>` 仅供暂存发行包的 Starter 使用，不进入普通 `StartupRequest` 或 session。
正常启动时，Starter 先处理安装目录下 `.update/task.xml`；更新资料不写入实例配置或 `.runtime`。

游戏确认安装后调用 `GameExitManager.RequestUpdate`，以 `GameExitAction.Update` 结束游戏运行并将控制权交回 Starter。
已校验包路径和原下载地址只保存在本进程的临时请求中，不写入配置或 session；下次游戏初始化时清空。
平台注册下载目录、包后缀和确认文案，游戏不调用平台安装器，也不处理安装失败后的平台行为。
桌面 Starter 在游戏退出后准备更新事务和启动暂存进程；Android GameActivity 返回更新结果，
由 MainActivity 调用系统安装器并结束应用进程。安装请求失败由平台启动器反馈，旧发行文件保留。

- 正常：没有更新任务，按现有流程启动。
- 更新中：游戏下载并校验 ZIP，用户确认后游戏结束运行，Starter 核对 `DesktopRelease.xml` 和发行文件集合，解压到
  `.update/staged`，启动其中的新 Starter。新进程确认接手后，旧进程正常退出；新 Starter 等待旧 PID
  和启动时间对应的进程结束，再完整备份到 `.update/backup` 并替换发行文件。
- 已更新未清理：完成记录保留，用户重新启动正式目录中的 Starter 时清理暂存包、备份和任务，再进入游戏。
  清理失败只记录日志，下次重试，不阻塞正常启动。

同一安装目录的其他实例必须先退出，不强制结束它们。更新不修改 `Instances`、`Starter.xml` 或用户自建文件。
替换失败时恢复旧发行文件并删除本次新增的文件；回滚失败保留任务和备份。发现中断的替换任务时，正式 Starter
启动暂存 Starter 执行恢复并退出，恢复完成后请用户重新启动。准备阶段尚未修改正式文件的遗留任务可直接清理。
回滚仅覆盖文件替换失败或中断，不自动判断新版本的业务功能是否正常。

桌面安装需要可写的便携安装目录，不自动提权。无法执行自动更新时提供原下载链接；手动关闭所有实例并替换发行文件，
保留实例和配置。更新完成后不自动启动游戏。Headless 不检查版本、不注册游戏内更新入口，但首次启动仍会处理安装目录的
恢复或清理任务。

## P2P 自动开启与运行时控制

有效 P2P 配置仍只合并一次。普通端口先监听，世界加载完成后以 System 调用 StartP2PCommand；GUI 提前监听与 Headless 先加载世界的时序均由监听生命周期的自动请求协调。每个监听生命周期只自动请求一次，手工 stop 会消费尚未发送的自动请求。

运行时 p2p start/stop 只改变房间生命周期，不保存世界或 session，不清除启动覆盖。世界/会话中的 Enabled=false 不妨碍显式 start；后续启动仍使用原配置。配置保存与运行时启停不是同一操作，参见 [Headless](Headless.md)。
