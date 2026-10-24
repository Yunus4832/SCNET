# SCNET.Engine.Network

提供传输宿主、连接生命周期、可靠投递确认，以及邀请打洞与认证中继路径。
业务通过 Engine 类型访问网络；LiteNetLib 由模块内部使用。
游戏协议、玩家权限、世界、模组配置和 UI 由调用方负责。

## 默认安全传输

连接流量在 LiteNetLib 数据报外使用 Bouncy Castle 2.7.0 DTLS 1.2，固定 ECDHE-ECDSA / ChaCha20-Poly1305，
覆盖应用握手、游戏载荷、ACK、连接和断线控制包。直连、打洞和中继使用同一端到端保护，公开发现和信令数据报仍可单独发送。
这不是 DTLS 1.3 实现，也不是仅在中继 HMAC 外换名称。DTLS 握手、密钥导出、记录序号和防重放由库负责。
各平台统一使用库提供的 ChaCha20-Poly1305，实现不跨 JNI 调用逐包加密原语。
选择依据包括 Android 小包传输实测；不自行实现握手、密钥派生或 AEAD。

`NetworkHostOptions.Identity` 可提供调用方管理的 P-256 身份，宿主创建自己的密钥副本，关闭后销毁副本。
`NetworkIdentity.Create/Import/ExportPrivateKey` 支持生成和受控持久化；导出的 PKCS#8 私钥必须安全保存并及时清除临时副本。
未提供身份时宿主使用临时密钥，重建宿主后地址可能变化；此默认值不代表游戏账号或存档身份。
`LocalIdentity` 和请求/连接的 `PeerIdentity` 暴露公钥及其 RFC 7638 SHA-256 指纹的 Bech32m 地址（`scnet` 前缀、64 字符）；对端在 DTLS 握手中证明持有私钥。
用户输入可经 `Engine.Core.Security.IdentityAddress.Normalize` 校验并规范化；固定公钥时比较完整地址，不使用界面简写。

**证明持钥与信任目标是不同的。** 默认不限制远端公钥，适用于开放实验或上层尚未选择身份的连接；
它不提供预期服务器的首次信任，也不能阻止主动中间人替换未知目标。
需要固定服务器或受信任集合时，配置 `ValidatePeerIdentity`，返回 false 会在应用握手发送前拒绝。
回调在握手工作线程同步执行，必须快速、线程安全，不阻塞 UI 或等待宿主退出。
Engine 不查询认证服务、不认领账号、不自动覆盖公钥；已验证公钥的业务信任和玩家准入由调用方负责。

请求与连接的 `ChannelBinding` 是本次 DTLS exporter 的 32 字节结果，双方一致且新握手不同，
可供后续准入证明绑定；不能用公开 `Id` 或 IP 代替。外部认证和主体/角色重构不在当前传输层实现中。

默认最多 32 个并发安全握手、512 个安全会话，可通过宿主选项配置。服务端先验证无状态 cookie 再分配握手工作者；
握手、应用等待、取消、拒绝和宿主关闭均有时间/容量边界。连接流量没有公开的明文回退开关。
内部 `SCDT` 前缀保留给 DTLS 封装，不是普通应用发现报文。

`BroadcastDatagram` 通过宿主原 socket 发送 IPv4 局域网广播，回复仍由该宿主接收；
广播不建立 DTLS 会话，接收端须启用 `ReceiveBroadcasts`。

DTLS 与封装预留 42 字节，加上中继预留 46 字节；原默认不可靠载荷上限 461 降到 419 字节。
实际可发送大小必须读取 `GetMaxPayloadSize`，不能把此示例常量当所有路径永远不变的保证。
超出不可靠预算返回 TooLarge，可靠包仍由 LiteNetLib 分片，但加密开销同样计入预算。

## 日志

应用在启动时通过 `NetworkLogging.Configure((level, message) => ...)` 接入自己的日志系统，等级由 `NetworkLogLevel` 表示。
LiteNetLib 日志适配、消息格式处理和网络不可达警告的 30 秒限流由模块内部负责；其他错误保留完整信息。
该出口是进程级设置，`NetworkHost` 的创建和销毁不会重新注册或清除日志出口。

## 宿主与事件

`NetworkHost` 创建实际 UDP socket，后台任务持续推进传输。
`NetworkHostOptions.LocalIPv4Address` 与 `LocalIPv6Address` 分别指定本地绑定，默认监听各自地址族的所有接口；指定地址不表示关闭另一地址族。
调用方在自己的业务循环中调用 `DispatchEvents()`，派发连接请求、连接、接收、断线及邀请失败通知。
业务事件不会由网络线程直接调用。数据与生命周期通知都有容量边界；宿主故障通过 `Failure` 和 `Completion` 观察。

```csharp
await using var host = new NetworkHost(port: 28887);
host.ConnectionRequested += request => request.Accept();
host.Received += (connection, message) => connection.Send(message.Payload.Span);
```

