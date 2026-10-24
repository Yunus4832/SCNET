# Headless 模式

Headless 模式在不打开游戏窗口的情况下运行服务端逻辑，适合部署独立联机服务器。

它复用同一套世界、网络和模组运行时，但跳过图形界面流程。启动目标和世界都通过启动会话系统解析，详见 [StartupSessions.md](./StartupSessions.md)。

## 启动方式

### 容器部署

Linux 主机执行 `./Scripts/publish.sh --project Survivalcraft.Linux` 会同时生成桌面 ZIP 和 `Publish/game-server-<版本>-linux-amd64.tar.gz`。构建机需要 Podman 或 Docker，基础镜像为 `mcr.microsoft.com/dotnet/runtime:10.0`；默认只使用本地已有基础镜像，可用 `IMAGE_PULL_POLICY=missing` 允许构建时拉取。镜像使用同一 `SurvivalcraftStarter` 二进制文件，以 `--server` 参数运行。

将镜像包复制到部署机器并解压。首次执行 `./deploy.sh` 会创建 `server.conf` 并要求编辑；按 `server.conf.example` 填入实例、会话、世界、种子、游戏模式、游戏 UDP 端口，以及可选的 ContentServer URL 和模组列表。再次运行脚本会校验并导入镜像、查询所选模组的精确 PackageHash、生成实例的 `Config/ModProfile.xml`、更新内容仓库配置，再通过 Compose 启动服务器：

```bash
tar -xzf game-server-<版本>-linux-amd64.tar.gz
./deploy.sh
# 编辑 server.conf
./deploy.sh
```

配置文件是由服务器管理员维护的可信 Shell 文件。`MODS` 每行填写一个 `模组标识 版本号`；空列表无需内容服务器。配置模组时，部署主机需要能访问 ContentServer，容器自身也需要能够访问该地址来下载缺失的包。部署脚本会在实例的 `ContentRepositories.xml` 中配置内容服务器，并同步全局、当前世界和当前会话的模组 profile；`Instances/` 目录持久化世界、设置和模组缓存。

端口和世界参数来自 `server.conf`，每次启动都会用 `--save` 保存启动会话。种子只在首次创建世界时生效。容器使用 Linux 宿主网络，游戏 UDP 端口直接监听宿主机；容器不启用用于局域网发现的广播端口。部署主机还需要 `sha256sum`、Python 3 和相应的 Docker Compose 或 Podman Compose。

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
- `--no-broadcast`: 禁用当前会话的局域网发现广播，容器部署默认使用
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

## P2P 房间运行时管理

服务器监听和世界加载就绪后，已启用的世界／会话配置通过 System 命令自动注册房间。GUI 与 Headless 使用同一流程；未启用的服务器也可在运行中开启，无需重新加载世界。

```text
p2p start
p2p status
p2p stop
```

`p2p start` 使用启动时捕获的存档／会话配置；没有预先配置有效信令地址和房间信息时拒绝执行。命令不提供服务选择或配置修改参数，配置通过 GUI 存档服务器设置或启动参数完成。

三项命令使用 Server 域及标准权限 `game:server.p2p.manage`。本地控制台和控制 HTTP 使用 LocalOperator；GUI 房主玩家与远程玩家同等授权，认领权限管理能力不会自动授予该使用权。

本地进程使用 GET/POST /commands，GUI 玩家使用 GET/POST /player/commands；identity 分别为 `game:server/p2p/start`、`game:server/p2p/status`、`game:server/p2p/stop`。先通过对应入口发现命令及参数。

start／stop 的 Pending 表示注册或清理尚未完成，通过 status 的 Data.state 确认 Active／Disabled。重复开启幂等；Stopping 期间拒绝再次开启。HTTP 超时不撤销已经受理的操作，应先查询实际状态。

运行时启停不写回世界、会话或实例设置。下次启动仍按原世界设置和会话覆盖决定是否自动启用；当前没有独立的保存命令。

