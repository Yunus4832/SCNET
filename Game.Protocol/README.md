# SCNET.Game.Protocol

游戏线格式与准入总览见 [游戏联机协议](../Doc/Protocol/GameProtocol.md)，跨层边界见[协议索引](../Doc/Protocol/README.md)。

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


游戏通信契约与二进制编解码。依赖基础数学能力与 MessagePack，不引用游戏运行时、图形窗口或网络传输实现。

程序集发布版本由 `GameProtocolPackageVersion` 管理；通信兼容代际由 `GameProtocolVersion` 管理。未发布阶段后者固定为 `1`，不随开发迭代或库版本自动递增。

游戏与工具直接引用协议数据，游戏负责生成稳定快照、认证授权和业务处理，传输层负责收发字节。

## 数据边界

- `WorldMetadata` 和 `CharacterEntitySnapshot` 提供薄客户端需要的世界、玩家实体摘要；游戏自己的世界与实体序列化内容以字节快照传输，由游戏解释。
- `ContentRepositoryDescriptor` 与 `RequiredModProfile` 只描述服务器公布的下载来源和模组要求，不携带游戏配置对象、启用状态或客户端本地配置。编解码器限制载荷结构；游戏在使用前仍执行模组语义版本和配置规则校验。
- 游戏概念 `GameMode`、`CharacterSex`、`Season` 和库存拖拽方式 `DragMode` 定义在游戏各自的领域名称空间中。协议不定义这些枚举或副本，对应字段使用 `byte`。游戏边界负责验证数值并转换，未知值不能直接应用或当作默认值。
- 消息种类、连接阶段、持续动作同步阶段（`SyncAction`）与单次交互种类（`InteractionMessageKind`）和内置编辑载荷标签（`BuiltInBlockEditKind`）等通信语义仍使用协议枚举。协议只处理结构与传输约束，不验证游戏枚举的业务取值。

| 协议字段 | 传输类型 | 游戏含义 |
|---|---|---|
| `ServerInfoResponsePackage.GameMode`、`WorldMetadata.Mode` | `byte` | 游戏模式，当前 `0` 为创造模式 |
| `ServerInfoResponsePackage.Season` | `byte` | 季节 |
| `CharacterProfilePackage.CharacterSex`、`CharacterCreateRequestPackage.CharacterSex` | `byte` | 角色性别类别，当前 `0` 为男性、`1` 为女性 |
| `InventoryDragDropRequestPackage.DragMode` | `byte` | 库存拖拽方式，当前 `0` 为全部、`1` 为单件 |

薄客户端独立消费这些数值，只声明其工作负载确实支持的值，不引用游戏枚举。`Season` 同样使用单字节，不提供旧四字节格式解析。

瞄准和挖掘共用 `SyncAction` 的 `None / Start / Cancel / Complete`。交互消息用 `InteractionMessageKind` 区分 `None / Use / Place`；放置是独立操作，不是普通交互的结束阶段。

游戏负责在发送前采集不可变的数据快照。协议消息和编解码器不查询当前世界、不创建运行时实体，也不执行安装或连接操作。

`Terrain` 目录包含区块分配轮次、内容版本、分片与单元格增量等值类型。协议负责其线格式，游戏继续负责分片生成、缓存、背压、预测以及重组结果的应用；纯分片重组器由协议库共用。实体状态批次中的 `BodyItem` 保持值类型；游戏为每轮采集完整状态，并按状态轮次忽略旧快照。

角色操作以 `ComponentCharacterPackage` 为领域基类，位置、挖掘、放置、交互、瞄准等使用具体记录。
客户端、库存、家具、地形、命令等多操作家族同样使用独立具体消息，操作枚举仅供游戏策略使用，不参与字段选择。
`IProtocolMessage` 以显式数字 Union 标签登记具体消息，各字段使用连续数字 `Key`。
MessagePack 编码不携带运行时类型名；包内是消息数组，外层继续使用长度和压缩帧。

所有线格式配置、数学值及协议值格式化器均属于 `Game.Protocol.Serialization`，不引入引擎序列化库或传输层。
游戏在处理器中解析玩家并验证发送者，序列化器只编码已确定的数据契约。

聊天契约位于 `Game.Protocol.Messaging`，包含正文片段、样式、本地化键与参数。协议不查找本地语言；游戏通过 `GameMessageLocalization` 创建或解析本地化消息。`TerrainUpdateLocation` 是游戏更新器与协议共用的位置值类型，游戏负责有效性和距离约束。

