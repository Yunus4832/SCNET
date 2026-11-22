# 网络稳定性与容量测试

按要回答的问题选择工具：

| 问题 | 工具 |
|---|---|
| 能否建立传输连接，直连还是经中继 | [NetworkProbe](../Tools/NetworkProbe/README.md) |
| 实际游戏在延迟、丢包或短断网后能否恢复 | NetworkDamageTool 与隔离的 GUI、服务器实例 |
| 同一工作负载随连接数增长的资源变化 | [ServerLoadTool](ServerLoadTesting.md) |

## 游戏弱网测试

NetworkDamageTool 在本机转发 UDP，按种子生成延迟、抖动和丢包，并可限制带宽。测试先运行同场景无损基线，
再记录进入游戏的时限、双向实际丢包与恢复后的应用状态。将启动失败与进入游戏后的同步失败分别记录。

```text
GUI Client -> NetworkDamageTool -> 独立服务器 Server
```

基本用法：

```bash
dotnet run --project Tools/NetworkDamageTool -- run \
  --listen 127.0.0.1:28989 \
  --target 127.0.0.1:28987 \
  --seed 12345 \
  --up-latency-ms 120 --up-jitter-ms 50 --up-loss 0.05 \
  --down-latency-ms 120 --down-jitter-ms 50 --down-loss 0.05 \
  --down-bandwidth-kbps 1500 \
  --events artifacts/network-run/proxy-events.jsonl
```

客户端连接代理监听端口。多客户端测试为每个客户端启动一个代理。代理绑定首次发送方 IP，
同一客户端的查询和正式连接可使用多个源端口，各自保留独立回包路由。每个方向共用损伤与带宽预算，
最多保留 64 条路由。客户端重启后，可重启对应代理清空路由。代理保留 UDP 数据报边界。

短断网使用 `--up-outage-start-seconds 30 --up-outage-duration-seconds 5`，下行使用同名的 `down`
选项。时间从代理启动计算，断网窗口含起点、不含终点。计划发送时间落在窗口内的延迟包也会丢弃。
恢复后继续使用原路由和常规损伤配置。

定向服务器探测在原超时窗口内每 250 ms 重发请求，收到响应、取消或超时即停止；
LAN 广播发现不使用该重试。探测等待时长可能包含丢包重试，列表中的估计 Ping 不能替代
已建立连接的往返延迟指标。启动失败必须与已进入 Game 后的同步失败分别记录。

安全握手、原生连接和游戏准入分别计时。[DTLS 期限定义](../Engine.Network/Security/SecureTlsPeer.cs)
为握手保留 5 秒，[外层等待与未就绪清理](../Engine.Network/Security/SecurePacketLayer.cs)保留 6 秒；
未挂载安全会话的寿命还覆盖原生请求期限和 1 秒收尾，默认合计 12 秒。加载页在传输任务结束后开始原有的 10 秒准入后备计时；服务端给出的
`AdmissionDeadline` 在传输等待期间仍然有效。测试同时检查取消和停机能及时释放握手资源，以及
连接完成后准入期限仍会到期。会话容量、身份校验和重传算法沿用现有约束。

每个方向支持以下选项：

- `--up/down-latency-ms`：固定单向延迟；
- `--up/down-jitter-ms`：均匀分布的正负抖动；
- `--up/down-loss`：0 到 1 的丢包概率；
- `--up/down-bandwidth-kbps`：串行化带宽，0 表示不限速。

`--seed` 使随机决策可重复，`--events` 每秒写入累计的双向数据报、字节、转发和丢弃计数。
当前版本使用整个进程生命周期内的固定损伤配置，除预设的每方向单次断网窗口外，不支持运行时切换配置或任意多阶段断流脚本。

## 判断地形是否恢复

- **缺块**：停止移动后检查视野内区块是否最终就绪。超过重试时限后，已有部分分片的区块会请求缺片，完全未收到分片时才重请完整区块。分别比较补发字节与完整请求次数。
- **方块变更**：检查最终方块值和内容版本。增量只有与本地基线相符时才应用，版本断层会请求完整快照。替换完成前保留已经安装的内容和几何。

协议与版本规则见[地形分发](TerrainDistribution.md)。

## 容量测试

容量测试不能用少量客户端线性外推。仓库中的 `ServerLoadTool` 使用真实连接、Bootstrap 和包协议
逐步建立轻量负载客户端，并可配合真实 GUI 客户端观察体验。工具边界、指标语义和使用方式见
[服务端负载测试](ServerLoadTesting.md)。测试结果只代表实际使用的工作负载、构建和硬件，不能直接
换算为完整玩家的最大并发数。
