# 网络探针

该工具使用 Engine.Network 的实际 LiteNetLib socket，验证信令注册、候选挑战、正式握手和可靠回显。无 Game、Window 或世界数据依赖。该工具支持邀请直连及中继准备；游戏本体已有独立接入路径，探针不代替游戏验收。

发布 ZIP 包含本说明与 `scripts/`。下文默认路径适用于仓库 Debug 构建；使用解压后的发行目录时，显式指定程序集路径，例如：

```bash
python3 scripts/local-lab.py --probe ./NetworkProbe.dll \
  --server ../rendezvous/RendezvousServer.dll --output /tmp/scnet-package-test
bash scripts/nat-lab.sh --probe-dir . --server-dir ../rendezvous \
  --output /tmp/scnet-package-nat
```

`../rendezvous` 应替换为实际信令服务解压目录。普通使用探针的 `host`、`client`、`scenario` 模式只需要 .NET，维护脚本的系统依赖另见对应章节。

## 本机验证

### 无权限验证入口

仓库中的 POSIX CI worker 或开发机可运行：

```bash
python3 Scripts/test-network.py --output /tmp/scnet-network-gate
```

需要 .NET 10 SDK、Python 3 和可用的本机 TCP/UDP 端口，不需要 root、界面、Android workload、Podman 或网络隔离工具。
依次运行协议、Engine、信令服务三个测试项目，再构建探针并验证候选、中继、会话定位和准备完成后取消。
保留各步骤日志、TRX、结构化场景证据及总结果，任何一步不符即返回非零。输出目录必须尚不存在；默认使用 39191–39196，可通过 `--base-port` 修改。
已还原依赖时可加 `--no-restore`；默认允许 `dotnet` 还原项目依赖，不安装 SDK。
这是一条平台无关的 CI 调用入口，没有自动配置或启用 Gitee 等托管 CI。高权限 NAT 与物理设备验收另行执行。

从仓库根目录编译：

```bash
dotnet build RendezvousServer/RendezvousServer.csproj
dotnet build NetworkProbe/NetworkProbe.csproj
```

先启动信令服务器：

```bash
dotnet RendezvousServer/bin/Debug/net10.0/RendezvousServer.dll \
  --urls http://127.0.0.1:31991 --Rendezvous:UdpPort 31990
```

另一个终端运行场景，它启动独立的房主与客户端进程：

```bash
dotnet NetworkProbe/bin/Debug/net10.0/NetworkProbe.dll \
  scenario http://127.0.0.1:31991 127.0.0.1:31990
```

场景入口连接已运行的信令服务，不负责启动或停止该服务。普通场景预算为 45 秒；显式延长准备的场景预算另计；场景失败或取消后回收两端子进程。看到两端 PASS 且退出码为零，才表示本次场景完成。scenario 使用本机两端，不能作为 NAT 穿透证据。
父场景通过当前用户专用的本地命名管道请求子进程取消，先等待正常关闭和最终资源样本；控制通道建立最多等待 2 秒，关闭预算为每端 8 秒，超时才强制回收。该通道不参与游戏协议，也不会写入会话恢复信息。

### 自动管理完整本机场景

在 POSIX 主机上，Python 3 标准库脚本可管理临时信令服务和两端探针：

```bash
python3 NetworkProbe/scripts/local-lab.py --output /tmp/scnet-local-test
python3 NetworkProbe/scripts/local-lab.py --mode scenario-relay --output /tmp/scnet-relay-test
```

先执行上面的两个项目构建。脚本不构建、不下载运行时；需要 Python 3 和 .NET 10。
默认 TCP 控制端口 31991、UDP 信令端口 31990、中继 UDP 32000–32003，可用
`--http-port`、`--udp-port`、`--relay-port` 修改。控制入口绑定 loopback，UDP 广告地址使用 loopback；UDP socket 沿用信令服务的所有接口绑定。
`--probe` 和 `--server` 可指定已有程序集。输出目录必须尚不存在。
`lab-manifest.json` 保存配置和两份入口程序集哈希，`lab-summary.json` 保存退出结果；服务与探针日志及两端结构化结果也保留。
服务启动失败不启动探针；正常结束、超时、Ctrl+C 或 SIGTERM 后回收本轮创建的进程组。
原生运行时可用 `--mode scenario-lease --cancel-after-path`：等到准备路径确认后，仅终止父探针，要求父进程通过私有通道正常取消两端，并检查最终资源归零。该选项不等待完整的 70 秒租约场景，也不支持 Podman 模式。