示例接受所有握手，仅用于回显实验；业务服务器应在接受前校验握手并作出授权决定。
调用方仍须运行事件派发循环，并在结束时等待宿主关闭。

`NetworkConnection.Id` 是连接身份，独立于传输地址。
`TransportEndpoint` 返回端点副本，中继时表示中继地址；玩家身份由业务协议确定。
`PathKind` 区分 Direct、HolePunch 和 Relay。
接收载荷已复制，回调后可以继续使用；断线信息包含原因分类、有限长度原因数据与 socket 错误码。

## 发送与投递确认

`DeliveryMode` 提供 ReliableOrdered、ReliableUnordered、Sequenced、Unreliable。
`TrySend` 返回 Accepted、Closed、TooLarge 或 Backpressure；`Send` 遇到拒绝时抛出 `NetworkSendException`。
调用方应处理背压，并处理检查状态与实际发送之间的断线竞态。

`SendWithReceipt` 仅用于可靠投递，返回带独立 ID 的 `DeliveryReceipt`。
其 `Completion` 表示传输确认，业务处理完成需要业务协议另行确认。
`PendingReliableBytes` 统计未确认载荷，连接或宿主关闭会结束未完成的确认。
通知与可靠发送预算通过 `NetworkHostOptions` 配置。

## 逻辑目标与准备对象

`AddressNetworkTarget` 定位地址，`InvitationCodeNetworkTarget` 定位六位码，
`InvitationSessionNetworkTarget` 定位不透明会话 ID。
邀请控制 URL 由调用方配置，公网使用 HTTPS，loopback 实验允许 HTTP。
中继策略为 Disabled、Allowed 或 Required；普通监听与邀请功能可同时运行。

`PrepareConnectionAsync` 使用宿主原 socket 完成端点注册、候选认证或中继分配，返回 `ConnectionPreparation`。
调用方可在这条路径上先交换应用数据报，再执行一次 `ConnectAsync`。
成功邀请准备的 `Target` 保留会话定位信息，适合重启后重新协商。
保存逻辑目标即可；临时票据、参与凭据、端点与中继密钥属于本次准备的生命周期。

调用方负责在等待期间周期调用 `ExtendLeaseAsync`，并在消费或取消后等待准备对象关闭。
关闭会取消未完成握手、释放注册及未转交路径；已转交连接由宿主继续持有。
准备期限到期须重新协商，续约不能复活已过期的准备。
HTTP 客户端须保持有效直到相关准备和邀请释放完成。

## 邀请、中继与诊断

`RegisterRoomAsync` 返回 `NetworkRoomRegistration`，后台维护房间租约与端点注册。
关闭注册停止新的信令加入，清理对应会话的端点、候选和未连接中继准备；已有连接按自身生命周期继续运行。普通监听不关闭，已知直连端点仍可使用。
维护失败由 `RoomRegistrationFailed` 通知调用方；Game 在业务层实现退避重新注册。

中继数据报绑定路由、双方身份及密钥，验证来源和重放序号，计入 MTU 开销。
地址变化后重新协商。关闭通知与空闲回收共同管理路由释放。
`NetworkHost.Resources` 提供近似资源计数，关闭后收敛到零；并发采样不构成原子快照。

信令失败统一使用 `RendezvousException`，携带操作、分类、错误码及 HTTP 状态；用户取消保留取消结果。
可选 EventSource `SCNET-Engine-Network` 输出中继来源与认证结果诊断，默认关闭，不记录凭据或报文内容。

仓库的 NetworkProbe、RendezvousServer 与对应测试提供回显、准备租约、隔离 NAT、弱网及实际游戏验证入口。
测试报告应明确拓扑和覆盖范围，loopback 或模拟 NAT 的通过不能证明任意公网可达性。

加入端的候选响应必须匹配尚未结束的信令请求、登记的参与者和信标端点；请求取消后清理对应探测，迟到候选不能重新创建路径。房主仍可接收其有效邀请的新参与者候选。

邀请交换信标观察端点，以及最多四个网卡候选（IPv4 RFC1918、IPv6 ULA 或公网单播地址，优先公网 IPv6）。网卡候选使用实际游戏端口，并绑定登记挑战；Engine 从原游戏套接字验证候选身份，选定首次成功的端点后仅维护该路径。局域网是否可达仍取决于路由与防火墙；不可达时按中继策略处理。

宿主配置 `ChannelCount` 默认 8，允许 1–64；对端应使用一致的通道配置。连接发送检查采用宿主配置的上限。`PreparationTimeout` 默认 15 秒，允许大于零且不超过两分钟，限制整个逻辑目标准备；内部阶段期限与路径租约仍分别约束操作，增加总预算不保证不可达路径能够成功。

信令登记端点的地址族不限制对端候选：IPv4 信令服务器可以交换双方声明的公网 IPv6 地址。声明不是可达性证明，必须经同一尝试与挑战验证后才能选择；两端仍需具备可达的 IPv6 地址和允许 UDP 的网络。
