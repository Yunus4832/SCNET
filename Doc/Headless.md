# Headless 模式

Headless 模式在不打开游戏窗口的情况下运行服务端逻辑，适合部署独立联机服务器。

它复用同一套世界、网络和模组运行时，但跳过图形界面流程。启动目标和世界都通过启动会话系统解析，详见 [StartupSessions.md](./StartupSessions.md)。

## 启动方式

### Windows

```bash
SurvivalcraftStarter.exe --server
```

等价短参数：

```bash
SurvivalcraftStarter.exe -d
```

### Linux

```bash
./SurvivalcraftStarter --server
```

开发环境可以直接运行 Linux 项目：

```bash
dotnet run --project Survivalcraft.Linux/Survivalcraft.Linux.csproj -- --server
```

### Android

Android 同样通过 `RunningSettingManager` 选择 GUI 或 Headless。正常应用重启会读取 `config:RunningSetting.xml`；ADB 调试也可以通过 `Survivalcraft.Android.CommandLine` Intent Extra 临时传入 `--server`、`--session`、`--world`、`--game-mode` 等同一套参数。`Survivalcraft.Android.InstanceId` 用于选择隔离的数据实例。

## 常用参数

- `--instance <实例名>`: 选择或创建 `Instances/<实例名>` 数据实例；省略时由 `Starter.xml` 选择
- `--server-port <端口>`: 仅覆盖本次运行的游戏端口
- `--broadcast-port <端口>`: 仅覆盖本次运行的广播端口
- `--http-command`: 为本次有效 session 启用 loopback HTTP 命令宿主
- `--no-http-command`: 为本次有效 session 禁用 loopback HTTP 命令宿主
- `--http-command-port <端口>`: 覆盖 loopback HTTP 命令宿主的默认端口 `28889`
- `--http-command-access-token <Token>`: 覆盖本次运行使用的 Bearer Token；至少 32 个字符
- `-d` / `--server`: 切换到 `HeadlessServer`
- `--session <名称>`: 选择或创建一个具名启动会话
- `--world <名称>`: 给 `--session` 指定世界名或世界目录名
- `--seed <种子>`: 给 `--session` 指定新建世界时使用的种子
- `--game-mode <模式>`: 给 `--session` 指定游戏模式覆盖
- `--log-level <级别>`: `Debug`、`Verbose`、`Information`、`Warning`、`Error`
- `--save`: 保存适用的 `RunningSetting` 字段，并将当前有效 session 写入 `SessionInfo.xml`

`--world`、`--seed` 和 `--game-mode` 只有在同时指定 `--session` 时才会生效。没有 `--session` 时它们会被忽略，避免一次临时命令覆盖默认启动状态。
模式可取 `Creative`、`Harmless`、`Survival`、`Challenging`、`Cruel`、`Adventure`。新世界以该模式创建；已有世界仅在有效 session 中使用该模式，保存时仍保留原存档模式。
端口和游戏模式覆盖默认只作用于内存中的有效 session；与 `--save` 同时使用时才会写入 `SessionInfo.xml`。Session 未指定端口时回退到 `Settings.xml` 的默认端口。

示例：

```bash
./SurvivalcraftStarter --instance server --server --session survival --world World --seed 123456 --game-mode Creative --log-level Information --save
```

再次启动同一个服务器：

```bash
./SurvivalcraftStarter --instance server --server --session survival
```

## HTTP 命令宿主

HTTP 命令宿主默认关闭；`Settings.xml` 的 `HttpCommandEnabled` 是实例默认开关，启动参数或保存的 session 可以覆盖它。启用后默认使用 `28889`，并且当前只监听 `127.0.0.1`。可以为多实例指定其他端口：

```bash
./SurvivalcraftStarter --instance server --server --session survival --http-command --http-command-port 29889
```

端口和长期 access token 保存在当前实例的 `Settings.xml`，默认端口为 `28889`。旧配置没有 `HttpCommandAccessToken` 或 token 无效时，加载设置后会生成一个 256-bit 随机 token 并立即保存。配置端口无效、端口被占用或监听失败时会记录明确错误，并且本次运行不启动 HTTP Host；游戏和服务器本身继续启动，不会静默改用其他端口。

`--http-command`、`--no-http-command`、`--http-command-port` 和 `--http-command-access-token` 会先合并进本次有效 `SessionInfo`，优先级高于 `Settings.xml`。默认只影响本次启动；与 `--save` 同用时写入对应命名 session，之后按该 session 启动可以恢复相同的 HTTP 配置覆盖。命令行参数可能被本机进程查看工具读取，因此长期部署应保护好实例的 `Settings.xml` 和 `SessionInfo.xml`。