### 隔离地址族验证

Linux 的 `local-lab.sh` 先创建独立网络命名空间，再启动上述场景；需要 `unshare`、`ip` 和 `nft`：

```bash
bash NetworkProbe/scripts/local-lab.sh --family ipv4 --output /tmp/scnet-ipv4
bash NetworkProbe/scripts/local-lab.sh --family ipv6 --mode scenario-relay --output /tmp/scnet-ipv6-relay
bash NetworkProbe/scripts/local-lab.sh --family dual --output /tmp/scnet-dual
```

IPv4/IPv6 模式阻断另一地址族的输出，并用独立报文及 nft 计数器校准；
双栈模式允许两族，控制地址使用 `localhost`，UDP 使用 IPv6。它们验证隔离 loopback 下的地址族处理，不能作为公网 IPv6 或跨网打洞证据。
输出额外保存接口和阻断规则。直接运行 Python 的 `--family` 只选择地址，不执行隔离；`--isolated` 拒绝在原宿主命名空间中运行。
没有本机 .NET 时，可显式传入 `--runtime-image` 使用缓存的 Podman 镜像，并用 `--probe`、`--server` 指定程序集。
容器复用实验命名空间，结束时只删除本轮容器；脚本不拉取镜像。容器模式应在 root 专用测试环境使用。

## 逻辑目标与自动 UDP 配置

将 UDP 地址参数写为 `auto` 时，探针使用 Engine 的逻辑目标入口。Engine 从控制地址下的
`GET /transport` 读取 UDP 端口；`Rendezvous:UdpAdvertisedHost` 未设置时使用控制地址的 DNS 名称。
公网控制地址必须是 HTTPS；HTTP 仅限 loopback 实验。服务器和客户端各自在自己的实际宿主 socket 上注册。

```bash
dotnet NetworkProbe/bin/Debug/net10.0/NetworkProbe.dll scenario http://127.0.0.1:31991 auto
dotnet NetworkProbe/bin/Debug/net10.0/NetworkProbe.dll scenario-session http://127.0.0.1:31991 auto
dotnet NetworkProbe/bin/Debug/net10.0/NetworkProbe.dll scenario-relay http://127.0.0.1:31991 auto
```

`scenario-session` 使用房主输出的不透明会话 ID，在新客户端进程中重新申请参与身份和路径；它不会复用票据、凭据或端点。
`client` 在 `auto` 模式下既接受六位码，也接受会话 ID。准备成功后的 `ConnectionPreparation.Target` 是
`InvitationSessionNetworkTarget`，可供调用方保存恢复信息。会话定位信息不授予游戏权限，正式游戏握手仍负责授权。
这组探针验证逻辑定位与路径收发，不包含游戏模组下载和进程重启恢复。

显式 UDP 地址入口用于测试候选协商、错误端口和路径回退；它不自动读取配置。`scenario-session` 必须使用 `auto`。

## 两端独立运行

房主运行：

```bash
dotnet NetworkProbe/bin/Debug/net10.0/NetworkProbe.dll \
  host https://signal.example.test SIGNAL_IP:31990
```

等待 `READY joinCode=......`，在另一台设备运行客户端，将联机码作为最后一个参数：

```bash
dotnet NetworkProbe/bin/Debug/net10.0/NetworkProbe.dll \
  client https://signal.example.test SIGNAL_IP:31990 012345
```

地址与端口需替换为实际部署；控制 URL 可使用域名，UDP 地址当前要求 IP 端点。公网控制入口应使用 HTTPS。联机码仅用于定位会话，日志不输出房主凭据或短期票据。

所有入口支持 `--bind-ipv4 ADDRESS` 和 `--bind-ipv6 ADDRESS`，分别指定两个地址族的本地绑定，默认值为 `0.0.0.0` 与 `::`。
这两个选项并不关闭另一个地址族。`host`、`client` 和 `client-recovery` 还支持 `--local-port PORT`（0–65535，默认 0 自动分配）。
`scenario*` 将绑定地址传给两个子进程，端口分别自动分配；固定端口实验应独立启动两端。
绑定地址必须是对应地址族的本机 IP，选项不能重复。公网映射端口仍以协商结果为准。

加上 `--output DIRECTORY` 可保存本轮结构化证据，目录必须尚不存在：

