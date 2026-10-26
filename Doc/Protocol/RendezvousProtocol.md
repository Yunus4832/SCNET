# 信令、临时房间与认证中继协议

`SCNET.Rendezvous.Protocol` 定义独立于游戏内容的邀请、地址交换和中继数据契约。
参考服务为 RendezvousServer，传输客户端由 Engine.Network 提供。
部署端口、证书及生命周期配置见[信令服务部署](../RendezvousDeployment.md)。

这里的“认证”指票据或路由能力的持有校验，不是[玩家认证](AuthenticationProtocol.md)。
获取邀请、进入房间或连上中继均不绕过游戏服务器的准入和存档检查。

## HTTP 控制面与房间发现

参考实现使用 ASP.NET HTTP JSON 默认 camelCase 名称。公网控制入口应通过可信 HTTPS 提供，
操作凭据位于请求正文，不放入 URL 或公开房间列表。控制请求最多 16 KiB；响应上限为 1 MiB。

| 方法与路径 | 请求字段 | 响应/作用 |
|---|---|---|
| GET `/transport` | 无 | `udpPort`、可选 `udpHost`、可选 `roomDirectoryPath` |
| GET `/rooms` | 可选 `limit`、`cursor` | `rooms`、`nextCursor` |
| POST `/invitations/create` | `maxParticipants`，参考入口默认 8 | SessionRegistration |
| POST `/invitations/join` | `joinCode`、`requestId` | ParticipantRegistration |
| POST `/invitations/join-session` | `sessionId`、`requestId` | ParticipantRegistration |
| POST `/invitations/update-room` | `sessionId`、`credential`、`publication` | 更新公开房间资料，成功 204 |
| POST `/invitations/renew`、`/invitations/close` | `sessionId`、`credential` | 主机续期结果或关闭成功 204 |
| POST `/invitations/registration-ticket` | `sessionId`、`participantId`、`credential` | RegistrationTicket |
| POST `/invitations/renew-participant`、`/invitations/leave` | 同上 | 参与者续期结果或离开成功 204 |
| POST `/relay/allocate`、`/relay/renew-preparation` | `sessionId`、`participantId`、`attemptId`、`credential` | RelayAllocation |
| POST `/relay/host-offers` | `sessionId`、`credential` | RelayAllocation 数组，可长轮询等待 |
| POST `/relay/host-ready` | `sessionId`、`participantId`、`attemptId`、`credential`、`expiresAt` | 确认该轮准备，成功 204 |

GUID 在 JSON 中使用字符串；时间为 DateTimeOffset 的 ISO 8601 字符串，与认证 JWT 的 Unix 秒字段不同。

参考 `/health` 只用于部署健康检查，不作为游戏服务器在线或玩家通过准入的证明。
HTTP 错误可能是空的 401，或带 `error` 的 400/404/409/429 等结果；不能假定每个错误都有同一成功结构。

| 类型 | 字段 |
|---|---|
| SessionRegistration | `sessionId`、`joinCode`、`hostCredential`、`expiresAt` |
| ParticipantRegistration | `sessionId`、`participantId`、`credential`、`expiresAt` |
| RegistrationTicket | `token`、`expiresAt` |
| RelayAllocation | `sessionId`、`participantId`、`attemptId`、`endpoint`、`credentials`、`expiresAt` |
| RelayRouteCredentials | `routeId`、`side`、`key`（32 字节，HTTP JSON 为 base64） |
| RoomPublication | `name`、`description`、`isPublic` |
| RoomSummary | `id`、`name`、`description`、`joinCode`、`connectionMethods` |

房间名称上限 100 字符，说明上限 1024；分页最多 100 条、客户端最多 100 页，游标最多 512 字符。
房间列表不返回主机凭据、参与者凭据或路由密钥；它是临时发现，不属于[持久服务器源](ServerSourceProtocol.md)。

## UDP 地址注册与交换

`SignalCodec` 当前版本为 `1`，数据报最大 1200 字节。布局为：

```text
ASCII "SCRV" | byte version=1 | UTF-8 JSON SignalMessage
```

UDP codec 使用 `JsonSerializer` 的默认名称，字段为 PascalCase，**不同于 HTTP 的 camelCase**：
`Operation`、`RequestId`、`SessionId`、`ParticipantId`、`Ticket`、`Endpoint`、`ExpiresAt`、`Error`、`Challenge`、`LocalEndpoints`。
请求 ID 必须非空，Operation 最多 32 字符，Ticket 与 Challenge 最多 64，Endpoint 最多 128。
本地候选端点还须通过 [LocalCandidateEndpoints](../../Rendezvous.Protocol/LocalCandidateEndpoints.cs) 的地址集合检查。

主要往返：

1. 通过 HTTPS 获取参与凭据和短期注册票据。
2. `register` 携带票据，从实际 UDP socket 发送；服务端返回 `challenge`。
3. 同一端点使用 `confirm` 回应挑战，成功返回 `registered` 与观察到的端点。
4. 参与者通过 `exchange` 请求主机候选；服务端向主机发送 `introduce`，向参与者返回 `candidate`。
5. 双方通过候选地址准备直连/打洞路径；无法直连时按控制面分配中继。

主机参与者使用空 GUID，普通参与者使用分配的非空 GUID。票据、端点和交换挑战必须属于同一邀请及准备轮次；
失败的信令响应为 `Operation="error"`。具体的确认和候选行为见 [UdpSignalHost](../../RendezvousServer/UdpSignalHost.cs)。

信令 UDP JSON 本身不加密；不得把它视为保密信道，也不能在其中传游戏服务器密码、外部 grant 或玩家私钥。
其端点注册和挑战只负责传输准备，游戏连接仍须执行 DTLS 和游戏准入。

## 中继数据面

中继封装版本为 `1`，使用 HMAC-SHA256 的前 16 字节，不加密内层载荷。
`RelayDatagramCodec` 的布局为：

| 偏移 | 大小 | 字段 |
|---:|---:|---|
| 0 | 4 | ASCII `SCRL` |
| 4 | 1 | version=1 |
| 5 | 16 | RouteId，GUID big-endian 字节序 |
| 21 | 1 | Side，0 或 1 |
| 22 | 8 | Sequence，非零 UInt64 big-endian |
| 30 | 可变 | 非空 payload |
| 末尾 | 16 | HMAC-SHA256 截断标签，覆盖此前所有字节 |

固定开销 46 字节，内层最大 1426 字节。每条路由/方向使用其分配的 32 字节 key；key 不进入数据报头。
接收者先核对路由和标签，再使用 64 序号滑动窗口拒绝重复和过旧数据报，允许窗口内乱序。
同一准备的 `AttemptId`、到期时间和主机确认有生命周期约束，不能用旧分配续接另一个准备轮次。

业务载荷的端到端机密性来自[DTLS 传输](TransportProtocol.md)。中继 HMAC 不能替代它；
公开 HTTP 房间列表、HTTPS 控制凭据、UDP 信令和加密游戏数据是四个不同边界。

## 源码与互操作

线格式以 [SignalCodec](../../Rendezvous.Protocol/SignalCodec.cs)、
[RelayDatagramCodec](../../Rendezvous.Protocol/RelayDatagramCodec.cs) 及对应模型为依据，
控制入口以 [RendezvousServer/Program.cs](../../RendezvousServer/Program.cs) 为依据。
NuGet SemVer 不等于两个 UDP codec 的版本 byte；未发布阶段不保留旧封装解析。

[NetworkProbe](../../NetworkProbe/README.md) 可检查控制面、准备租约和直连/中继路径；
测试通过不表示某玩家已经获得游戏业务权限。
