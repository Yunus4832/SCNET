# 游戏自动化

自动化通过正常输入和游戏规则验证界面与玩法。启动图形程序并启用回环 HTTP 命令宿主，再通过本机接口控制。启用与认证方法见 [HTTP 命令宿主](Server.md#http-命令宿主)。

## 观察与行动

先用 `GET /commands` 获取当前可用命令和参数，再向 `POST /commands` 提交命令与参数。`Completed` 表示本次请求处理完成。持续操作应继续查询对应的状态接口。

| 命令（`game:` 前缀） | 用途 |
| --- | --- |
| `automation/ui/context/get` | 当前 Screen、Dialog、可交互控件的 selector、文本、边界和 actions；库存槽位还包含库存 ID、索引、物品值和数量 |
| `automation/gameplay/context/get` | 本地角色位置、速度、相机、准星目标、区块状态、生命、飞行/浸水/着地、暂停与面板状态、库存 |
| `automation/ui/screenshot` | 保存实际渲染截图（包含已开启的 F3 调试面板），返回资源路径和尺寸 |
| `automation/input/run` | 同时按住键和鼠标按钮，并发送一次相对鼠标移动 |
| `automation/input/status` | 最近的组合输入 ID、状态、累计帧数/秒数与取消原因 |
| `automation/input/cancel` | 松开组合输入所持有的合成键和按钮 |
| `automation/input/text` | 向当前文本输入路径发送文字，不替换控件内容 |
| `automation/ui/key` | 单帧按键，使用同一个组合输入调度器 |
| `automation/ui/tap` | 点击发现的控件中心 |
| `automation/ui/drag` | 从 sourceSelector 拖动到 targetSelector，来源必须支持 drag |
| `automation/ui/scroll` / `swipe` | 对支持相应动作的控件注入滚轮或触摸滑动 |
| `automation/input/mouse/move` | 单帧相对鼠标移动 |
| `automation/navigation/start` / `status` / `cancel` | 启动、观察和停止步行导航 |
| `automation/view/look_at` / `angles` / `status` / `cancel` | 朝向坐标、绝对或相对角度控制、查询和停止视角控制 |

Gameplay 上下文的 `Runtime` 观测提供两侧运行时 ID、侧别、世界是否已加载、本地端口和被观测角色的位置，
以及 `DistinctProjects`，用于检查本地双世界和权威位置收敛；它不修改运行时或角色状态。

Gameplay 库存观测在创造模式中仅返回玩家存储区域，避免把整个创造目录重复发送；
`TotalSlotsCount` 表示底层索引空间大小。用 UI 槽位的 `Slot` 与 `Text` 选择当前可见物品，
不要把创造目录的无限数量当作拾取数量。
槽位索引只在所属库存内有意义，使用 `InventoryId` 和 `Index` 共同识别槽位；
未绑定库存的占位控件不会作为可操作槽位返回。

UI 拖放使用真实鼠标输入，`durationSeconds` 可选，默认为 0.5，范围为 0.1–3 秒。
返回的 `Id` 与组合输入共用状态查询；等待输入完成后，仍需观察目标槽位，确认物品实际转移。

列表中当前可见且中心位于列表范围内的可选行以 `Item[index]` 目标提供 `tap`。索引属于当前列表，刷新后必须重新发现目标；点击经正常输入处理，不直接修改选中项。

## 组合输入

```json
{"identity":"game:automation/input/run","arguments":{"keys":["W"],"durationSeconds":1}}
```

`keys` 与 `mouseButtons` 是可选的枚举名称数组；`mouseDeltaX/Y` 是可选的整数相对位移。
`durationSeconds` 范围为大于 0 至 15，设置后优先于 `durationFrames`；后者为 1–600，默认 1。
角色移动应优先按秒指定时长，帧数会随帧率改变实际持续时间。相对鼠标位移仅在开始时发送一次。

同一时刻只运行一个组合输入。请求成功后轮询 status，再重新观察角色、目标或库存。
窗口失活、界面切换、角色切换或超过动作期限会取消输入；释放合成键不会松开仍被物理设备按住的键。

## 自动导航

```json
{"identity":"game:automation/navigation/start","arguments":{"x":10,"y":65,"z":20,"range":0.75,"timeoutSeconds":30}}
```

坐标是角色脚部的世界坐标。单段距离最多 128 格，range 为 0.5–4，超时为 1–120 秒。
目标区块必须已经加载。当前导航面向未骑乘、未飞行的本地步行角色；不自动挖路、生成地形或瞬移。

导航复用动物的地形寻路服务，生成正常玩家运动指令，仍使用原有物理和联机路径。
它与普通输入共享一个运动执行系统，不复制玩家身份、库存或存档。
手动移动、转向、跳跃、挖掘、使用，以及新的组合输入，会停止导航；死亡、睡眠、面板或角色变化也会停止。
查询状态区分 searching、walking、arrived、failed 与 cancelled，失败原因包括无路径、部分路径、卡住和超时。
到达仍应结合实际位置确认，不将搜索完成误认为角色已经到达。
状态中的 `StartPosition` 是此次搜索的实际起点，`NextWaypoint` 是当前待跟随的路径点，
没有已完成的有效路径时为 null。结合实际位置、路径点和搜索代价判断绕行，不仅比较最终坐标。

## 可重复的世界

创建测试世界可指定 `--session NAME --world NAME --seed SEED --game-mode Creative
--generator game:flat --generator-param terrainHeight=64 --player PLAYER_NAME`。
平坦地形仍有海岸和水域：固定种子不保证出生点周围都是可步行陆地，操作前检查着地、浸水与截图。
地形参数只用于创建，不改变已有世界；命名会话保存规则见 [启动会话](StartupSessions.md)。

## 视角控制

使用 `view/look_at` 可让第一人称相机朝向指定的世界坐标：

```json
{"identity":"game:automation/view/look_at","arguments":{"x":10.5,"y":65.5,"z":20.5}}
```

目标是世界坐标，方块中心通常使用整数坐标加 0.5。这是对准方向，不会自动走近目标、
穿透遮挡或保证交互距离；对准后仍通过 gameplay/context/get 的准星目标确认实际命中。

`view/angles` 接受 `yawDegrees`、`pitchDegrees` 和可选的 `relative`（默认 false）。
Yaw 0 朝向 +Z，90 朝向 +X，180 朝向 -Z，-90 朝向 -X；Pitch 正值向上，范围 -82–82。
relative=true 表示相对于提交时的实际相机方向，并选择最短转向路径，不用于整圈旋转动画。

```json
{"identity":"game:automation/view/angles","arguments":{"yawDegrees":90,"pitchDegrees":0,"relative":true}}
```

两种命令都接受 `toleranceDegrees`（默认 1，范围 0.1–5）与 `timeoutSeconds`（默认 10，范围 1–30）。
容差是观察方向与目标方向的夹角，不是每个欧拉角分别的容差。调用后按返回 Id 查询 view/status，
Status 为 turning、aligned、failed 或 cancelled，ErrorDegrees 给出最新夹角误差。

控制仅用于准备就绪的 FppCamera，不直接修改相机矩阵或角色角度，仍通过正常 Look 输入逐步转向。
gameplay/context/get 返回 CameraType、YawDegrees、PitchDegrees；出生过场还未结束时应等待 FppCamera。
视角控制、导航和组合输入互斥；新的键鼠动作、手动移动/转向/交互、相机变化、面板、死亡或睡眠
会取消视角控制。其他相机可用原始鼠标输入转向，按 V 切换相机。