- `manifest.json`：模式、时间、进程、本地绑定和探针/Engine 程序集 SHA-256。
- `events.jsonl`：每行一个阶段，包含 UTC、经过时间及阶段数据；所选路径在 `path-confirmed` 和 `connected` 中记录。
- `samples.jsonl`：两端每 250 毫秒采集应用载荷字节、连接路径/身份、RTT、丢包比例、可靠队列、宿主资源、进程 CPU 时间和内存；关闭后另外保存最终样本。字节是应用接受发送和接收的载荷，不包含信令、握手、线路头部或中继封装。
- `summary.json`：完成或失败、最后阶段、耗时；信令失败包含操作、类别、错误码及 HTTP 状态。

场景模式另外在 `host/`、`client/` 下保存两端各自的完整记录，父进程结果在所有子进程结束或回收后写入。
两端关闭时等待宿主后台任务结束，并校验资源计数归零；最终采样阶段为 `host-stopped`。失败结果仍保留失败前的业务阶段，不被关闭事件覆盖。父场景没有独立的游戏 socket，其采样文件为空。
仅完成邀请注册或路径挑战不表示联机成功；客户端还需完成无连接探测、可靠回显与结束确认。
输出不包含消息载荷、票据或房主凭据；标准输出和可选的诊断跟踪仍可由调用方重定向保存。

当前每次运行只验证一位客户端，房主收到客户端在可靠回显后发送的完成消息即退出并关闭邀请。房主邀请由Engine持续续约；普通运行预算为 45 秒；独立 host 和 client 可通过 0–90 秒准备等待参数延长本轮预算。双方须使用相同构建。跨网测试应保存两端日志、信令日志、拓扑及构建信息；默认策略Allowed在直接候选超时后申请中继；Disabled不使用中继，Required直接申请中继。

注册和路径探测使用正式传输 socket，握手前不重新绑定。`PATH challengeConfirmed` 表示候选挑战完成；最终 PASS 才表示可靠数据往返成功。真实公网可达性仍须在不同出口及经过校准的 NAT 拓扑中验证。

本机 IPv6 控制与 UDP 的候选/中继场景可配置 `--urls http://[::1]:31991`、
`--Rendezvous:UdpAdvertisedHost ::1`、`--Rendezvous:RelayAdvertisedAddress ::1`，
探针使用 `http://[::1]:31991 auto`。IPv6 loopback 握手、回显与关闭验证不代表公网 IPv6 已完成验收。

## 隔离双 NAT 实验室

脚本在新的 network namespace 内创建模拟公网、两台独立 NAT 路由器和两个不同 LAN。
普通场景的 UDP 使用显式固定端口 SNAT，入站过滤采用 conntrack 来源元组；先校准不同目标观察到的映射一致，
并确认未联系的来源不能入站，再运行正式 Engine socket 的协商和可靠回显。默认 MASQUERADE 的行为
可能因内核不同而变化，不能直接当作端点无关映射。

需要 Python 3、`unshare`、`nsenter`、`ip`、`nft`、`sysctl`、`openssl`、`tcpdump`，以及内核 veth/netfilter 支持。
已编译的 NetworkProbe、RendezvousServer 可在安装 .NET 的主机直接运行；也可显式指定已缓存的 Podman
运行时镜像。脚本不拉取镜像。root 启动新的 network namespace，普通用户另外使用 user namespace；
脚本检查命名空间与调用者不同，拒绝直接更改宿主网络。容器模式应在 root 的专用测试环境使用。

```bash
bash NetworkProbe/scripts/nat-lab.sh --case punch \
  --output /tmp/scnet-network/nat-punch
bash NetworkProbe/scripts/nat-lab.sh --case peer-blocked \
  --output /tmp/scnet-network/nat-relay
bash NetworkProbe/scripts/nat-lab.sh --case udp-blocked \
  --output /tmp/scnet-network/nat-blocked
bash NetworkProbe/scripts/nat-lab.sh --case endpoint-dependent \
  --output /tmp/scnet-network/nat-dependent
bash NetworkProbe/scripts/nat-lab.sh --case double-nat \
  --output /tmp/scnet-network/nat-double
bash NetworkProbe/scripts/nat-lab.sh --case hairpin \
  --output /tmp/scnet-network/nat-hairpin
```

