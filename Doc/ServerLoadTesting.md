# 服务端负载测试

`ServerLoadTool` 使用轻量客户端建立真实传输连接，完成 Bootstrap、世界快照确认和角色加入。
用它比较空闲、同区域移动和分散探索下的协议、地形与兴趣路由成本。

## 测试条件

- 在授权的隔离服务器上启动无模组 Creative 世界。工具会创建玩家、移动角色并触发区块生成，测试世界会留下这些数据。
- 负载为合成位置快照。需要评价输入、碰撞、战斗、渲染或玩家体验时，另用真实 GUI 客户端观察。
- 将世界人数限制、观察客户端占用的名额与实际性能拐点分别记录。工具接受最多 200 个连接，实际准入由世界设置决定。
- 同时观察驱动端 CPU、循环延迟和网络。较大规模测试可将驱动与服务器分配到不同机器。

## 运行

先按 [独立服务器](Server.md) 启动独立实例，例如：

```bash
Survivalcraft.Server/bin/Debug/net10.0/linux-x64/Survivalcraft.Server \
  --instance load-server --session load --world LoadWorld \
  --game-mode Creative --generator game:flat --generator-param terrainHeight=64 --seed 1234 \
  --server-port 31987 --broadcast-port 31988 --http-command --http-command-port 31989
```

普通构建与运行：

```bash
dotnet build Tools/ServerLoadTool
dotnet run --project Tools/ServerLoadTool --no-build -- \
  --host 127.0.0.1 --port 31987 --clients 4 \
  --workload shared --ramp 1 --warmup 10 --duration 30 \
  --visibility 32 --seed 1 --output .agent-work/load-test/artifacts/shared-4
```

输出目录必须为空，不覆盖以前的结果。`--help` 显示参数与边界。

| 参数 | 默认值与含义 |
| --- | --- |
| `--host` / `--port` | 默认 `127.0.0.1` / `28887`，填本次测试服务器地址与游戏端口。 |
| `--output` | 必填，目录须为空或尚不存在。 |
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

客户端位置快照频率为 10 Hz，与当前游戏客户端一致；每秒检查兴趣位置，移动超过 8 方块时更新锚点，与游戏客户端兴趣位置上报的阈值一致。该阈值位于发送边界；服务端需求管理仍接收已批准的位置修正。
地形请求按锚点而不是连续玩家位置生成，使用真实分配规则（区块中心位于内容半径内），
使用当前 allocation generation 与分片协议，丢失分片会重试。不会将分片数量当作完成区块数量。
测量结束后停止移动，最多等待 30 秒让当前区块请求收敛。
水边出生可能生成坐骑，工具通过正常的 DismountRequest 请求下船并等待确认，不修改服务端实体。
采样同时记录发送位置和服务端在线玩家状态中的实际位置；持续明显偏离会判为失败，避免无效移动冒充探索负载。

## 资源与 tick 观测

本机测试推荐同时提供 `--server-pid` 和 `--server-http-port`。HTTP token 从对应实例的 `Config/Settings.xml` 获取后放入环境，
不要把 token 写进命令参数、报告或 Git 文件。该端口必须对应本次被测试的服务器；远程测试可通过 SSH 转发诊断端口。
HTTP 首先发现命令，再使用只读 `game:diagnostics/network/get`，沿用原有 LocalOperator 认证与权限边界。
异步采样不会让客户端主循环等待 HTTP 响应。

`ServerTicks` 提供累计 tick 数、累计工作毫秒、超过 50ms 的 tick 数、最大工作耗时、最大起始迟到时间。
`ReliableQueues` 按在线客户端记录 `Path`（Direct、HolePunch、Relay）和 `PendingBytes`，用于区分实际连接路径与可靠发送积压。
这些计数属于整个独立服务器进程。用测量区间的计数差值计算平均工作耗时和超预算比例，最大值覆盖进程生命周期。
CPU 单位为单核百分比，100% 表示一颗逻辑 CPU 的时间，多线程可能超过 100%。没有提供 PID 或诊断入口时对应指标为 null，不推测填补。
网络诊断中的 `Terrain` 区分服务端待处理的完整区块请求、尚未就绪的内容及未完成编码数，
同时提供已分配区块的工作线程状态分布和存储队列大小。待处理数不含补片请求，
工作线程状态是采样时的近似观测；不要将客户端待完成数直接解释为网络发送队列长度。
完整 GUI 客户端的同一诊断入口还返回 `ClientTerrain`：分片重组积压、收片/完成/补片请求累计数，
以及逐区块的可视范围标记、内容加载和可绘制状态、线程状态及内容/几何版本。
它用于核对真实客户端的内容和几何收敛，薄协议负载工具没有客户端渲染世界，不提供这些指标。
`PendingLocationCount`、累计 `LocationHandoffTimeouts` 和 `DeferredAllocationPasses` 分别帮助定位
位置变更等待、后台令牌交接超时及存储背压导致的分配延后。累计值须使用测量区间差值。

## 输出与解读

- `manifest.json`：参数、UTC 起始时间、运行时、系统、CPU 数、工具/协议 DLL SHA256、服务器快照中的世界名/种子/模式/人数上限与负载边界；不含凭证。
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
