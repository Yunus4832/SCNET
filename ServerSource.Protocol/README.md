# SCNET.ServerSource.Protocol

SCNET 服务器源 HTTP 协议的模型、校验器和客户端，面向 .NET 10。
程序集为 `SCNET.ServerSource.Protocol`，命名空间为 `ServerSource.Protocol`。

- 服务器源描述、分页、快照和服务器条目模型。
- `ServerSourceValidator` 和结构化校验结果。
- `ServerSourceProtocolClient`：使用调用方提供的 `HttpClient` 拉取和校验数据。
- 包内 `schemas/server-source-v1.schema.json` 提供 JSON Schema。

不依赖游戏、引擎或 ContentServer。此协议描述服务器源，不承担 P2P 信令或房间管理。

[服务器源协议](https://gitee.com/Yunus0712/scnet/blob/main/Doc/Protocol/ServerSourceProtocol.md)
