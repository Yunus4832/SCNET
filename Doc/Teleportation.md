# 玩家传送与标记

传送在单机的权威世界或联机服务端执行。GUI、远程玩家和本地控制台共用命令处理器，
但调用身份不同；权限模型见 [命令权限](CommandPermissions.md)。

## 命令格式

```text
/mark                                  # 保存当前位置为 private previous；键盘 M 同义
/mark private home                     # 保存个人标记，同名覆盖
/mark public spawn                     # 保存公共标记，需要 game:world.mark.manage
/tp                                    # 返回 private previous；键盘 F6 同义
/tp self private previous
/tp self private home
/tp self public spawn
/tp self player "Player Name"
/tp self position 100 70 -200
/tp player "Player Name" position 100 70 -200
/tp player "Player Name" player "Destination Player"
/tp player "Player Name" private spawn
/tp player "Player Name" public spawn
```

`self` 表示调用玩家，`player` 后必须明确指定被传送玩家。名称不区分大小写，也可以使用
角色 ID；包含空格的名称需加引号。坐标为脚底位置，仅接受绝对坐标。
控制台没有当前玩家，必须明确指定被传送玩家。

M、F6 只在窗口激活、玩家存活且没有 HUD 面板或对话框占用输入时生效。
M 不授予传送权限，F6 仍受传送权限与落点检查约束。

## 标记保存与返回点

标记保存在当前世界 `Project.xml` 的 `ExtensionData/game/position-marks`：
`Private` 按主体地址分组，`Public` 保存公共标记。两者独立、不跨世界共享。
标记名称不区分大小写，长度为 1–64 个字符。联机标记写入和解析由服务端执行，
客户端不接收他人的个人标记全集。保存标记和自身标记补全使用实际操作者主体，角色归属不决定私有标记作用域；没有可确定主体时拒绝操作，不回退到角色拥有者。

`previous` 是普通主体私有标记。成功传送时将角色实际离开的位置写入当前控制主体的该标记，
因此连续执行裸 `/tp` 可往返切换；失败、取消或仍在等待时不修改。
未保存过该标记时，裸 `/tp` 返回标记缺失提示。地形变化后的返回点仍需重新接受安全检查。

角色首次进入 Playing 时，服务端在缺失的情况下，将实际出生位置写入当前控制主体的私有 `spawn`；
世界公共 `spawn` 采用首次完成该初始化的玩家实际出生位置。这些都是可覆盖的普通标记，
后续睡觉或重生不会自动更新，覆盖标记也不改变真实出生机制。

## 授权与补全

| 操作 | 权限 |
| --- | --- |
| 保存个人标记 | 不需要额外权限 |
| 保存公共标记 | `game:world.mark.manage`，创造模式默认允许 |
| 传送自己 | `game:player.teleport.self`，创造模式默认允许 |
| 传送指定玩家 | `game:player.teleport.others`，创造模式不自动授予 |

以上管理权限使用标准授权策略。认领服务器获得权限管理能力，不自动获得传送他人的使用权。
有授权能力的调用者可按主体地址授予使用权，或同时授予再授权能力。`/permission players` 列出存档中的玩家，包含离线及尚无角色的玩家：

```text
/permission grant <主体地址> game:player.teleport.others
/permission delegate <主体地址> game:player.teleport.others
```

`/tp player A` 不完整，不能执行；整个 `player` 分组始终要求传送他人的权限，
即使 A 恰好是调用者也不切换语义。`private` 查找目标角色当前控制主体的私有标记，`public` 查找公共标记。

补全按调用玩家的权限和作用域过滤，只回复请求者。一级候选为 `self`、`player`；目的地类型为
`position`、`player`、`private`、`public`。有权限传送他人时可以补全目标角色当前控制主体的私有标记名称，
但不会广播这些名称或传输坐标。`/mark` 也可补全已有标记以便覆盖。
联机补全有短暂输入防抖，迟到响应不会覆盖新输入，也不会执行命令。

目的玩家候选排除被传送玩家本人，被传送玩家候选则保留调用者。手动指定自己到自己时返回
`teleport.unchanged`：不移动、不取消已有等待，也不覆盖 `previous`。

## 落点检查与异步完成

目标玩家必须在线、进入世界、存活且未骑乘。坐标 X/Z 范围为 ±1000000、Y 为 0–255，
拒绝非有限数值。传送按玩家身体大小检查地形和移动方块的实际碰撞盒，空间不足时拒绝；
不会自动寻找安全落脚点，也不检查坠落或岩浆危险。

落点区块尚未加载时，先异步准备小范围地形，返回 `teleport.loading`（Pending），
最多等待 30 秒。期间玩家留在原地，完成后重新检查权限与碰撞。成功后重置速度并同步位置，
不改变出生点、朝向或飞行状态。

每个玩家最多一个等待请求，新传送替换旧请求；目标死亡、离线、骑乘或实体替换会取消等待。
完成、失败、取消或退出世界均释放临时地形加载位置，不持久化等待状态。
远程玩家通过原请求的关联 ID 收到最终结果，本地调用者通过消息系统接收。
HTTP 返回 Pending 后不继续等待业务完成，最终结果记录在服务端日志中。

消息面板默认执行后返回消息输入。传送在实际成功后关闭面板，失败或 Pending 不关闭；
用户已经开始新输入或重新打开面板时，旧结果不改变当前面板。该行为由命令注册声明。

## HTTP 适配

本地控制入口提供 `game:player/teleport/other`，必填 `player`，另提供完整的 `x`、`y`、`z`
或 `destination` 玩家名称。GUI 与 Headless 的 `/commands` 都使用 LocalOperator，在本进程执行；
GUI 客户端不会把该入口的世界命令转发给远程服务器。

代表当前 GUI 玩家执行时，使用 `/player/commands`，由权威服务端检查真实玩家权限。
Headless 没有玩家入口。先发现实际 binding 与参数再调用，启用方法见
[HTTP 命令宿主](Headless.md#http-命令宿主)。
