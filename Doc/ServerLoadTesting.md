# 服务端负载测试

`ServerLoadTool` 是专用协议负载工具，不是新的游戏运行模式，也不是 Agent 客户端。
它复用当前仓库的数据包编码与传输通道，独立建立真实 LiteNetLib 连接，完成 Bootstrap、世界快照确认、玩家创建与进入 Playing。
不启动窗口、音频或完整客户端世界。协议变化时与仓库一起修改，不提供历史协议兼容。

## 边界与安全

- 仅在明确授权的隔离测试服务器上使用。测试会创建玩家、移动玩家、请求生成地形，可能明显占用 CPU、内存与带宽，并留下存档记录。
- 使用无模组的 Creative 世界；工具发送合成的飞行位置快照，没有客户端碰撞、路径规划、战斗或方块操作。
- 不授予管理员权限，不发送管理命令，不修改游戏的 GUI/HeadlessServer 运行模式。
- 负载客户端不是完整真实玩家。结果可用于比较协议、地形分发、兴趣路由与基础玩家同步成本，不能直接声称支持同等数量的完整游戏玩家。
- 若驱动端 CPU、主循环延迟或网络成为瓶颈，不能归因于服务器。较大规模应把驱动端与服务器放在不同机器。
- 连接上限还受世界最大人数限制，包含真实观察客户端和服务端自身。不要把人数上限拒绝当成性能饱和。

## 运行

先按 [Headless](Headless.md) 启动独立实例，例如：

```bash
Survivalcraft.Linux/bin/Debug/net10.0/linux-x64/SurvivalcraftStarter \
  --instance load-server --server --session load --world LoadWorld \
  --game-mode Creative --terrain FlatContinent --terrain-level 64 --seed 1234 \
  --server-port 31987 --broadcast-port 31988 --http-command --http-command-port 31989
```

普通构建与运行：

```bash
dotnet build ServerLoadTool
dotnet run --project ServerLoadTool --no-build -- \
  --host 127.0.0.1 --port 31987 --clients 4 \
  --workload shared --ramp 1 --warmup 10 --duration 30 \
  --visibility 32 --seed 1 --output .agent-work/load-test/artifacts/shared-4
```

输出目录必须为空，不覆盖以前的结果。`--help` 显示参数与边界。

| 参数 | 默认值与含义 |
| --- | --- |
| `--clients` | 1；1–200 个连接，每连接独立身份与 UDP socket |
| `--ramp` | 1；相邻客户端启动间隔（秒），0 为尽快启动 |
| `--join-timeout` | 60；每个客户端进入 Playing 的最长等待时间 |
| `--warmup` | 10；所有客户端进入 Playing 后的预热时间 |
| `--duration` | 30；预热之后的测量时间，不包含加入或尾部收敛阶段 |
| `--workload` | `idle`、`shared`、`explore`，分别为空闲、同区域往返移动、分散直线探索 |
| `--visibility` | 32；内容/兴趣半径，32–128 方块 |
| `--speed` | 4；移动速度，0–16 方块/秒；驱动卡顿时限制单次推进，实际速率可能更低 |
| `--seed` | 1；控制探索方向；新连接身份独立随机生成 |
| `--server-pid` | 可选；采样同机服务器进程的 CPU、工作集和线程数 |
| `--server-http-port` | 可选；采样同机/SSH 转发的诊断入口，需要环境变量 `SCNET_LOAD_TOKEN` |

客户端位置快照频率为 10 Hz，与当前游戏客户端一致；每秒检查兴趣位置，移动超过 8 方块时更新锚点，与 TerrainUpdater 的阈值一致。
地形请求按锚点而不是连续玩家位置生成，使用真实分配规则（区块中心位于内容半径内），
使用当前 allocation generation 与分片协议，丢失分片会重试。不会将分片数量当作完成区块数量。
测量结束后停止移动，最多等待 30 秒让当前区块请求收敛。
水边出生可能生成坐骑，工具通过正常的 DismountRequest 请求下船并等待确认，不修改服务端实体。
采样同时记录发送位置和服务端在线玩家状态中的实际位置；持续明显偏离会判为失败，避免无效移动冒充探索负载。

## 资源与 tick 观测

本机测试推荐同时提供 `--server-pid` 和 `--server-http-port`。HTTP token 从对应实例的 `Config/Settings.xml` 获取后放入环境，
不要把 token 写进命令参数、报告或 Git 文件。该端口必须对应本次被测试的服务器；远程测试可通过 SSH 转发诊断端口。
HTTP 首先发现命令，再使用只读 `game:diagnostics/network/get`，沿用原有 ServerOperator 认证与权限边界。
异步采样不会让客户端主循环等待 HTTP 响应。

网络诊断新增 `HeadlessTicks`：累计 tick 数、累计工作毫秒、超过 50ms 的 tick 数、最大工作耗时、最大起始迟到时间。
这些计数属于整个 Headless 进程；使用测量区间首尾差值计算平均工作耗时和超预算比例。最大值是进程生命周期最大值，不是区间 P95。
CPU 单位为单核百分比，100% 表示一颗逻辑 CPU 的时间，多线程可能超过 100%。没有提供 PID 或诊断入口时对应指标为 null，不推测填补。

## 输出与解读

- `manifest.json`：参数、UTC 起始时间、运行时、系统、CPU 数、工具/游戏 DLL SHA256、服务器快照中的世界名/种子/模式/人数上限与负载边界；不含凭证。
- `events.jsonl`：客户端状态转换与失败原因。
- `samples.jsonl`：各阶段的 Playing 数、流量、区块积压、传输 RTT、驱动与服务端资源、可选网络/tick 计数。
- `summary.json`：成功/失败、测量时间、进入世界时间、区块完成与重试数、P95 RTT/区块传输/驱动循环耗时。

流量是应用层编码帧字节，包含帧长度前缀，不含 UDP/LiteNetLib 协议头、心跳与重传。
`PostWarmup*Bytes` 包含收敛尾段；精确测量区间带宽应使用 `samples.jsonl` 中 measurement 阶段的累计计数差值。
区块传输耗时从首次请求到完整分片重组；不是客户端光照/网格构建时间。RTT 为 LiteNetLib 传输 ping，不是可靠事件延迟。

退出码：0 表示所有指定客户端进入世界并完成测量与尾部区块收敛；1 表示运行失败、断线、超时或取消；2 表示参数/启动错误。
任何拒绝、异常或未完成的区块都不能作为成功基准。退出会关闭所有由工具创建的连接，但不会停止服务器或删除世界。

## 基准方法

分别运行 idle/shared/explore，不混合为一个最大人数。按 1、2、4、8、16 等递增，接近拐点后缩小步长。
固定游戏构建、世界、种子、可视距离、负载速度、预热与测量窗口，并记录已有区块缓存/离线玩家的差别。
比较冷启动应使用新的隔离实例；复用世界会产生缓存优势和离线玩家积累，不能当作等价冷启动。
保留一到两个真实 GUI 客户端观察区块、移动与交互；人数增加导致 tick、积压或 canary 体验恶化时停止增加负载。
报告只针对实际测试负载与硬件，不从小规模测试外推最大容量。