停止撤销房间和新的信令协商，取消重试，保留普通游戏监听及已建立的直连、中继玩家。已获知普通服务器地址的玩家仍可直连。注销失败时状态保留错误，远端房间可能保留至租约到期；这不是禁止新玩家进入服务器的准入开关。

### 容器 HTTP 远程控制

在 `server.conf` 中配置：

```bash
HTTP_COMMAND=true
HTTP_COMMAND_PORT=28889
HTTP_COMMAND_ACCESS_TOKEN=''  # 留空使用实例自动生成并持久保存的 Token
```

重新运行 `./deploy.sh` 后生效。开关和端口通过启动参数覆盖并保存到部署会话；Token 非空时同样作为会话覆盖，须至少 32 个非空白字符；留空会清除该会话的 Token 覆盖，使用实例 `Config/Settings.xml` 中的 `HttpCommandAccessToken`。脚本不输出 Token。

容器采用宿主网络，无需 Compose `ports` 映射。HTTP 仅监听宿主机 `127.0.0.1`，通过 SSH 隧道访问：

```bash
ssh -N -L 29889:127.0.0.1:28889 server.example
curl -H "Authorization: Bearer <token>" http://127.0.0.1:29889/commands
```

将 `server.example` 替换为部署主机，将隧道目标端口改为配置的 `HTTP_COMMAND_PORT`。本地转发端口可自行选择；不需要对外开放 HTTP 端口。设为 `HTTP_COMMAND=false` 并重新部署即可关闭。

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

HTTP 只暴露显式注册了 `HttpCommandBinding` 的命令。Headless HTTP 请求以 `LocalOperator` 执行，但仍受命令域、宿主要求和权限规则约束。当前监听仅限本机；跨主机管理应通过 SSH 隧道等受保护的传输访问，不应直接公开端口。

认证后可通过 `GET /commands` 发现当前宿主实际可执行的 HTTP 命令。返回值包含稳定 identity、本地化说明和显式声明的参数名、类型与必填状态；它已按当前运行模式、宿主主体和权限过滤。客户端应先发现命令，再以同一 identity 向 `POST /commands` 发送调用请求。

```bash
curl -H "Authorization: Bearer <token>" http://127.0.0.1:28889/commands
```

GUI 模式还提供 UI 发现、点击、拖放、滚动、截图、组合键鼠输入、文本输入、角色观测和步行导航，完整合同见 [游戏自动化](Automation.md)。操作通过正常输入和角色运动路径执行。Headless 不会发现或执行这些 GUI 专用命令。

GUI 与 Headless 都提供只读命令 `game:diagnostics/network/get`，无参数。返回当前 NetNode 生命周期内的连接内发送累计统计：通道的应用层发送批次与编码字节，以及各包类型的接收方投递次数与未压缩载荷字节。两次读取的差值可用于比较测试时间段；编码字节不含传输协议头、底层重传与分片开销，载荷字节不含包标记且不能与编码字节相加。未发送或延后的快照不计入，无连接发现消息不计入。该命令仅供本地操作者使用，不向普通联机玩家开放。

Headless 的诊断还返回 `HeadlessTicks`，记录进程内累计 tick 工作耗时与超预算次数；GUI 为 null。
独立瘦协议客户端负载工具及结果解读见 [服务端负载测试](ServerLoadTesting.md)。

## 玩家与世界命令

玩家标记、传送、授权和消息面板行为不依赖 Headless 模式，见
[玩家传送与标记](Teleportation.md)。身份、执行域及本地／玩家 HTTP 适配规则见
[命令权限模型](CommandPermissions.md)。

## 数据实例

Starter 首先在程序基础目录注册 `starter:`，读取 `starter:Starter.xml`，再将选中实例的目录注册为游戏使用的 `external:`、`data:` 和 `config:`。实例目录位于：

```text
Instances/<实例名>/
```