`punch` 要求 HolePunch 与可靠回显；`peer-blocked` 阻断对端直达、保留信令和中继，要求 Relay 与回显；
`udp-blocked` 阻断客户端全部 UDP，要求总体准备期限内返回 Timeout。每次在新拓扑中运行一个场景，输出目录不得已存在。
`endpoint-dependent` 使用受控目标相关映射：信令目标映射到 50000，其他目标映射到 50001；先校准同一 socket 的不同目标映射不一致及未联系来源被阻挡，再要求回退 Relay 并完成可靠回显。
`double-nat` 为每端增加独立内层路由器，UDP 先映射到内层 45000、再映射到外层 50000。校准最终映射与过滤，并保存内外层 UDP 包头和路由规则，要求 HolePunch 与可靠回显。
`hairpin` 将两个 LAN 接到同一 NAT，使用固定本地端口 41000 和公网映射 50000/50001，通过显式 DNAT/SNAT 模拟回环。直接跨 LAN 通信被过滤，只允许经公网映射返回；保存两个 LAN 的包头，要求 HolePunch 与可靠回显。
`hairpin-blocked` 使用相同拓扑但拒绝公网映射回环，默认要求 Relay 与回显。
`hairpin-local` 同样拒绝公网回环，但允许跨 LAN 路由；禁用中继时仍要求 HolePunch 与回显，用于验证私网候选。该拓扑保留源 NAT，包头可同时出现私网与转换后的源地址。
`--relay-policy Disabled|Allowed|Required` 选择路径策略，默认 Allowed。Disabled 在对端被阻断或目标相关映射场景要求 HolePunchUnavailable；Required 在 UDP 可用时要求 Relay。
`beacon-blocked` 只阻断客户端 UDP 信令端口，保留控制 HTTP 和中继：Allowed/Required 要求 Relay，Disabled 要求 UdpUnavailable。
`relay-blocked` 阻断两端到中继池的 UDP：Allowed/Disabled 要求 HolePunch，Required 要求 RelayUnavailable。
`address-filter` 使用有期限的已联系地址集合，允许该地址从其他端口发回报文，但拒绝未联系地址；显式固定映射 50000 到本地 41000。普通 `punch` 的 conntrack 配置则按地址和端口过滤。独立校准探针验证两者对“同地址换端口”的不同结果。
`public-endpoints` 把两端直接接入模拟公网，校准无 NAT、无入站过滤及本地端口与观察端口一致，再完成邀请码候选路径与可靠回显。这是隔离拓扑中的无 NAT 对照，不是实际公网可达性证明。
每端同时保存阶段与周期采样，成功或按预期失败后均检查最终宿主停止、资源计数归零。失败场景通过终止信号取消仍在等待的房主；POSIX 探针处理 SIGTERM，Ctrl+C 也走取消和关闭流程。

远端机器没有 .NET 时，将普通 Debug 构建目录复制过去，然后使用：

```bash
bash nat-lab.sh --case punch --probe-dir /path/to/probe --server-dir /path/to/signal \
  --runtime-image mcr.microsoft.com/dotnet/sdk:10.0 \
  --output /path/to/new-results
```

TLS 证书在实验中临时生成，只通过子进程环境或只读容器挂载信任，不修改系统信任库。
私钥保存在输出目录之外并在结束时删除；输出仅保留公开证书、路由器规则、校准结果、
UDP 包头（无 payload）、进程日志、构建哈希及 `summary.json`。失败也清理本次创建的容器与命名空间进程。

已验证的矩阵范围是明确配置的 IPv4 固定映射/过滤、双层固定映射、受控目标相关映射、受控回环、直达阻断和 UDP 阻断；
这不能代表所有 NAT 行为、IPv6、真实运营商或完整游戏体验。

## 当前信令保护参数

控制入口默认每个来源 IP 每分钟允许 600 次请求，由 `Rendezvous:HttpRequestsPerMinute` 配置，超限返回 HTTP 429，不排队。只接受 `Rendezvous:TrustedProxies` 配置的可信代理提供的一层转发头；共享出口的用户共享请求预算。具体容量仍需按部署负载验证。

加入操作要求非空随机 `RequestId`：调用方在一次加入尝试及其 HTTP 重试中保持相同 ID。重复请求返回原参与者；参与者过期后同 ID 返回 `join_request_expired`，新尝试使用新 ID。每个邀请保留最多 1024 条加入请求记录，直至邀请关闭或过期。

