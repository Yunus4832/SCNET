# 信令服务与网络探针部署

RendezvousServer 是独立于游戏和 ContentServer 的临时会话服务。它管理邀请、UDP 端点登记及
认证中继路由，不加载游戏、世界或模组，也不提供虚拟局域网。游戏不预设信令地址，邀请默认关闭；
IP/DNS 直连和 LAN 发现继续使用现有游戏监听端口。

## 发行物与本机运行

通过仓库脚本创建发行物：

```bash
./Scripts/publish.sh --project RendezvousServer --project NetworkProbe
```

PowerShell 对应命令：

```powershell
./Scripts/publish.ps1 -Project RendezvousServer,NetworkProbe
```

服务 ZIP 名为 `rendezvous-server-<版本>-portable.zip`，包含 `RendezvousServer.dll`、依赖、
运行时配置、`appsettings.json` 和 `Deployment/run.sh`。安装 .NET 10 ASP.NET Core 运行时后，
解压到独立目录并进入该目录：

```bash
dotnet RendezvousServer.dll
# 或在 Bash 主机上
bash Deployment/run.sh
```

Windows 同样在解压目录执行 `dotnet RendezvousServer.dll`。不需要数据库、Redis 或 MQ。
会话、票据及中继路由全部驻内存，进程重启会使旧邀请失效；运行中的游戏房主会退避重试并注册新码。
打洞直连已建立后不依赖信令在线；经过该进程的中继连接会随进程停止而中断。

开发阶段直接运行源码：

```bash
dotnet run --project RendezvousServer -- --urls http://127.0.0.1:31991
```

loopback 控制地址允许 HTTP。公网控制地址必须使用 HTTPS，客户端拒绝带凭据、查询或片段的控制 URI。

## Linux 容器发行物

在 Linux 执行 `Scripts/publish.sh --project RendezvousServer` 时，除 portable ZIP 外还会生成
`Publish/rendezvous-server-<版本>-linux-amd64.tar.gz`。构建需要 Podman 或 Docker，默认使用本机已有的
`mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled`；允许拉取时可设置 `IMAGE_PULL_POLICY=missing`。
Windows 发布脚本仍生成 portable ZIP，Linux PowerShell 发布同样生成容器包。

部署包包含镜像归档及 SHA-256、`compose.yaml`、`deploy.sh` 和 `server.conf.example`。
在 Linux 主机解压后：

```bash
cp server.conf.example server.conf
# 编辑 server.conf：设置公网 UDP 地址、中继 IP、端口池和 HTTPS 入口
bash deploy.sh
```

部署仅依赖 Bash、sha256sum、Docker Compose 或 Podman Compose，不要求主机安装 .NET、Python、
数据库或消息服务。脚本校验并导入镜像，然后启动容器。再次部署保留 `server.conf` 和 `Certificates/`；
不要用新模板覆盖实际配置。`--config PATH` 可指定配置文件，`--skip-import` 使用已导入镜像，
`--no-start` 只导入。`CONTAINER_ENGINE=docker` 或 `podman` 可指定引擎。
配置文件遵循 Bash 赋值语法，脚本直接读取，仅使用可信的配置文件。

容器使用 Linux host 网络，UDP 公布端口与实际监听端口一致，不需要生成端口映射或依赖容器内部 IP。
防火墙仍需开放 UDP 信令端口和中继端口池。模板默认中继池只有 **4 个端口**，与 portable 服务默认
128 个端口不同，可按带宽调整 `RELAY_PORT_COUNT`；端口数量限制的是同时中继路由数。
默认地址为 loopback，仅用于本机验证。公网部署必须修改 `UDP_ADVERTISED_HOST` 和
`RELAY_ADVERTISED_ADDRESS`。没有持久业务数据，重启会使未完成的邀请和中继连接失效。

HTTPS 有两种配置方式：

- 已有同机反向代理：保留 `CONTROL_URLS='http://127.0.0.1:31991'`，代理可信 HTTPS 域名到该地址；
  `TRUSTED_PROXY` 填写实际代理来源 IP。