角色数据使用创建请求、创建拒绝、资料修改和地形兴趣位置等具体消息。队伍提示和快照仍由服务器产生，协议记录不执行队伍操作。

掉落物新增批次使用 `PickableSnapshot` 值类型；高频位置批次使用仅含编号和位置的 `PickablePosition`；批量删除只传编号。游戏侧在入队前采集状态并设置轮次，协议层不读取帧时钟，也不创建运行时物品或投射物。

格面坐标 `CellFace` 及其基础几何运算由协议库提供，游戏和编解码共用该值类型。可编辑方块只传目标、编号、字节或参数；真值表、存储器及库存修改由游戏处理。家具设计的 XML 是游戏侧预先序列化的载荷，设计校验和链接重建不在协议编解码器中执行。

库存协议使用独立的槽位引用和槽位快照；普通槽位传数量与值，衣物槽位传值列表。游戏侧采集并冻结批次，再按权限应用到实际库存。

命令调用统一使用 `Request`，携带命令标识、二进制参数（上限 4 KiB）和关联编号；`Result` 统一返回关联结果，由接收端根据本地请求上下文决定如何处理。HTTP、文本和 UI 适配器在游戏侧构造命令，随后共用注册命令的二进制编解码。协议不传输 HTTP 请求封套、JSON 参数或待执行的原始文本，也不记录请求入口。目录查询使用普通游戏命令 `game:commands/catalog`，服务端按玩家权限返回可调用命令的标识，客户端适配器在本地补充入口所需的参数说明。补全请求仍携带待补全的文本，它只查询候选项，不执行命令。

`CommandResponse`、权限项、补全项和本地化描述符是协议契约。完整的 `Game.Commands.CommandResult`、发布策略、展示策略及便利工厂方法属于游戏，协议不传输 `Sensitive` 或 `Audience`。游戏先决定收件人，再采集响应快照，并复制可变 JSON 和参数数组。

统一响应包含成功状态、业务代码、完成状态、消息/本地化描述、结果数据及显示提示。请求消息只区分参数编码和操作，不传输 HTTP、聊天框等入口身份。客户端按关联编号查找本地调用上下文，决定完成等待任务或展示结果；已取消请求的近期响应不转为界面通知。客户端收到响应后由游戏侧恢复本地结果，不接收服务端发布策略。本地化解析、HTTP 请求转换、权限与命令执行仍在游戏侧。游戏与模组使用类型化 MessagePack 载荷，不提供逐字段读写回调。

`ProtocolPacketCodec` 提供完整数据报的批量封装与解析（包含外层长度），可直接供不引用游戏的工具使用。`TerrainChunkFragmentReassembler` 共用结构校验和缺片状态；游戏仍决定分配代际、兴趣范围和内容应用。通道编号由 `NetworkChannel` 共用，可靠性和发送调度由游戏或工具选择。

解码先按契约检查长度、深度、必填字段和变体，再执行协议边界校验；游戏继续负责权限和领域规则。
命令参数使用 `ProtocolPayloadCodec`，远程调用能力在命令注册中显式声明。
模组可以发送带数字 Key 的 MessagePack 数据契约，并通过 `Read<T>()` 读取。

## 玩家身份与角色数据

`PlayerAddress` 使用引擎基础层的统一身份编码：32 字节公钥指纹编码为 `scnet` 前缀的 64 字符 Bech32m，保存和输出采用小写。所有主体地址均校验前缀、校验和和填充位；不接受旧 Base64URL 地址。账户、角色 GUID 和会话编号分别建模。


角色状态和操作消息归属 `Game.Protocol.Characters`，队伍控制消息归属 `Game.Protocol.Teams`。
`CharacterDataPackage`、`CharacterListPackage`、`OnlineCharacterStatePackage` 和角色实体快照使用角色名称；
其集合字段统一为 `Characters`，不再使用指代玩家身份的 `Players`。
`ComponentCharacterPackage.ControllerSessionId` 是控制该角色的临时 byte 连接编号，用于路由和归属校验，
不是 `PlayerAddress`、`CharacterId` 或持久会话 GUID。未发布的协议兼容整数保持 `1`。


`TeamManagePackage` 只含服务器提示和 `SyncTeams` 快照；队伍修改通过已验证会话中的游戏命令执行。
`TeamState` 使用独立 `TeamId`、队长地址与成员地址，不携带角色成员列表。客户端不能通过提交快照修改服务器队伍。
