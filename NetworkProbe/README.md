# 网络探针

该工具使用 Engine.Network 的实际 LiteNetLib socket，验证信令注册、候选挑战、正式握手和可靠回显。无 Game、Window 或世界数据依赖。该工具支持邀请直连及中继准备；游戏本体已有独立接入路径，探针不代替游戏验收。

发布 ZIP 包含探针程序集和本说明。探针使用 .NET 10，可在 Windows、Linux 上运行；
使用发行包时将下文的构建目录替换为解压目录。

Agent 的实验选择、进程管理和结果判定由仓库技能
[scnet-network-probe](../.agents/skills/scnet-network-probe/SKILL.md) 维护。
技能按宿主使用 Bash 或 PowerShell 操作 .NET 工具，不随探针发布实验脚本。

## 本机验证

### 测试与探针入口

使用 .NET 10 SDK 运行网络相关测试：

```bash
dotnet test Rendezvous.Protocol.Test/Rendezvous.Protocol.Test.csproj
dotnet test Engine.Network.Test/Engine.Network.Test.csproj
dotnet test RendezvousServer.Test/RendezvousServer.Test.csproj
```

以下 `dotnet` 命令可用于 Windows 和 Linux；需要 Linux 内核网络设施的专项实验另见技能。

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

### Agent 维护

Agent 的本机与隔离网络实验由仓库的
[scnet-network-probe 技能](../.agents/skills/scnet-network-probe/SKILL.md) 维护，建议在 Linux 环境执行。
这些维护脚本不包含在面向用户的探针发行包中。

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

## 校准后的 NAT 实验

[探针技能](../.agents/skills/scnet-network-probe/SKILL.md)
定义隔离 WAN、独立或共享路由器、双层 NAT、映射/过滤校准、故障注入及验收矩阵。
每轮先用独立 UDP 报文验证实际映射与入站过滤，再运行 Engine 探针，保留规则、
UDP 包头、进程日志、构建哈希和结构化结果。不能仅凭连接成功推断 NAT 类型。

实验编排由 Agent 根据假设选取，工具本身不更改路由或防火墙。历史固定映射、
目标相关映射、受控回环等实验结果不代表所有 NAT、真实运营商或游戏体验。

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

### 双栈、MTU 与损伤实验

双栈控制地址与 UDP 广告名使用 `localhost` 时，房主和加入端解析每种地址族的一个端点。
加入端共享一次加入登记和票据，并行验证候选；选定路径后取消并等待其他尝试清理。
Required 策略直接申请中继；Allowed 在全部直连候选失败后申请中继。

单族信标阻断、链路 MTU、固定种子的突发丢包、路径确认后取消的操作及独立校准见
[探针技能](../.agents/skills/scnet-network-probe/SKILL.md)。这些操作不是探针的命令行选项。
`scenario-candidate-lease` 与 `scenario-lease` 均在确认路径后等待 70 秒并续约；前者验证候选，
后者强制中继。`client-recovery` 的可靠负载可用于 MTU/丢包对照；探针自身不注入损伤。

连接采样与游戏网络诊断同时记录 `Path` 和 `AddressFamily`（`InterNetwork` / `InterNetworkV6`）。直连或打洞状态本身不能证明使用了 IPv6；公网测试应同时检查地址族与实际游戏就绪状态。
