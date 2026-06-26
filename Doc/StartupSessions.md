# 启动会话

启动会话把“怎么启动游戏”拆成两层：

- `RunningSetting`: 当前进程的启动入口状态
- `SessionInfo`: 可恢复的具体目标，例如世界、服务器浏览器或远程服务器

这样 GUI、Headless、远程联机重启和模组下载后的恢复都可以走同一套流程。

## RunningSetting

路径：`config:RunningSetting.xml`

职责：

- 记录 `RunMode`
- 记录日志级别
- 指向默认或 pending session
- 保存未消费的启动参数

它不保存世界名、种子、远程服务器地址或密码。这些属于 session 状态。

当前字段：

- `RunMode`
- `LogLevel`
- `DefaultSessionId`
- `PendingSessionId`
- `RemainingArgs`

命令行参数：

- `-d` / `--server`: 设置 `RunMode=HeadlessServer`
- `--gui`: 设置 `RunMode=Gui`
- `--session <名称>`: 按名称选择或创建 session
- `--world <名称>`: 和 `--session` 一起使用，设置 session 的目标世界
- `--seed <种子>`: 和 `--session` 一起使用，设置新世界种子
- `--log-level <级别>`: 设置日志级别
- `--save`: 保存合并后的启动设置

`--world` 和 `--seed` 没有 `--session` 时会被忽略。

## SessionInfo

路径：`config:SessionInfo.xml`

一个 session 包含：

- `SessionId`
- `Name`
- `Target`
- `World`
- `Seed`
- `ServerHost`
- `ServerPort`
- `Password`

`Target` 可取：

- `MainMenu`
- `WorldList`
- `World`
- `ServerBrowser`
- `RemoteServer`

Headless 使用 `World` session。远程联机重启使用 `RemoteServer` session。

## Pending Session

当 GUI 客户端为了下载服务器 required mods 需要重启时，会创建一个临时 session，并把它的 id 写入 `RunningSetting.PendingSessionId`。

下一次启动时：

1. `PendingSessionId` 成为 active session
2. 启动流程恢复到对应目标
3. session 被消费后清理 pending 状态

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

仓库地址解析：

1. `ModProfile.RepositoryUrl`
2. `Settings.DefaultModRepositoryUrl`

远程服务器下发的 `RequiredModProfile.RepositoryUrl` 优先级最高，客户端不会用本地默认仓库覆盖它。
