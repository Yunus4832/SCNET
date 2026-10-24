# SCNET.Survivalcraft

SCNET 游戏运行时及模组开发契约，支持 .NET 10 和 .NET 10 for Android，主命名空间为 `Game`。
包含世界、方块、生物、联网、模组和界面等游戏逻辑；平台启动器与内容发布服务不在本包中。

## 模组引用

```xml
<PackageReference Include="SCNET.Survivalcraft" Version="0.1.0" ExcludeAssets="runtime" />
<PackageReference Include="SCNET.ContentTool" Version="0.1.0" PrivateAssets="all" />
```

游戏 SDK 及引擎用于编译；实际运行时由游戏宿主提供，不应复制进 `.scpkg`。
ContentTool 是独立的构建工具，负责生成内容包，不会成为游戏 SDK 的运行时依赖。
依赖通过 NuGet 传递，模组项目无需逐一引用 Engine、EntitySystem 或原生资产包。

[模组开发](https://gitee.com/Yunus0712/scnet/blob/main/Doc/Modding.md) · [NuGet 包说明](https://gitee.com/Yunus0712/scnet/blob/main/Doc/NuGet.md)

## 玩家、角色与会话

- `PlayerAddress` 表示持钥玩家身份，与角色和连接编号无关；实例身份配置使用 `Player` 和 `Server` 两种独立用途。
- `Game.Players.WorldPlayer` 是存档内的玩家记录，保存角色归属、权限、禁言和封禁；`WorldPlayerStore` 管理其持久化。
- `CharacterData` 保存角色名称、外观、出生和进度，`ComponentCharacter` 是角色实体组件；`CharacterId` 是持久角色标识，`CharacterIndex` 仅用于世界内运行时索引。
- `Client` 是连接会话，绑定已验证的 `PlayerAddress` 和当前控制的 `CharacterId`。临时连接编号及会话 GUID 不作为玩家或角色身份。
- `SubsystemPlayers.Store` 管理存档玩家；`SubsystemCharacters` 管理角色集合、实体、生成、重生和在线／离线状态；`SubsystemTeams` 管理玩家队籍。
- `SubsystemPositionMarks` 独立管理世界公共标记和按玩家地址划分的私有标记，不依赖角色是否存在。
- `CommandPrincipal.Subject` 是执行操作的存档玩家；`Character` 是可为空的当前角色。权限来自玩家，不来自角色。
- 角色界面与展示使用 `CharacterScreen`、`CharacterModelWidget`；`PlayerInput` 仍表示玩家输入，Headless 或未控制角色的玩家不因此获得角色实体。

开发阶段不保留旧类型别名或旧存档字段解析；修改 API、资源注册和序列化字段时同步更新游戏、模组和工具。

角色实体数据库模板为 `Character`、`MaleCharacter` 和 `FemaleCharacter`，角色组件成员键统一为 `Character`；
生成覆盖数据将 `CharacterId` 写入该成员。`Characters` 子系统模板明确声明 `Characters`、`OfflineCharacters`，
与运行时代码读取的集合一致。AI 寻找、避让角色使用 `FindCharacterBehavior`、`AvoidCharacterBehavior`，
对应 `CreatureTargetingKind.FindCharacter` / `AvoidCharacter`；`ChaseNonCharacterProbability` 表示追逐非角色生物的概率。

## 队伍

`SubsystemTeams.Store` 持有 `TeamStore`，只管理以 `PlayerAddress` 标识的成员、独立 `Guid` 队伍 ID、队长及邀请事务。
队长离队会解散队伍；普通成员离队只退出自己的队籍。所有成员可以邀请新玩家，加入申请由队长审批。
每名玩家最多一队，待加入玩家最多一个未处理请求；请求有效期 60 秒，发起操作按玩家限制为每秒一次。
断线取消相关待处理事务，保留持久队籍。响应只允许指定玩家执行一次，加入时重新验证当前队伍和参与者状态。

核心不依赖角色、连接、发包或界面。命令层负责准入及在线检查，网络层负责提示和服务器快照；
客户端队伍副本拒绝业务写入。公开队伍集合和成员集合均只读，加载及替换先完整验证再提交。
`CharacterTeamQuery` 按“当前操作者 → 队伍 → 当前控制角色”组合展示，不缓存角色队籍，不改变角色所有权。

## 玩家与角色边界

`SubsystemPlayers` 仅加载和保存 `WorldPlayerStore`，不持有角色实体或连接，也不负责进入角色界面。
角色子系统通过玩家记录验证归属，玩家子系统不反向依赖角色生命周期；队伍也只依赖玩家记录。
角色状态事件统一为 `CharacterAdded`、`CharacterRemoved`、`CharacterListChanged`，与会话连接事件区分。

`PlayerSessionQuery` 提供会话、实际操作者、控制角色和展示名称的只读组合查询；
`PlayerSessionBinding` 负责当前一玩家一角色阶段的初始会话绑定。
角色元数据不创建玩家记录，角色模型不再提供隐式创建玩家的 `Owner` 属性。
客户端私有权限记录只由明确的服务器权限消息更新，公开的角色归属地址不作为认证或权限来源。

角色子系统仍是游戏运行时协调者，包含网络状态同步、启动角色准备及界面切换；
这些属于角色工作流，并不表示角色数据与玩家私有记录重新合并。
后续多角色和角色转移需扩展显式控制授权，不依赖队伍或把所有权当作当前控制关系。