- 直接 HTTPS：例如 `CONTROL_URLS='https://0.0.0.0:9520'`，将可信证书放在 `Certificates/`。
  PFX 设置 `TLS_CERTIFICATE_PATH='/certificates/server.pfx'` 和可选密码；PEM 设置
  `TLS_CERTIFICATE_PATH='/certificates/fullchain.pem'` 与 `TLS_CERTIFICATE_KEY_PATH='/certificates/privkey.pem'`。
  容器默认用户 UID 为 1654，证书和私钥必须对该用户可读，配置文件建议权限 600。

`CONTROL_URLS` 作为启动参数传入，避免 `appsettings.json` 的 loopback 地址覆盖容器监听设置。
发行物不附带证书，也不会自动申请证书。

## 配置和端口

编辑服务目录的 `appsettings.json`，命令行参数及 .NET 配置环境变量也可覆盖值。
例如 `Rendezvous__UdpPort=31990` 对应 `Rendezvous:UdpPort`。

| 配置 | 默认 | 含义 |
| --- | --- | --- |
| `Urls` | `http://127.0.0.1:31991` | HTTP 控制监听；适合本机测试或同机 HTTPS 反向代理。 |
| `Rendezvous:UdpPort` | `31990` | UDP 登记、挑战和候选交换端口。 |
| `Rendezvous:UdpAdvertisedHost` | `null` | 客户端连接的 UDP 主机名或 IP；为空时使用控制 URI 的主机名。 |
| `Rendezvous:RelayAdvertisedAddress` | `127.0.0.1` | 中继公布的 IP；公网部署必须设置实际可达 IP，目前不接受 DNS 名。 |
| `Rendezvous:RelayFirstPort` | `32000` | 独立中继端口池起点。 |
| `Rendezvous:RelayPortCount` | `128` | 端口数量，范围 1–256；每条路由独占一个端口。 |
| `Rendezvous:HttpRequestsPerMinute` | `600` | 每个来源 IP 的 HTTP 请求预算；房间目录与控制请求分别计数，同一 NAT 下的玩家共享各类预算。 |
| `Rendezvous:TrustedProxies` | `127.0.0.1`, `::1` | 可信 HTTP 代理地址；只接受这些地址提供的一层转发头。 |

允许 TCP HTTPS 入口（通常 443）、UDP 31990 和 UDP 32000–32127 通过主机及云平台防火墙。
若服务位于 NAT 后，UDP 登记端口及中继池需要保持公布端口的一对一映射；HTTPS 代理不能转发这些 UDP。
控制地址与 UDP 公布主机可分别配置。中继广告 IP 与端口必须是客户端实际可达的地址，不能保留 loopback。
中继 IP 支持 IPv4 或 IPv6，监听地址族随公布 IP 选择；双方必须能够到达该地址。
IPv6 IP 配置值直接写字面量（例如 `::1`），URL 与带端口的端点使用方括号（例如 `http://[::1]:31991`）。

## 同机 HTTPS 代理

可以让 HTTPS 代理监听公网 443，向 `http://127.0.0.1:31991` 转发全部控制路径。代理负责证书、
HTTPS 及转发 `X-Forwarded-For` / `X-Forwarded-Proto`。服务仅信任配置中的代理 IP，转发层数为一层。
代理在另一台机器或容器时，应把其实际来源 IP 加入 `TrustedProxies`；不要配置所有来源为可信代理。
没有正确配置时，代理之后的请求会共用代理 IP 的限流预算。

游戏实例安装或手工添加 HTTPS 信令服务；世界服务器设置选择服务并复制地址和名称，开启 P2P 后下次开服自动注册。玩家从安装列表选择服务输入联机码，或在独立信令分类浏览公开房间。
Headless 成功注册会输出六位联机码，GUI 房主可以在游戏菜单的邀请状态中复制。注册失败不停止普通监听，
信令恢复后重新注册可能更换联机码。玩家用同一信令地址和六位码申请加入，最终仍由游戏握手判断权限。

## 探针

`network-probe-<版本>-portable.zip` 只依赖 .NET 10，不依赖 Game、Window 或世界数据。
在探针目录执行：

