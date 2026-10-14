# 命令身份、执行范围与权限

## 三种身份

- `LocalOperator`：本地进程控制者。GUI 管理操作、Headless 控制台及经过 Bearer 认证的本机 HTTP 使用同一身份，控制本进程。
- `Player`：世界中的玩家。本地房主与远程玩家没有身份或权限差别；从玩家记录读取权限，不根据连接顺序、设备或 IsMainPlayer 提权。
- `System`：内部自动流程。网络及 HTTP 请求不能自行声明该身份。

本地控制者与玩家可以是同一个人，但调用路径分别表达不同身份。GUI 玩家命令栏的世界／服务器操作仍使用 Player；本地应用设置使用 LocalOperator。认领服务器只授予玩家标准权限管理与再授权能力，不赋予全部业务权限。

## 范围、身份和环境分别判断

| CommandDomain | 执行范围 | 默认允许身份 |
| --- | --- | --- |
| Application | 当前本地应用 | LocalOperator、System |
| World | 单机世界或本机权威服务端世界 | LocalOperator、Player、System |
| Server | 本机正在运行的服务器 | LocalOperator、Player、System |

调用者的 `CommandPrincipalKind` 是单一身份；命令的 `AllowedPrincipals` 使用独立的 `CommandPrincipalKinds` 集合，可进一步声明需要具体玩家数据等身份约束。`HostRequirement` 限定 GUI 或 Headless 环境；`CanExecuteHere` 判断实际执行范围。具备权限不代表世界已加载或服务器已经监听，业务处理器仍检查动态条件。

玩家请求通过 CommandGateway：Application 在玩家所在进程执行，World／Server 在本机权威世界执行或通过 CommandPackage 发往服务器。服务器使用连接绑定的真实 Player 身份与服务器保存的权限。不能由客户端提供操作者身份或授权集合。

本地管理调用使用 `CommandExecutor.ExecuteLocal` 或 `CommandGateway.SubmitLocal`；玩家操作使用 ExecutePlayer／Submit。不能为了方便将玩家管理请求改为 ExecuteLocal。玩家聊天的本地操作者补全仅限 Application 范围，不能混入操作者才能执行的世界／服务器管理命令。

HTTP 宿主提供两组独立入口，沿用 Bearer、显式启用及 loopback 监听：

- `GET/POST /commands`：GUI 与 Headless 的本进程控制入口，使用 LocalOperator。客户端不转发此入口的世界／服务器命令。
- `GET/POST /player/commands`：仅 GUI 提供，要求当前有活动玩家，使用真实 Player。单机／GUI 服务端在本地执行，客户端经 CommandGateway 和 CommandPackage 请求远程服务器。Headless 返回 404，不代表在线的远程玩家调用。

两组入口只暴露显式 HTTP binding。玩家入口只查询及调用 World／Server 命令；远程查询由服务器按该玩家权限返回，远程调用再次校验。请求体不接受身份、权限或玩家 GUID 来替换当前调用者。玩家切换、退出世界或连接断开时尚未完成的请求失效。

POST 使用相同的 identity/arguments 格式；返回 success/code/message/state/data。Pending 表示业务尚未完成，不表示最终成功。请求最多等待 15 秒，超时返回 504；已经送到服务器的操作不能因为 HTTP 超时而撤销，重试前应查询实际状态。HTTP 请求关联 ID 与内部远程请求 ID 分离，迟到回复不会混入聊天或匹配到新的请求。

调用入口使用 Text（玩家文本）、UserInterface、Console、HttpApi 或 Internal 表达，不通过 Mod 标签表达命令归属。模组通过注册所有者及命令命名空间标识归属，与执行身份无关。玩家主体通过 FromPlayer 从玩家记录构造；本地操作者及系统使用固定主体，不能混入玩家数据或玩家授权。

## 权限执行与授予

具体命令声明 RequiredPermission；未声明时只执行身份和环境检查。已注册权限的检查顺序：LocalOperator／System 放行，随后检查隐式业务授权条件，再检查玩家显式授权。业务隐式条件应明确，例如创造模式玩家可以控制天气；房主身份不是隐式条件。

玩家授权包含权限标识及 CanDelegate，并随世界玩家记录保存。持有使用权不等于可以再授权。

| GrantPolicy | 玩家使用及授权规则 |
| --- | --- |
| Standard | 玩家可获得使用权；有对应再授权能力或标准权限管理能力者可授予 |
| OperatorManaged | 本地操作者可授予玩家使用权；玩家不能继续授权 |
| NonGrantable | 不允许显式授予玩家；只允许本地操作者／系统及权限定义中明确的隐式条件 |

标准权限管理能力不会自动赋予其他业务权限。授权管理者可向自己或其他玩家授予目标权限；再授权能力的授予也受 CanDelegate 校验。

## 非命令功能的边界

领地保护由领地石模组负责；玩家面板黑名单的重构暂缓。这些功能仍使用 ServerManager／ServerMaster，不参与 CommandPrincipal.FromPlayer 的权限生成，不在本次命令迁移范围内。调试相机不检查玩家管理权限，沿用创造模式的可用条件。
