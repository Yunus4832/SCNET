# Headless 模式

Headless 模式在不打开游戏窗口的情况下运行服务端逻辑，适合部署独立联机服务器。

它复用同一套世界、网络和模组运行时，但跳过图形界面流程。启动目标和世界都通过启动会话系统解析，详见 [StartupSessions.md](./StartupSessions.md)。

## 启动方式

### Windows

```bash
SurvivalcraftStarter.exe --server
```

等价短参数：

```bash
SurvivalcraftStarter.exe -d
```

### Linux

```bash
./SurvivalcraftStarter --server
```

开发环境可以直接运行 Linux 项目：

```bash
dotnet run --project Survivalcraft.Linux/Survivalcraft.Linux.csproj -- --server
```

### Android

Android 同样通过 `RunningSettingManager` 选择 GUI 或 Headless。正常应用重启会读取 `config:RunningSetting.xml`；ADB 调试也可以通过 `Survivalcraft.Android.CommandLine` Intent Extra 临时传入 `--server`、`--session`、`--world`、`--game-mode` 等同一套参数。`Survivalcraft.Android.InstanceId` 用于选择隔离的数据实例。

## 常用参数

- `--instance <实例名>`: 选择或创建 `Instances/<实例名>` 数据实例；省略时由 `Starter.xml` 选择
- `--server-port <端口>`: 仅覆盖本次运行的游戏端口
- `--broadcast-port <端口>`: 仅覆盖本次运行的广播端口
- `--http-command`: 为本次有效 session 启用 loopback HTTP 命令宿主
- `--no-http-command`: 为本次有效 session 禁用 loopback HTTP 命令宿主
- `--http-command-port <端口>`: 覆盖 loopback HTTP 命令宿主的默认端口 `28889`
- `--http-command-access-token <Token>`: 覆盖本次运行使用的 Bearer Token；至少 32 个字符
- `-d` / `--server`: 切换到 `HeadlessServer`
- `--session <名称>`: 选择或创建一个具名启动会话
- `--world <名称>`: 给 `--session` 指定世界名或世界目录名
- `--seed <种子>`: 给 `--session` 指定新建世界时使用的种子
- `--game-mode <模式>`: 给 `--session` 指定游戏模式覆盖
- `--log-level <级别>`: `Debug`、`Verbose`、`Information`、`Warning`、`Error`
- `--save`: 保存适用的 `RunningSetting` 字段，并将当前有效 session 写入 `SessionInfo.xml`

`--world`、`--seed` 和 `--game-mode` 只有在同时指定 `--session` 时才会生效。没有 `--session` 时它们会被忽略，避免一次临时命令覆盖默认启动状态。
模式可取 `Creative`、`Harmless`、`Survival`、`Challenging`、`Cruel`、`Adventure`。新世界以该模式创建；已有世界仅在有效 session 中使用该模式，保存时仍保留原存档模式。
端口和游戏模式覆盖默认只作用于内存中的有效 session；与 `--save` 同时使用时才会写入 `SessionInfo.xml`。Session 未指定端口时回退到 `Settings.xml` 的默认端口。

示例：

```bash
./SurvivalcraftStarter --instance server --server --session survival --world World --seed 123456 --game-mode Creative --log-level Information --save
```

再次启动同一个服务器：

```bash
./SurvivalcraftStarter --instance server --server --session survival
```

## HTTP 命令宿主

HTTP 命令宿主默认关闭；`Settings.xml` 的 `HttpCommandEnabled` 是实例默认开关，启动参数或保存的 session 可以覆盖它。启用后默认使用 `28889`，并且当前只监听 `127.0.0.1`。可以为多实例指定其他端口：

```bash
./SurvivalcraftStarter --instance server --server --session survival --http-command --http-command-port 29889
```

端口和长期 access token 保存在当前实例的 `Settings.xml`，默认端口为 `28889`。旧配置没有 `HttpCommandAccessToken` 或 token 无效时，加载设置后会生成一个 256-bit 随机 token 并立即保存。配置端口无效、端口被占用或监听失败时会记录明确错误，并且本次运行不启动 HTTP Host；游戏和服务器本身继续启动，不会静默改用其他端口。

`--http-command`、`--no-http-command`、`--http-command-port` 和 `--http-command-access-token` 会先合并进本次有效 `SessionInfo`，优先级高于 `Settings.xml`。默认只影响本次启动；与 `--save` 同用时写入对应命名 session，之后按该 session 启动可以恢复相同的 HTTP 配置覆盖。命令行参数可能被本机进程查看工具读取，因此长期部署应保护好实例的 `Settings.xml` 和 `SessionInfo.xml`。

```http
POST /commands
Authorization: Bearer <token>
Content-Type: application/json

{"identity":"game:world/time/get","arguments":{}}
```

HTTP 只暴露显式注册了 `HttpCommandBinding` 的命令。Headless HTTP 请求以 `ServerOperator` 执行，但仍受命令域、宿主要求和权限规则约束。当前监听仅限本机；跨主机管理应通过 SSH 隧道等受保护的传输访问，不应直接公开端口。