因此不同实例拥有独立的设置、身份、世界、模组、缓存和日志。`--instance` 由 Starter 消费，不会写入 `RunningSetting.RemainingArgs`。不存在的命令行实例会自动创建。

启动初始化时会准备实例的用户和服务器身份，不依赖 GUI 或首次开服。私钥直接保存在实例的
`Config/LocalIdentities.xml` 中；部署备份须保留实例目录，并限制访问。复制实例会复制身份，
不再依赖宿主系统的外部密钥存储。详细格式与备份恢复见 [实例身份](StartupSessions.md#实例身份)。

## 配置文件

以下列出启动入口与模组配置；游戏设置、内容仓库及信令服务安装列表的路径见
[文件定位](FileStorage.md)。

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
    BroadcastPort="0" />
</Sessions>
```

`GameMode` 是可选字段，未指定时不会覆盖存档模式。

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

## 内容服务

Headless 与 GUI 使用相同的 `ContentSourceContext` 和统一下载服务。每个实例在 `ContentRepositories.xml` 中维护有序的持久内容仓库集合；
启动先按 Profile 的 `ModId + Version + PackageHash` 查询 ContentPackageCache，只有精确包缺失时才按启用仓库顺序查询和下载。
首选来源失败后，只会回退到声明同一 PackageHash 的其他来源。

Profile 不保存仓库地址。未配置可用仓库时，只要所有 requirement 已在本地缓存，Headless 仍可完全离线启动；否则启动会返回
包含缺失精确身份和来源失败的诊断错误。Headless 不持有联机客户端接收的临时仓库作用域。

## 运行时行为

Headless 启动后会：

- 初始化设置、内容和统一包缓存
- 解析启动会话和有效模组 profile
- 下载缺失的 required mods
- 启动服务端侧模组 runtime
- 加载或创建世界
- 启动游戏端口和广播端口
- 运行 20 TPS 左右的主循环
- 退出时保存世界和设置

Headless 进程运行期间不会像 GUI 一样在进入 world 前弹窗请求重启；如果要切换模组组合，需要用新的 session/profile 重新启动进程。

可以通过 `Ctrl+C` 终止进程。

## 邀请联机

邀请是现有监听 Host 上的可选能力，开启后仍接受 IP/DNS 直连及已启用的 LAN 发现。游戏不内置
信令地址。房主配置保存在世界的 `P2P` 值组中，包含 `Enabled`、`ControlUrl`、`ServiceName`、
`Name`、`Description` 和 `IsPublic`。GUI 的世界服务器设置在页面末尾提供 P2P 开关，信令服务、房间名称、简介和公开房间选项始终显示，服务器总开关关闭时禁用全部服务器配置，开启后再由 P2P 开关控制其配置项，已有值保留。
选择实例已安装的信令服务会复制其地址和名称；
修改或删除安装记录不会改变世界配置。Headless 与 GUI 共用这份世界开服配置。

默认 P2P 关闭且房间不公开。开服注册成功会在日志中输出六位联机码；公开房间进入信令服务的房间目录。
信令失败不停止游戏监听，服务恢复后房主会退避重试，重新注册时重新提交房间资料并可能取得新码。
实例中的 `InvitationRelayPolicy` 只供加入端选择是否允许中继，房主仍为授权加入者接收中继路径。
信令服务、HTTPS 和 UDP 端口配置见 [信令服务部署](RendezvousDeployment.md)。

### 容器的信令房间

`server.conf` 的 `P2P` 默认 `false`，`ROOM_PUBLIC` 默认 `false`。设置 `P2P=true`、`RENDEZVOUS=https://signal.example/`，并填写 `ROOM_NAME`、`ROOM_DESCRIPTION` 后部署，即可尝试注册信令房间。公开房间还需 `ROOM_PUBLIC=true`。这些字段映射为会话启动覆盖，由现有 `--save` 保存到会话，不修改世界配置或实例安装列表。信令失败不关闭普通游戏端口。
