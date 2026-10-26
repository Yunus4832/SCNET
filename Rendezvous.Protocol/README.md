# SCNET.Rendezvous.Protocol

HTTP 入口、UDP JSON 和中继二进制布局见[信令与中继协议](../Doc/Protocol/RendezvousProtocol.md)。

邀请联机的信令与认证中继协议契约，面向 .NET 10，命名空间为 `Rendezvous.Protocol`。

- 会话注册、续约、参与方、房间发现和错误响应模型。
- `SignalCodec`：信令报文编码和解码。
- `RelayDatagramCodec`、`ReplayWindow`：认证中继报文及重放保护。
- 协议常量、候选地址和输入边界约束。

本包不启动服务器或 UDP socket，不依赖 LiteNetLib、游戏或引擎。
传输实现由 `SCNET.Engine.Network` 提供，信令服务由独立的 RendezvousServer 应用提供。