认证后可通过 `GET /commands` 发现当前宿主实际可执行的 HTTP 命令。返回值包含稳定 identity、本地化说明和显式声明的参数名、类型与必填状态；它已按当前运行模式、宿主主体和权限过滤。客户端应先发现命令，再以同一 identity 向 `POST /commands` 发送调用请求。

```bash
curl -H "Authorization: Bearer <token>" http://127.0.0.1:28889/commands
```

GUI 模式还会发现 `game:automation/ui/context/get`、`tap`、`scroll`、`swipe`、`key`、`screenshot`，以及相对鼠标输入 `game:automation/input/mouse/move`。上下文命令返回当前 Screen、Dialog、目标 Widget 的 selector、文本、逻辑边界与支持的 actions；点击、滚轮、跨帧触摸滑动和相对鼠标移动都通过 Engine 合成输入执行。Headless 不会发现或执行这些 GUI 专用命令。

## 数据实例

Starter 首先在程序基础目录注册 `starter:`，读取 `starter:Starter.xml`，再将选中实例的目录注册为游戏使用的 `external:`、`data:` 和 `config:`。实例目录位于：

```text
Instances/<实例名>/
```

因此不同实例拥有独立的设置、身份、世界、模组、缓存和日志。`--instance` 由 Starter 消费，不会写入 `RunningSetting.RemainingArgs`。不存在的命令行实例会自动创建。

## 配置文件

Headless 启动主要涉及三个配置文件。

### RunningSetting.xml

路径：`config:RunningSetting.xml`

`RunningSetting.xml` 只保存启动入口层面的状态，不保存世界名、种子或游戏模式覆盖。

示例：

```xml
<RunningSetting RunMode="HeadlessServer" LogLevel="Information"
                WindowMode="Resizable" WindowWidth="0" WindowHeight="0"
                DefaultSessionId="" PendingSessionId="">
  <RemainingArgs />
</RunningSetting>
```

字段说明：

- `RunMode`: `Gui` 或 `HeadlessServer`
- `LogLevel`: 最低日志级别
- `WindowMode` / `WindowWidth` / `WindowHeight`: GUI 窗口设置；Headless 保留字段但不使用窗口
- `DefaultSessionId`: 没有命令行指定 session 时使用的默认会话 id
- `PendingSessionId`: 重启恢复使用的临时会话 id
- `RemainingArgs`: 启动器未消费、需要保留的参数

### SessionInfo.xml

路径：`config:SessionInfo.xml`

这里保存多个具名或临时启动会话。`StartupManager` 将 `RunningSetting`、本次 `StartupRequest` 与选中的 session 合并成 `StartupContext`；Headless 直接使用其中的有效 session，得到目标世界、种子、游戏模式覆盖和端口。

Headless 使用的会话通常是：

```xml
<Sessions>
  <SessionInfo
    SessionId="..."
    Name="survival"
    Target="World"
    World="World"
    Seed="123456"
    GameMode="Creative"
    ServerHost=""
    ServerPort="0"
    BroadcastPort="0"
    Password="" />
</Sessions>
```

`GameMode` 是可选字段。旧 session 没有该字段时，不会覆盖存档模式。

### ModProfile.xml

路径：

- 全局 profile：`config:ModProfile.xml`
- 世界 profile：`<world>/WorldModProfile.xml`
- 会话 profile：`config:SessionProfiles/<sessionId>.xml`

Headless 启动时会解析当前会话对应的有效模组 profile，并确保缺失的包已下载到本地缓存。解析结果会直接成为本进程的 `CurrentModRuntime.Value.EffectiveProfile`。Headless 不通过“请求重启”来准备模组。

## 世界解析

Headless 启动时：

- 如果 session 指向的世界存在，直接加载该世界
- 如果世界不存在，会按 session 中的 `World` 和 `Seed` 创建
- 如果世界已存在，`Seed` 不再影响该世界
- session 含 `GameMode` 时，新世界以该模式创建；已有世界以该模式运行，但保存仍保留原存档模式
- 如果世界没有开启 `RunServer`，Headless 会自动启用它并保存世界设置

## 模组仓库

模组下载地址按以下顺序解析：

1. 当前有效 `ModProfile.RepositoryUrl`
2. `Settings.DefaultModRepositoryUrl`

如果两者都为空，只能使用本地已经存在的模组包。

## 运行时行为

Headless 启动后会：

- 初始化设置、内容、包管理和本地模组导入
- 解析启动会话和有效模组 profile
- 下载缺失的 required mods
- 启动服务端侧模组 runtime
- 加载或创建世界
- 启动游戏端口和广播端口
- 运行 20 TPS 左右的主循环
- 退出时保存世界和设置

Headless 进程运行期间不会像 GUI 一样在进入 world 前弹窗请求重启；如果要切换模组组合，需要用新的 session/profile 重新启动进程。

可以通过 `Ctrl+C` 终止进程。