```http
POST /commands
Authorization: Bearer <token>
Content-Type: application/json

{"identity":"game:world/time/get","arguments":{}}
```

HTTP 只暴露显式注册了 `HttpCommandBinding` 的命令。Headless HTTP 请求以 `ServerOperator` 执行，但仍受命令域、宿主要求和权限规则约束。当前监听仅限本机；跨主机管理应通过 SSH 隧道等受保护的传输访问，不应直接公开端口。

认证后可通过 `GET /commands` 发现当前宿主实际可执行的 HTTP 命令。返回值包含稳定 identity、本地化说明和显式声明的参数名、类型与必填状态；它已按当前运行模式、宿主主体和权限过滤。客户端应先发现命令，再以同一 identity 向 `POST /commands` 发送调用请求。

```bash
curl -H "Authorization: Bearer <token>" http://127.0.0.1:28889/commands
```

GUI 模式还提供 UI 发现、点击、拖放、滚动、截图、组合键鼠输入、文本输入、角色观测和步行导航，完整合同见 [游戏自动化](Automation.md)。操作通过正常输入和角色运动路径执行。Headless 不会发现或执行这些 GUI 专用命令。

GUI 与 Headless 都提供只读命令 `game:diagnostics/network/get`，无参数。返回当前 NetNode 生命周期内的连接内发送累计统计：通道的应用层发送批次与编码字节，以及各包类型的接收方投递次数与未压缩载荷字节。两次读取的差值可用于比较测试时间段；编码字节不含传输协议头、底层重传与分片开销，载荷字节不含包标记且不能与编码字节相加。未发送或延后的快照不计入，无连接发现消息不计入。该命令仅供应用用户与服务器操作员使用，不向普通联机玩家开放。

Headless 的诊断还返回 `HeadlessTicks`，记录进程内累计 tick 工作耗时与超预算次数；GUI 为 null。
独立瘦协议客户端负载工具及结果解读见 [服务端负载测试](ServerLoadTesting.md)。

## 玩家传送

内置 `/tp` 命令在本地世界执行，联机时由服务端授权并执行：

```text
/mark                       # 标记自己的当前位置，也可按键盘 M
/mark private home          # 保存当前存档内自己的命名标记；同名覆盖
/mark public spawn         # 保存当前存档内公共命名标记；需要 world.mark.manage 权限
/tp self private home       # 传送到自己的命名标记
/tp self public spawn       # 传送到公共命名标记
/tp                         # 传送到自己的返回点
/tp self private previous   # 与裸 /tp、键盘 F6 相同
/tp self private spawn      # 自己的出生位置快照
/tp self public spawn       # 世界出生位置快照
/tp self player "Player Name"
/tp self position 100 70 -200
/tp player "Player Name" position 100 70 -200
/tp player "Player Name" player "Destination Player"
/tp player "Player Name" private spawn
/tp player "Player Name" public spawn
```

