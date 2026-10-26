# UDP 安全传输边界

本文说明当前 `Engine.Network` 的保护范围及其与上层协议的交界，不定义新的 TLS 变体。
可用 API、宿主事件和容量选项见 [Engine.Network](../../Engine.Network/README.md)。

## 连接与公开数据报

连接流量使用 Bouncy Castle 的 DTLS 1.2，当前固定套件为
`TLS_ECDHE_ECDSA_WITH_CHACHA20_POLY1305_SHA256`。握手、密钥派生、记录加密和重放保护由该实现负责。
LiteNetLib 的连接控制、应用握手、ACK、游戏载荷和断线控制包均处于保护范围；不提供明文连接回退。

局域网发现、服务器信息探测以及独立信令数据报不建立游戏 DTLS 会话，不能因此信任其来源或内容。
公开目录、信令控制面和认证 HTTP 的保护规则由各自协议决定。

当前 Engine 封装通过 LiteNetLib 的 unconnected 属性字节 `0x08`、ASCII `SCDT` 与后续 DTLS 记录识别
安全数据报；`SCDT` 后没有额外的 SCNET 版本字节。它不是 `Game.Protocol` 的消息帧，也不是中继头。
封装代码见 [SecurePacketLayer](../../Engine.Network/Security/SecurePacketLayer.cs)。

## 持钥证明与目标信任

每个网络宿主使用 P-256 身份，调用方可以提供实例密钥；不提供时引擎生成临时身份。
宿主持有自己的密钥副本，关闭时销毁副本。游戏实例的持久化方式属于[启动会话](../StartupSessions.md#实例身份)。

对端身份来自 DTLS 握手中的公钥持有证明。身份地址是 RFC 7638 SHA-256 JWK 指纹的
64 字符 `scnet` Bech32m 编码；SPKI 公钥和完整地址用于比较，界面省略值不能用于验签或授权。

持钥证明不能自行证明“这就是玩家原本想连接的服务器”。调用方可通过 `ValidatePeerIdentity`
固定预期服务器身份；默认未固定的首次连接不等于预先建立了可信来源。
认证服务公钥的信任也不能由 DTLS 或目录收录自动推导。

## 连接绑定

握手完成后双方使用 exporter 标签 `EXPORTER-SCNET-transport-v1`、空上下文，导出 32 字节
`ChannelBinding`。同一连接双方值相同，新握手应使用新值。

外部认证挑战必须取自本次实际连接的主体、服务器身份和该绑定，不能使用 IP、公开连接编号或客户端自填字段替代。
游戏准入批准由[认证协议](AuthenticationProtocol.md)绑定到该值；重连必须获得新批准。

## 直连、打洞和中继

三种路径承载相同端到端安全连接。中继只包裹已加密的游戏连接载荷，不能因使用中继而省略 DTLS。
中继的 HMAC 用于路由访问控制与重放防护，本身不加密业务载荷，见[信令与中继](RendezvousProtocol.md)。

DTLS、Engine 前缀和中继头都会占用数据报预算。调用方必须读取 `GetMaxPayloadSize`，
不能把某一平台、直连或测试配置下的大小硬编码到所有路径。
超过不可靠预算时会返回 `TooLarge`；可靠消息的分片与发送策略仍由传输实现管理。

## 实现边界与验证

`Engine.Network` 不访问认证服务、不管理玩家账户、角色或存档权限，也不把通信密钥当作游戏业务授权。
其业务事件由调用方调度，不能将网络工作线程回调直接作为修改游戏世界的入口。

协议使用库实现的 DTLS 1.2，不宣称 DTLS 1.3；接入方还须匹配当前的 LiteNetLib 外层与 SCNET 封装。
跨语言实现不能仅凭实现了同名 TLS 套件就假定可互通。
安全连接、损坏报文和重放等验证见 [SecureNetworkTest](../../Engine.Network.Test/Security/SecureNetworkTest.cs)，
可运行路径探针见 [NetworkProbe](../../NetworkProbe/README.md)。
