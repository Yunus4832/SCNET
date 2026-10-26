# 协议索引

本目录保存当前源码对应的协议契约。实现细节、游戏业务、部署配置和临时设计记录不互相替代。
SCNET 仍未发布：现有线格式整数保持 `1`，不为开发迭代保留旧解析分支；产品及 NuGet SemVer 独立管理。

## 协议与职责

| 契约 | 实现归属 | 解决的问题 |
|---|---|---|
| [内容包](ContentPackageProtocol.md) | `Content.Packaging` | `.scpkg`、载荷、逻辑哈希与安装边界 |
| [内容制造](ContentCreationProtocol.md) | 游戏制造入口、ContentTool、ContentServer/WebUI | 如何创建、保存草稿和提交不可变内容包 |
| [服务器源](ServerSourceProtocol.md) | `ServerSource.Protocol` | HTTP 只读游戏服务器目录 |
| [信令与中继](RendezvousProtocol.md) | `Rendezvous.Protocol`、RendezvousServer | 临时房间、地址交换、打洞与中继能力 |
| [安全传输](TransportProtocol.md) | `Engine.Network` | DTLS、对端持钥证明、通道绑定与路径保护 |
| [游戏联机](GameProtocol.md) | `Game.Protocol`、游戏处理器 | 二进制消息、准入、世界同步及业务授权边界 |
| [外部认证](AuthenticationProtocol.md) | `Authentication.Protocol`、独立认证服务 | 签名事务、网页交互、grant 和连接批准 |

同名的 `v1` 路径或版本整数不表示这些协议共用消息格式或状态。
信令里的“认证中继”表示持有路由能力，不是玩家账号认证；服务器源也不证明游戏服务器身份。

## 从发现到进入游戏

1. 玩家从局域网、本地记录、服务器源或信令房间选择目标。
2. 客户端准备实际路径，探测服务器信息并核对、准备所需模组；必要重启只恢复目标与 profile。
3. 新连接完成 DTLS 持钥证明，得到双方身份和本次连接绑定。
4. 游戏执行开放、密码或外部认证准入；只有外部模式访问认证服务器。
5. 准入与存档业务检查通过后，服务器才将玩家加入名单，发送 Bootstrap 并推进世界同步。

重启不恢复认证上下文；已保存的有效 grant 或服务器密码可按普通连接再次使用。
同一进程浏览器交互期间的断线恢复仍需新连接、新挑战与新批准。

## 身份、来源与业务

- `scnet1...` 是公钥派生的身份地址；域名、IP、显示名称、目录记录 ID 都不能代替它。
- ContentServer 的认证服务目录只审核来源 URL 和展示资料，不记录认证公钥或身份。
- 客户端安装认证服务时直接发现并由玩家确认公钥；游戏服务器单独配置自己信任的认证服务公钥。
- 认证服务不管理游戏角色、存档权限、队伍或存档封禁，也不需要游戏服务器注册。
- 角色 ID、临时连接编号与玩家身份独立；每个游戏请求仍须按已验证会话执行业务授权。

来源目录 API 见 [ContentServer API](../ContentServerApi.md) 和[认证来源目录](../ContentServer.md#认证服务目录)；
业务复制流程见[网络复制](../NetworkReplication.md)，实例与重启规则见[启动会话](../StartupSessions.md)。

## 实现与验证入口

- .NET 协议项目 README： [游戏](../../Game.Protocol/README.md)、[服务器源](../../ServerSource.Protocol/README.md)、[信令](../../Rendezvous.Protocol/README.md)、[认证](../../Authentication.Protocol/README.md)。
- [内容哈希向量](PackageHashVectors.json)与[外部认证独立签名向量](../../Authentication.Protocol.Test/Fixtures/README.md)。
- 接入工具：[NetworkProbe](../../NetworkProbe/README.md) 验证传输路径，[ServerLoadTool](../ServerLoadTesting.md) 消费游戏协议。

参考实现限制须与协议要求区分。例如认证参考服务只有管理员预置账号密码，不要求第三方采用同一账号方式。