不带参数的 `/mark` 和键盘 M 都提交与 `/mark private previous` 相同的命令；裸 `/tp` 提交与 `/tp self private previous` 相同的命令。`previous` 就是普通的个人命名标记，没有独立的命令、存储字段或特殊查找路径。M 仅在正常游戏输入中生效，不在聊天、编辑面板或对话框中触发。此点随世界中的玩家数据保存，联机时由服务端维护。`/mark` 不授予传送权限。
命名标记也随存档保存：private 保存到各玩家数据（包括服务端离线玩家记录），public 保存到世界的玩家子系统。两类标记相互独立，不跨存档共享；名称不区分大小写，允许 1–64 个字符，含空格时使用引号。联机客户端不接收他人的个人标记，标记写入与传送解析均由服务端执行。个人标记无需额外权限；公共标记写入需要 `game:world.mark.manage`（创造模式默认允许，其他模式按标准权限授予），传送使用现有的 `game:player.teleport.self` 权限与同样的落点加载、安全检查。成功传送只更新个人标记 `previous`，不修改其他个人标记或公共标记。
裸 `/tp` 传送到个人标记 `previous`，尚未记录时返回与其他缺失标记相同的提示。其他格式分别传送调用者或指定玩家。玩家名称不区分大小写，也可以使用玩家 GUID；含空格的名称需要引号。
F6 直接提交该传送命令，不打开命令面板；与 M 一样，仅在窗口激活、玩家存活且没有 HUD 面板或对话框占用输入时生效，仍遵守传送权限和落点安全检查。
`/tp` 一级补全只提供 `self` 和 `player`，按各自的传送权限过滤。self 后选择 `position`、`player`、`private` 或 `public`；player 后先选择被传送玩家，再选择同样的四种目的地。private 始终使用被传送玩家自己的个人标记，public 使用当前世界的公共标记。具有传送他人权限的调用者可在对应 private 路线补全该玩家的标记名称，不向其他客户端广播这些名称。`/tp player A` 是不完整命令，不执行传送；这一分组始终要求 `game:player.teleport.others`，即使 A 是调用者自身也不切换权限语义。旧的隐式玩家、裸坐标及独立 spawn/worldspawn 路线不再支持。
目的玩家候选排除被传送玩家自身，但被传送玩家候选保留调用者自身；名称和 GUID 均可指定玩家。手动指定自己到自己时返回 `teleport.unchanged`，不移动、不取消已有等待请求、不覆盖 previous。self private/public 后补全对应作用域的已有标记，`/mark` 使用相同名称补全便于覆盖。联机命令补全在短暂输入防抖后向服务端查询，服务端按当前连接玩家的权限和作用域生成候选，只回复请求者；候选响应不会执行命令，也不会传输标记坐标，旧输入的迟到响应被忽略。
任何成功传送都会将被传送玩家实际离开的位置覆盖为其返回点，因此连续裸 `/tp` 可以往返切换；异步等待期间不提前覆盖，失败或取消不覆盖。移动方块或地形变化后，返回点仍需接受同样的安全校验。
`game:player.teleport.self` 是标准权限，创造模式玩家默认拥有；其他模式需要授权。
玩家进入 Playing 状态时，服务端在缺失时将已确定的实际出生位置复制到该玩家的 `private spawn`；世界 `public spawn` 保存首次完成该初始化的玩家的实际出生位置，而非可能位于方块内部的粗略世界出生锚点。它们都是普通、可覆盖并随存档保存的坐标标记，不保留特殊解析路径；后续睡觉或重生不会自动更新已有标记，覆盖标记也不会影响真实出生机制。
`game:player.teleport.others` 是标准权限，但不会因创造模式而默认授予。服务端操作员或具备相应授权能力的玩家可使用 `/permission grant "Player Name" game:player.teleport.others` 授予使用权，或使用 `/permission delegate "Player Name" game:player.teleport.others` 同时授予使用及再授权能力。仅持有使用权不能继续授权他人。
grant 候选按使用权授权能力筛选，delegate 候选按再授权能力筛选，两者展示权限各自的说明。
消息面板默认在命令执行后回到消息输入。传送、时间设置/推进、天气和季节设置成功后关闭面板；查询时间、帮助及授权等命令保留消息面板。失败或等待地形加载时不关闭，传送等待实际成功后才关闭。该行为由命令注册声明，本地和联机一致。
认领服务器不会自动获得传送其他玩家的权限。控制台必须指定被传送玩家。

目标玩家必须在线、进入世界且存活，骑乘时需先离开坐骑。坐标是脚底位置，仅支持绝对坐标；
X/Z 范围为 ±1000000，Y 为 0–255，不接受非有限数值。传送前按玩家身体大小检查地形及移动方块的实际碰撞盒，空间不足则拒绝传送。落点区块尚未加载时先异步准备小范围地形，返回 `teleport.loading`（Pending）；加载完成后校验并传送，等待期间玩家留在原地，最多等待 30 秒。
不自动寻找安全落脚点，也不检查坠落、岩浆等危险。传送不改变出生点、朝向或飞行状态；成功后重置速度并同步位置，失败时保持原位置和行动状态。
每个玩家最多有一个等待请求，新传送替换旧请求；目标死亡、离线、骑乘或角色被替换时取消，等待期间也会重新检查调用者权限。完成、失败、取消及世界退出都会释放临时地形加载位置，不写入存档。
远程玩家通过原请求的关联 ID 收到最终结果，本地调用者通过消息系统收到结果。HTTP 调用遇到未加载落点时返回 Pending，不保持 HTTP 连接等待，最终结果记录在服务端日志中。

Headless HTTP 提供 `game:player/teleport/other`，必填 `player`，另选完整的 `x`,`y`,`z` 或 `destination` 玩家名称。
遵守相同权限与执行范围；GUI HTTP 的 ApplicationUser 不具备世界操作权限，不暴露传送入口。

