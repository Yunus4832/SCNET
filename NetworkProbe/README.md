# 网络探针

该工具使用 Engine.Network 的实际 LiteNetLib socket，验证信令注册、候选挑战、正式握手和可靠回显。无 Game、Window 或世界数据依赖。该工具支持邀请直连及中继准备；游戏本体已有独立接入路径，探针不代替游戏验收。

发布 ZIP 包含探针程序集和本说明。探针使用 .NET 10，可在 Windows、Linux 上运行；
使用发行包时将下文的构建目录替换为解压目录。

Agent 的实验选择、进程管理和结果判定由仓库技能
[scnet-network-probe](https://gitee.com/Yunus0712/scnet/blob/main/.agents/skills/scnet-network-probe/SKILL.md) 维护。
探针本身支持 Windows/Linux；技能中的 Python/Bash 实验辅助脚本建议在 Linux 使用，
不随探针发行包分发。

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

[探针技能](https://gitee.com/Yunus0712/scnet/blob/main/.agents/skills/scnet-network-probe/SKILL.md)
定义隔离 WAN、独立或共享路由器、双层 NAT、映射/过滤校准、故障注入及验收矩阵。
每轮先用独立 UDP 报文验证实际映射与入站过滤，再运行 Engine 探针，保留规则、
UDP 包头、进程日志、构建哈希和结构化结果。不能仅凭连接成功推断 NAT 类型。

实验编排由 Agent 根据假设选取，工具本身不更改路由或防火墙。历史固定映射、
目标相关映射、受控回环等实验结果不代表所有 NAT、真实运营商或游戏体验。

## Engine 与信令边界

探针通过 `PrepareConnectionAsync`（`auto` 模式）或 `PrepareInvitationAsync`（显式 UDP
端点）取得 `ConnectionPreparation`，在原传输 socket 上完成应用数据报预检，再消费一次正式连接。
房主通过 `RegisterRoomAsync` 获取注册对象，Engine 维护租约，探针负责等待关闭及资源归零。

准备对象、投递确认、发送背压、通知队列和连接身份的 API 语义见
[Engine.Network](https://gitee.com/Yunus0712/scnet/blob/main/Engine.Network/README.md)。
HTTP/UDP 限流、中继端口池、期限与 HTTPS 配置见
[信令服务部署](https://gitee.com/Yunus0712/scnet/blob/main/Doc/RendezvousDeployment.md)。
这些限制应在所属实现和文档中维护，不在探针说明中复制另一份完整配置清单。

探针只验证传输准备与回显。Game 的模组补全、服务器信息校验、进程重启和房间重试属于游戏层，
不由探针通过来证明。成功准备后的调用方必须关闭准备对象，并让 HTTP 客户端保持有效直到关闭完成。
已转交连接由宿主持有，不随准备对象释放而断开。

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
[探针技能](https://gitee.com/Yunus0712/scnet/blob/main/.agents/skills/scnet-network-probe/SKILL.md)。这些操作不是探针的命令行选项。
`scenario-candidate-lease` 与 `scenario-lease` 均在确认路径后等待 70 秒并续约；前者验证候选，
后者强制中继。`client-recovery` 的可靠负载可用于 MTU/丢包对照；探针自身不注入损伤。

连接采样与游戏网络诊断同时记录 `Path` 和 `AddressFamily`（`InterNetwork` / `InterNetworkV6`）。直连或打洞状态本身不能证明使用了 IPv6；公网测试应同时检查地址族与实际游戏就绪状态。