UDP 控制入口每 IP 每秒最多接受 120 个报文、总计 4096 个，待处理报文最多 256 个；超限直接丢弃，由有预算的协商重试处理。周期性失效清理每秒推进一次，具体操作仍检查自身期限。该限制不适用于已经建立的游戏数据连接。

探针客户端使用 `PrepareInvitationAsync` 返回的 `ConnectionPreparation`，不再自行发送加入、注册或交换控制消息。准备路径先交换模拟 ServerInfo 的应用数据报，再由准备对象执行正式握手。应用数据报当前上限 1024 字节，并保留 `SCRV` 前缀用于 Engine 控制分流；探针交换的是小型应用回显消息；游戏的完整 ServerInfo 预检由 `ServerConnectionPreparation` 负责。

准备对象只能执行一次连接操作，释放会取消其未完成握手；成功连接由宿主持有，释放准备对象不关闭连接或共享宿主。邀请候选验证后每两秒发送一次已认证挑战以保持路径，准备对象期限为两分钟；超期拒绝应用数据报和握手。正式连接时或释放准备对象时回收对应保活状态。等待期间通过 `ExtendLeaseAsync` 周期续约，规则见后文“准备租约”。

房主通过 `RegisterRoomAsync` 获取 `NetworkRoomRegistration`，Engine 每 20 秒续约会话并重新完成 UDP 端点注册。探针在输出 READY 前也显式执行一次续约。邀请的 `Failure`、`Maintenance` 和宿主的 `RoomRegistrationFailed` 通知可用于观察后台失败；通知仍由 `DispatchEvents` 派发。关闭邀请取消维护任务并撤销会话，宿主关闭时也回收其邀请；邀请本身不拥有游戏监听或已建立连接。

Engine 通过失败通知报告邀请维护终止；Game 保留现有监听和连接，并按 4、8、16、30 秒退避重新注册。游戏模组准备期间每 20 秒续约路径，重启只保存逻辑目标，并重新获取路径和 ServerInfo。探针不包含这套 Game 模组业务。

客户端可追加 0–90 秒的准备延迟，用于验证握手前等待，例如 `client CONTROL_URL BEACON_IP:PORT JOIN_CODE 8`。探针在等待后检查准备状态仍存在，握手后检查状态已清理，并执行可靠回显。该本机检查不证明实际NAT映射被维持。

## 中继传输验证

中继目前有可独立实例化的 `UdpRelayRoute` 和 Engine 内部包层，已提供 HTTP 分配接口，已接入 Engine 房主介绍及客户端策略选择。运行针对测试：

```bash
dotnet test RendezvousServer.Test/RendezvousServer.Test.csproj
```

测试覆盖两条独立 UDP 中继路径与一个直连客户端共享宿主、可靠4 KB载荷的分片往返和隔离、关闭一条路径不影响另一条，以及真实UDP的错误密钥与重放拒绝。每条中继使用独立端口，双方使用不同密钥，服务器验证后为目的端重新认证报文；密钥不在UDP头中传输。首次认证来源固定，地址变化需要重新协商。

包层为中继预留 46 字节开销；当前 LiteNetLib 按宿主统一预留，直连的可用 MTU 也会保守减少。端口池、空闲回收、准备续约和认证释放由对应测试覆盖；实际网络可达性须结合独立进程、校准拓扑及不同出口的测试证据判断。

信令服务现提供 `/relay/allocate`、`/relay/host-offers`（最长20秒等待）和 `/relay/host-ready` 控制接口。分配校验参与者凭据；房主介绍和确认校验房主凭据。介绍经控制通道传递私有中继密钥，公网部署须使用HTTPS。默认实验端口池为32000–32127，公开地址默认127.0.0.1；部署必须设置 `Rendezvous:RelayAdvertisedAddress`，并配置/开放 `RelayFirstPort` 与 `RelayPortCount` 对应UDP范围。Engine通过这些接口消费房主介绍并准备中继路径。

后台每五秒扫描中继，认证流量超过配置的 `RelayLifetimeSeconds` 未活动则回收 socket（默认一分钟）；已有中继路径的活动期限独立于邀请关闭。端点介绍需在分配返回的到期时间内完成。`/relay/renew-preparation` 用于续约尚未连接的路由；取消、连接断开及宿主关闭发送认证 `relay-close` 数据报。释放通知丢失时仍依赖空闲期限回收。

独立两端强制中继验证：

```bash
dotnet NetworkProbe/bin/Debug/net10.0/NetworkProbe.dll \
  scenario-relay http://127.0.0.1:31991 127.0.0.1:31990
```