```bash
dotnet NetworkProbe.dll scenario http://127.0.0.1:31991 auto
dotnet NetworkProbe.dll scenario-relay http://127.0.0.1:31991 auto
```

这两种模式分别验证候选直连和强制中继，在本机创建独立房主及客户端进程。`auto` 从控制 API 读取 UDP
配置；本机成功不能证明真实 NAT 或跨网可达性。也可以在不同机器上分别运行：

```bash
# 房主，记录 READY 中的六位码
dotnet NetworkProbe.dll host https://signal.example/ auto
# 另一台机器，使用该六位码
dotnet NetworkProbe.dll client https://signal.example/ auto 012345 0 Allowed
```

客户端末项可以是 `Disabled`、`Allowed` 或 `Required`，用于明确禁止、允许回退或强制中继。
`scenario-session` 验证不透明会话目标，`scenario-lease` 等待 70 秒并续约准备，再执行握手和回显。
退出码 0 与 PASS 记录表示相应探针场景通过，不表示游戏模组、Bootstrap、地形或 Android 生命周期已通过。

## TUN 与 Fake-IP 环境

若控制域名被代理软件解析为 Fake-IP，同一 UDP socket 的回包来源可能被改写为虚拟 IP，
与公布的中继端点不一致。DIRECT 规则本身未必排除这种改写。可将
`Rendezvous:UdpAdvertisedHost` 设置为真实公网 IP，或在代理配置中排除该域名的 Fake-IP 解析。
保持 HTTPS 控制地址使用证书域名，并检查实际接收端点；不要通过放宽中继来源校验绕过问题。

设置 `SCNET_PROBE_TRACE=1` 可启用 `SCNET-Engine-Network` EventSource 的中继接收诊断。
`unassigned-endpoint` 表示来源没有对应的已协商路由，`invalid-authentication` 表示认证不符，
`replayed` 表示序号重放；这些诊断不包含票据、密钥或报文内容。

## 生命周期和日志

默认邀请租约为五分钟，房主定期续约；默认中继未收到认证流量一分钟后回收，清理周期五秒。邀请及中继期限可通过下文配置调整。
关闭邀请会禁止新的加入，但不撤销已有中继连接。准备取消、连接断开和 Host 关闭会发送认证中继关闭通知；
通知丢失仍由空闲边界回收。回收端口重新分配时使用新的路由 ID 和密钥。
单条中继的 Socket 故障会结束该路由并记录警告；其他异常记录错误。故障路由由清理器回收，回收失败逐条隔离，不影响其他路由及信令服务。正常取消不记录故障。

按需要调整 `Logging:LogLevel`。诊断保留阶段、分类和错误码；不要记录或公开票据、主机/参与者凭据、
中继密钥和完整请求正文。六位码只是定位方式，不能代替游戏身份验证或中继认证。


UDP 请求未收到应答报告 `UdpUnavailable`；中继探测未收到应答报告 `RelayUnavailable`；
候选打洞超时或房主尚未注册报告 `HolePunchUnavailable`。诊断中的 Operation 与 ErrorCode 标明失败阶段。
允许中继时，UDP 探测或打洞失败会继续尝试中继；禁止中继时直接报告失败。
认证拒绝和无效响应不会自动转为中继尝试。用户取消保留取消结果，不显示为网络超时。
准备租约在等待续约或传输握手时到期，报告握手超时；释放准备对象触发的取消仍按取消处理。

公网中继已通过桌面宽带与物理 Android 蜂窝网络之间的实际游戏验证，包括进入世界、
前后台超时提示、Wi-Fi/蜂窝切换后的断开与重新协商加入。切换不保证无缝迁移。
游戏弱网验证覆盖有限延迟、随机丢包和短断流后的区块收敛，不能外推其他网络条件。

