# SCNET.Game.Protocol

提供游戏消息契约和二进制编解码，依赖 Engine.Core 和 MessagePack。
线格式与准入见[游戏联机协议](../Doc/Protocol/GameProtocol.md)，跨层边界见[协议索引](../Doc/Protocol/README.md)。

## 名称空间

目录与名称空间按协议职责对应，不按游戏组件或调用入口分层：

| 名称空间 | 职责 |
| --- | --- |
| `Game.Protocol` | 协议版本、消息接口、包类型、通道、通用标识和限制 |
| `.Connection` | 连接握手、准备阶段、服务端信息及模组准备描述 |
| `.Characters` | 角色操作、状态与列表 |
| `.Teams` | 服务器队伍提示与快照 |
| `.Entities` | 实体、身体、生命状态、库存与衣物、掉落物和投射物同步 |
| `.Blocks` | 方块编辑、方块映射、电路及可编辑方块状态 |
| `.World` | 世界快照、时间、季节、天气和爆炸 |
| `.Terrain` | 地形请求、区块增量、分片与重组 |
| `.Commands` | 命令调用、响应、权限和补全 |
| `.Messaging` | 游戏消息及消息展示描述 |
| `.Mods` | 模组消息封套 |
| `.Localization` | 本地化文本描述 |
| `.Serialization` | MessagePack 契约配置、消息编码、数据帧及压缩 |


程序集版本使用 `GameProtocolPackageVersion`，连接兼容版本使用 `GameProtocolVersion`。
开发阶段的兼容版本约定见[协议索引](../Doc/Protocol/README.md)。

## 消费协议

- `ProtocolPacketCodec` 对完整数据报批量编码和解码。消息在 `IProtocolMessage` 上使用显式数字 Union 标签，
  字段使用数字 Key。通道编号来自 `NetworkChannel`，收件人、可靠性与发送时机由调用方选择。
- `ProtocolPayloadCodec` 编解码命令和模组的强类型参数。游戏采集世界快照并验证权限、数值和业务规则。
  消息只携带传输所需数据，HTTP 及文本入口由各自适配器处理。
- `WorldMetadata` 和 `CharacterEntitySnapshot` 提供薄客户端使用的数据。轮次、区块版本和缺片状态
  有独立的协议值类型。游戏负责选择分配身份、兴趣范围和何时应用结果。
- 游戏模式、季节、角色类别和拖拽方式以 `byte` 传输，实际取值由游戏定义并验证。
  通信状态使用协议枚举，例如阶段同步 `SyncAction` 和交互种类 `InteractionMessageKind`。

请求大小及字段约束见 [ProtocolLimits](ProtocolLimits.cs)。网格、区块分片及同步策略见[地形分发](../Doc/TerrainDistribution.md)，
命令和实体复制见[网络复制](../Doc/NetworkReplication.md)。

## 玩家身份与角色数据

`PlayerAddress` 采用 Engine.Core 的 SCNET Bech32m 编码。它标识玩家的公开身份。角色 ID、临时连接编号
和启动会话 ID 各自独立。角色集合使用 `Characters`，队伍成员使用玩家地址。队伍业务操作通过游戏命令办理，
`TeamManagePackage` 仅用于服务端提示和快照。

## 消息所有权

`ProtocolMessageOwnership.Capture` 按契约取得独立的可变负载副本：数组、列表、字典、嵌套联合成员
和 JSON 都分离；只有不可变值可以共享。复制前后校验协议值，不经过二进制编解码。调用期间发送方需保持输入稳定，返回后可继续修改自己的原始数据。
复制计划由 `Game.Protocol.Generators` 在编译期按联合与 Key 属性生成，运行时不使用反射。新字段
自动进入计划；未支持的可变类型、只读可变成员或循环契约以 `SCNETOWN001` 阻止编译，需先明确
其所有权规则。生成器只用于构建，不是运行时依赖或独立发布包。