手动客户端最后两个可选参数为准备延迟秒数和中继策略，例如 `client CONTROL_URL BEACON_IP:PORT JOIN_CODE 0 Required`。房主在后台长轮询中继介绍，安装认证路径后确认接收。中继准备也从原游戏socket发起认证数据报探测，成功后才执行握手；释放未连接的准备对象移除本地路径，成功转移则保留包层直到连接断开。中继初始准备期限取服务器配置（默认一分钟），准备对象在持有期间周期续约；消费后由连接持有路由直到断开。

Engine连接的 `SendWithReceipt` 返回带唯一ID的 `DeliveryReceipt`；`Completion` 只表示可靠传输的确认，不表示游戏已解码或处理。可靠有序及可靠无序可使用该入口，不可靠/Sequenced不支持。每个连接最多持有1024条待确认凭据；达到上限明确拒绝继续创建，连接断开或宿主关闭时未确认凭据以IOException结束。所有可靠发送现在都计入确认预算。

接收通知使用 `NetworkMessage`，包含复制后的载荷和Engine投递方式。当前LiteNetLib回调不提供通道号，Engine未宣称可以读取该元数据。断线通知使用 `NetworkDisconnectInfo`，包含Engine原因分类、最多256字节原因数据及socket错误码；握手失败以带该信息的 `NetworkConnectionException` 结束。`Reject` 和 `Disconnect` 接收原因字节，调用返回后可复用原数组。

入站握手最多4096字节，同时待决请求最多128个。请求有唯一ID与到期时间，默认5秒，可在创建宿主时配置为不超过30秒的正期限。后台推进超时拒绝；已过期的排队通知不会再调用应用，应用持有的请求也不能在期限后接受。Accept成功时返回宿主拥有的稳定NetworkConnection，过期或重复接受返回null；Reject返回是否成功决定请求，重复拒绝返回false。通知队列已按NetworkHostOptions限制容量，可靠发送预算已接入。

`NetworkHostOptions`集中配置请求期限和通知预算，默认4096条数据通知、8 MiB排队载荷及1024条生命周期通知的独立预留容量。队列保持原通知顺序。数据预算不足时无连接/不可靠数据允许丢弃；可靠接收明确关闭对应连接并传递 `receive-overload`，不能继续当作可靠接收成功。生命周期预留也耗尽时，宿主Failure及Completion报告错误并清理后台与队列。宿主关闭后不派发遗留通知。

可靠发送默认最多1024条未确认消息和4 MiB载荷，由NetworkHostOptions.MaxPendingReliableBytes配置字节预算。TrySend返回Accepted/Closed/TooLarge/Backpressure；Send拒绝时抛出带分类的NetworkSendException。所有可靠发送都由内部投递确认释放容量，无须业务请求receipt；无公开receipt的发送不创建用户可等待的Task。PendingReliableBytes可用于发送预算诊断。

请求接受后立即取得的连接与后续Connected通知引用同一对象，应用可在接受时按Connection.Id建立身份映射。TransportEndpoint仅表示实际传输端点（中继时为中继地址），不能作为玩家身份或真实来源IP；返回端点副本，修改它不会改变传输内部地址。

宿主推进、邀请维护与中继转发的后台循环在线程池启动，不依赖创建对象时的UI同步上下文；业务通知仍由调用方显式派发。


准备对象关闭时通过 `/invitations/leave` 释放申请名额与注册票据，重复释放幂等；已关闭的会话视为已释放。
已建立连接和中继转发由 transport 持有，不随准备对象释放而断开。加入请求的墓碑仍有数量上限，
已释放身份的旧 request ID 不能重新获取凭据或恢复名额。准备失败也尝试在独立的三秒预算内释放；
控制服务不可达时由参与注册自身的期限回收。成功准备后的调用方必须关闭准备对象，并让 HTTP 客户端活到关闭完成。


## 准备租约

`ConnectionPreparation.ExtendLeaseAsync` 只续约尚未消费且未过期的路径，单次请求最多两分钟；
中继以服务器返回的实际到期时间为准（默认一分钟）。调用方应在到期之前周期维护，过期后必须重新准备逻辑目标。
打洞保活的可达性期限和持有者的准备期限分开，未续约的准备不会因互相保活而永久占用探测状态。
中继续约保持 route ID、端点和密钥，并重新向房主投递同一条路径的新期限；旧期限的迟到确认不会确认新租约。
参与者续约不复活已经过期或释放的身份。

