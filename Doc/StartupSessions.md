# 启动会话

启动会话把“怎么启动游戏”拆成四个职责明确的对象：

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
- `-d` / `--server`: 设置 `RunMode=HeadlessServer`
- `--gui`: 设置 `RunMode=Gui`
- `--session <名称>`: 按名称选择或创建 session
- `--world <名称>`: 和 `--session` 一起使用，设置 session 的目标世界
- `--seed <种子>`: 和 `--session` 一起使用，设置新世界种子
- `--game-mode <模式>`: 和 `--session` 一起使用，覆盖 session 游戏模式
- `--log-level <级别>`: 设置日志级别
- `--window-mode <模式>`: GUI 窗口模式，可取 `Resizable`、`Borderless` 或 `Fullscreen`
- `--window-size <宽x高>`: GUI 窗口尺寸，例如 `1280x720`
- `--save`: 保存合并后的启动设置

`--world`、`--seed` 和 `--game-mode` 没有 `--session` 时会被忽略。

`--window-mode` 和 `--window-size` 只在 GUI 模式生效；Headless 会忽略它们，也不会因 `--save` 将其覆盖值持久化。

`--connect`、`--host`、`--player`、`--game-mode` 和端口覆盖不写入 `RunningSetting.xml`。`--host` 是强制调试参数：新世界直接创建为联机世界，已有非联机世界则更新并持久化其 `WorldSettings.RunServer=true`，之后统一走正常联机世界启动流程。未传 `--player` 时保持现有角色界面流程。`--connect` 与 `--host` 同时出现时按远程连接处理并忽略 `--host`。

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
- `ServerHost`
- `ServerPort`
- `BroadcastPort`
- `Password`

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
4. session 被消费后清理 pending 状态

Headless 不使用这个机制准备模组。它在启动阶段直接解析 profile、下载缺失包并启动 runtime。

## Mod Profile

模组 profile 决定本次运行加载哪些模组。

路径：

- 全局：`config:ModProfile.xml`
- 世界：`<world>/WorldModProfile.xml`
- 会话：`config:SessionProfiles/<sessionId>.xml`

`ModProfile.xml` 中的 `Package` 只保存：

- `ModId`
- `Version`

`PackageHash` 不进入用户可编辑的 profile 文件。实际加载和联机校验仍会在运行时使用包 hash。

启动完成后，代码应当以 `CurrentModRuntime.Value.EffectiveProfile` 作为当前有效 profile。`SessionProfiles/<sessionId>.xml` 只是启动前的恢复输入，不能在运行期当作“当前已生效模组”的判断依据。

仓库地址解析：

1. `ModProfile.RepositoryUrl`
2. `Settings.DefaultModRepositoryUrl`

远程服务器下发的 `RequiredModProfile.RepositoryUrl` 优先级最高，客户端不会用本地默认仓库覆盖它。