隔离实验覆盖固定和目标相关映射、地址及地址/端口过滤、双层 NAT、回环开关、单族 UDP 阻断、
IPv4/IPv6 双栈候选，以及低 MTU、突发丢包下的探针可靠收敛。
可重复脚本及依赖见 [NetworkProbe](../NetworkProbe/README.md)。这些属于模拟拓扑或传输层验证，
模拟场景本身不证明真实公网可达性，也不能代替实际游戏低 MTU、NAT 映射失效等未验证场景。公网 IPv6 已在两台蜂窝网络 Android 设备间完成一轮实际游戏连接验证；这不能外推运营商成功率。IPv4 信令可以交换两端的 IPv6 候选，信令服务器无需具有 IPv6 地址。

UDP 协商的结构化关联日志位于 `RendezvousServer.UdpSignalHost` 分类，级别为 Debug。
通过 `Logging:LogLevel:RendezvousServer.UdpSignalHost=Debug` 开启，记录操作、会话、参与者、尝试 ID、结果和错误码，
不记录票据、凭据或挑战内容。HTTP 默认请求日志只记录请求处理信息。

HTTP 控制操作的结构化日志位于 `RendezvousServer.SignalControlLogFilter` 分类，级别为 Debug。
记录操作路径、HTTP TraceId、可取得的会话/参与者/尝试 ID、状态码和错误码；创建和加入成功后使用返回的身份关联。
未建立会话的失败不会伪造会话身份，仍可通过 TraceId 关联标准 HTTP 日志。过滤器不序列化请求或响应正文。

### 邀请容量与期限

`Rendezvous:InvitationLifetimeSeconds` 默认 300 秒，允许 30–86400 秒；`Rendezvous:InvitationCapacity` 默认 1000，允许 1–1000000 个活跃邀请。环境变量分别为 `Rendezvous__InvitationLifetimeSeconds` 和 `Rendezvous__InvitationCapacity`。无效值会使服务启动失败。容量限制针对整个信标进程，邀请续约使用配置的期限。

票据、登记与中继预算也可通过 `Rendezvous` 配置或同名双下划线环境变量设置：

| 配置 | 默认 | 启动允许范围 |
| --- | --- | --- |
| `TicketLifetimeSeconds` | 30 秒 | 1–300 秒 |
| `RegistrationLifetimeSeconds` | 60 秒 | 60–3600 秒 |
| `RelayLifetimeSeconds` | 60 秒 | 60–3600 秒 |
| `RelayBytesPerSecond` | 1048576 字节 | 2048–1073741824 字节 |

登记与中继期限下限保留了现有客户端续约间隔的余量。中继流量预算按路由、双向合计，以收到的封装数据报字节数计入一秒窗口；超出预算的报文不转发。它不是整个服务的总带宽限制。

UDP 控制请求限额：`UdpRequestsPerAddressPerSecond` 默认 120，`UdpRequestsPerSecond` 默认 4096。均不超过 1000000，总额度不能小于单来源额度；无效配置拒绝启动。控制收件队列仍固定最多 256 条，独立于速率窗口。

## 公开房间目录

`GET /transport` 的 `roomDirectoryPath` 声明公开房间目录入口。参考服务使用
`GET /rooms?limit=100&cursor=...`；单页最多100个房间，返回 `rooms` 和 `nextCursor`。
房间条目包含本次注册的 ID、名称、简介、六位数字字符串联机码和 `connectionMethods`。
连接方式声明服务可提供直连准备或中继，不保证当前网络一定能直连。列表读取不会加入房间或分配中继。

房主使用 `POST /invitations/update-room` 更新资料，请求包含 `sessionId`、`credential` 和
`publication`（`name`、`description`、`isPublic`）。名称最多100个字符，简介最多1024个字符。
注册默认不公开；只有公开且邀请和已验证房主端点租约均有效的房间进入目录。
修改资料不延长邀请租约；隐藏、关闭或过期后不再返回。

分页游标属于当前服务进程，服务重启后失效；本轮分页期间新增房间需要下一次刷新才会出现。
目录不保证房间仍能加入，客户端连接时继续通过联机码执行现有协商和游戏握手。
公开目录不包含房主凭据、票据或候选地址。

控制接口请求体最多16 KiB，控制响应客户端读取预算为1 MiB。房间浏览的限流额度与邀请创建、续期等控制请求分开，刷新目录不会耗尽控制请求额度。