```bash
dotnet NetworkProbe/bin/Debug/net10.0/NetworkProbe.dll scenario-lease http://127.0.0.1:31991 auto
```

该场景强制中继，在正式握手前等待 70 秒，每 20 秒续约，随后验证同 socket 预检与可靠回显。
总预算 120 秒；它覆盖默认的一分钟中继准备期限。独立 client 的准备延迟可取 0–90 秒，运行预算为
45 秒加延迟；独立 host 可提供第四个参数作为 0–90 秒的额外等待预算。普通场景继续使用默认 45 秒。
这不是跨网、模组进程重启或连接后弱网验收。

## 可靠流恢复检查

将 `client` 替换为 `client-recovery`，其余参数相同。握手和首个回显成功后，
发送80条1024字节可靠消息，间隔100毫秒；验证全部消息按序回显、内容一致、传输确认完成，
且发送队列归零。STREAM记录UTC开始时间，便于与损伤工具断流窗口对照。
工具自身不注入损伤；必须保留代理的实际转发/丢弃记录，不能仅凭PASS声称弱网恢复通过。

设置 `SCNET_PROBE_TRACE=1` 可输出 Engine EventSource 的中继接收诊断，包括来源端点、
路由标识和认证/重放/未分配端点结果，不输出凭据或报文内容。默认关闭；开启后日志可能较多。

### 双栈候选与单族信标故障

`local-lab.py --family dual` 以 `localhost` 公告 UDP 信标，房主和加入端解析每种地址族的一个端点。
加入端共享一次加入登记和票据，并行验证候选；选定成功路径后取消并等待其他尝试清理。
只有全部直连候选失败后，Allowed 策略才申请中继；Required 策略直接使用中继。

在隔离 Linux namespace 中，`local-lab.sh --family dual --blocked-beacon-family ipv4`（或 `ipv6`）
仅丢弃指定地址族发往信标端口的 UDP，保留 HTTP 和另一地址族。脚本保存过滤规则并校准丢弃计数。
该选项要求隔离双栈实验，不能修改宿主网络，也不能用于推断公网 IPv6 可达性。

`--beacon-block-hook output` 可将同一丢弃规则放在输出链，以验证本地发送拒绝；默认 input 用于静默丢包。驱动器保留就绪前日志并等待明确的 READY 标记，不把第一行日志当作就绪信号。

`scenario-candidate-lease` 与 `scenario-lease` 都在路径确认后等待 70 秒并维护准备租约；
前者使用 Allowed 策略选择直连候选，后者强制中继。
`local-lab.py --family dual --mode scenario-candidate-lease --cancel-after-path`
可在双栈候选确认后取消父探针，并验证两端正常清理；使用原生运行时，无需等待完整租约时长。

### 链路 MTU 与分片恢复

`nat-lab.sh --link-mtu 576 --recovery` 在隔离 IPv4 拓扑的所有非 loopback 接口设置指定 MTU，
保存实际接口快照，并发送 80 条有序的 1024 字节可靠数据，要求回显、回执及队列最终收敛。
默认 MTU 为 1500，允许范围 576–9000；对照应使用相同构建和策略。
加入 `--relay-policy Required` 可验证带认证封装的中继路径。
这属于传输层验收，不能代替游戏地形收敛测试；链路 MTU 也不等同于应用载荷上限。

### 固定种子的突发丢包

`nat-lab.sh --case punch --recovery --burst-loss --seed 20261012` 在两台实验路由器的 WAN 出口
配置 `tc netem loss gemodel 2% 25% 100% 0%`，使用同一指定种子，保留配置及丢包统计。
该模型允许连续丢包；它不保证不同调度下逐包损伤完全相同。脚本要求两侧实际丢包计数大于零。
使用相同构建、MTU、策略和恢复负载运行无损对照；中继对照加 `--relay-policy Required`。
环境需要 `tc` 和内核 `sch_netem` 支持；模块未加载时由实验环境管理员配置，脚本不修改宿主模块。
规则仅在隔离 namespace 内创建，不改变宿主默认路由或 qdisc。

连接采样与游戏网络诊断同时记录 `Path` 和 `AddressFamily`（`InterNetwork` / `InterNetworkV6`）。直连或打洞状态本身不能证明使用了 IPv6；公网测试应同时检查地址族与实际游戏就绪状态。