## 数据实例

Starter 首先在程序基础目录注册 `starter:`，读取 `starter:Starter.xml`，再将选中实例的目录注册为游戏使用的 `external:`、`data:` 和 `config:`。实例目录位于：

```text
Instances/<实例名>/
```

因此不同实例拥有独立的设置、身份、世界、模组、缓存和日志。`--instance` 由 Starter 消费，不会写入 `RunningSetting.RemainingArgs`。不存在的命令行实例会自动创建。

## 配置文件

Headless 启动主要涉及三个配置文件。

### RunningSetting.xml

路径：`config:RunningSetting.xml`

`RunningSetting.xml` 只保存启动入口层面的状态，不保存世界名、种子或游戏模式覆盖。

示例：

```xml
<RunningSetting RunMode="HeadlessServer" LogLevel="Information"
                WindowMode="Resizable" WindowWidth="0" WindowHeight="0"
                DefaultSessionId="" PendingSessionId="">
  <RemainingArgs />
</RunningSetting>
```

字段说明：

- `RunMode`: `Gui` 或 `HeadlessServer`
- `LogLevel`: 最低日志级别
- `WindowMode` / `WindowWidth` / `WindowHeight`: GUI 窗口设置；Headless 保留字段但不使用窗口
- `DefaultSessionId`: 没有命令行指定 session 时使用的默认会话 id
- `PendingSessionId`: 重启恢复使用的临时会话 id
- `RemainingArgs`: 启动器未消费、需要保留的参数

### SessionInfo.xml

路径：`config:SessionInfo.xml`

这里保存多个具名或临时启动会话。`StartupManager` 将 `RunningSetting`、本次 `StartupRequest` 与选中的 session 合并成 `StartupContext`；Headless 直接使用其中的有效 session，得到目标世界、种子、游戏模式覆盖和端口。

Headless 使用的会话通常是：

```xml
<Sessions>
  <SessionInfo
    SessionId="..."
    Name="survival"
    Target="World"
    World="World"
    Seed="123456"
    GameMode="Creative"
    ServerHost=""
    ServerPort="0"
    BroadcastPort="0" />
</Sessions>
```

`GameMode` 是可选字段。旧 session 没有该字段时，不会覆盖存档模式。

### ModProfile.xml

路径：

- 全局 profile：`config:ModProfile.xml`
- 世界 profile：`<world>/WorldModProfile.xml`
- 会话 profile：`config:SessionProfiles/<sessionId>.xml`

Headless 启动时会解析当前会话对应的有效模组 profile，并确保缺失的包已下载到本地缓存。解析结果会直接成为本进程的 `CurrentModRuntime.Value.EffectiveProfile`。Headless 不通过“请求重启”来准备模组。

## 世界解析

Headless 启动时：

- 如果 session 指向的世界存在，直接加载该世界
- 如果世界不存在，会按 session 中的 `World` 和 `Seed` 创建
- 如果世界已存在，`Seed` 不再影响该世界
- session 含 `GameMode` 时，新世界以该模式创建；已有世界以该模式运行，但保存仍保留原存档模式
- 如果世界没有开启 `RunServer`，Headless 会自动启用它并保存世界设置

## 内容服务

Headless 与 GUI 使用相同的 `ContentSourceContext` 和统一下载服务。每个实例在 `Settings.xml` 中维护有序的持久内容仓库集合；
启动先按 Profile 的 `ModId + Version + PackageHash` 查询 ContentPackageCache，只有精确包缺失时才按启用仓库顺序查询和下载。
首选来源失败后，只会回退到声明同一 PackageHash 的其他来源。

Profile 不保存仓库地址。未配置可用仓库时，只要所有 requirement 已在本地缓存，Headless 仍可完全离线启动；否则启动会返回
包含缺失精确身份和来源失败的诊断错误。Headless 不持有联机客户端接收的临时仓库作用域。

## 运行时行为

Headless 启动后会：

- 初始化设置、内容和统一包缓存
- 解析启动会话和有效模组 profile
- 下载缺失的 required mods
- 启动服务端侧模组 runtime
- 加载或创建世界
- 启动游戏端口和广播端口
- 运行 20 TPS 左右的主循环
- 退出时保存世界和设置

Headless 进程运行期间不会像 GUI 一样在进入 world 前弹窗请求重启；如果要切换模组组合，需要用新的 session/profile 重新启动进程。

可以通过 `Ctrl+C` 终止进程。
