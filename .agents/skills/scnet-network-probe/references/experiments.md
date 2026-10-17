# Probe experiment recipes

Run from the repository root. These scripts are owned by this skill and prefer Linux.
Use a fresh `.agent-work/network-probe/artifacts/<run-id>/` output directory for each run.

## 自动管理完整本机场景

在 POSIX 主机上，Python 3 标准库脚本可管理临时信令服务和两端探针：

```bash
python3 .agents/skills/scnet-network-probe/scripts/local-lab.py --output .agent-work/network-probe/artifacts/local-test
python3 .agents/skills/scnet-network-probe/scripts/local-lab.py --mode scenario-relay --output .agent-work/network-probe/artifacts/relay-test
```

先按 [探针说明](../../../../NetworkProbe/README.md#测试与探针入口) 构建 NetworkProbe 和 RendezvousServer。脚本不构建、不下载运行时；需要 Python 3 和 .NET 10。
默认 TCP 控制端口 31991、UDP 信令端口 31990、中继 UDP 32000–32003，可用
`--http-port`、`--udp-port`、`--relay-port` 修改。控制入口绑定 loopback，UDP 广告地址使用 loopback；UDP socket 沿用信令服务的所有接口绑定。
`--probe` 和 `--server` 可指定已有程序集。输出目录必须尚不存在。
`lab-manifest.json` 保存配置和两份入口程序集哈希，`lab-summary.json` 保存退出结果；服务与探针日志及两端结构化结果也保留。
服务启动失败不启动探针；正常结束、超时、Ctrl+C 或 SIGTERM 后回收本轮创建的进程组。
原生运行时可用 `--mode scenario-lease --cancel-after-path`：等到准备路径确认后，仅终止父探针，要求父进程通过私有通道正常取消两端，并检查最终资源归零。该选项不等待完整的 70 秒租约场景，也不支持 Podman 模式。

## 隔离地址族验证

Linux 的 `local-lab.sh` 先创建独立网络命名空间，再启动上述场景；需要 `unshare`、`ip` 和 `nft`：

```bash
bash .agents/skills/scnet-network-probe/scripts/local-lab.sh --family ipv4 --output .agent-work/network-probe/artifacts/ipv4
bash .agents/skills/scnet-network-probe/scripts/local-lab.sh --family ipv6 --mode scenario-relay --output .agent-work/network-probe/artifacts/ipv6-relay
bash .agents/skills/scnet-network-probe/scripts/local-lab.sh --family dual --output .agent-work/network-probe/artifacts/dual
```

IPv4/IPv6 模式阻断另一地址族的输出，并用独立报文及 nft 计数器校准；
双栈模式允许两族，控制地址使用 `localhost`，UDP 使用 IPv6。它们验证隔离 loopback 下的地址族处理，不能作为公网 IPv6 或跨网打洞证据。
输出额外保存接口和阻断规则。直接运行 Python 的 `--family` 只选择地址，不执行隔离；`--isolated` 拒绝在原宿主命名空间中运行。
没有本机 .NET 时，可显式传入 `--runtime-image` 使用缓存的 Podman 镜像，并用 `--probe`、`--server` 指定程序集。
容器复用实验命名空间，结束时只删除本轮容器；脚本不拉取镜像。容器模式应在 root 专用测试环境使用。

## 隔离双 NAT 实验室

脚本在新的 network namespace 内创建模拟公网、两台独立 NAT 路由器和两个不同 LAN。
普通场景的 UDP 使用显式固定端口 SNAT，入站过滤采用 conntrack 来源元组；先校准不同目标观察到的映射一致，
并确认未联系的来源不能入站，再运行正式 Engine socket 的协商和可靠回显。默认 MASQUERADE 的行为
可能因内核不同而变化，不能直接当作端点无关映射。

需要 Python 3、`unshare`、`nsenter`、`ip`、`nft`、`sysctl`、`openssl`、`tcpdump`，以及内核 veth/netfilter 支持。
已编译的 NetworkProbe、RendezvousServer 可在安装 .NET 的主机直接运行；也可显式指定已缓存的 Podman
运行时镜像。脚本不拉取镜像。root 启动新的 network namespace，普通用户另外使用 user namespace；
脚本检查命名空间与调用者不同，拒绝直接更改宿主网络。容器模式应在 root 的专用测试环境使用。

```bash
bash .agents/skills/scnet-network-probe/scripts/nat-lab.sh --case punch \
  --output .agent-work/network-probe/artifacts/network/nat-punch
bash .agents/skills/scnet-network-probe/scripts/nat-lab.sh --case peer-blocked \
  --output .agent-work/network-probe/artifacts/network/nat-relay
bash .agents/skills/scnet-network-probe/scripts/nat-lab.sh --case udp-blocked \
  --output .agent-work/network-probe/artifacts/network/nat-blocked
bash .agents/skills/scnet-network-probe/scripts/nat-lab.sh --case endpoint-dependent \
  --output .agent-work/network-probe/artifacts/network/nat-dependent
bash .agents/skills/scnet-network-probe/scripts/nat-lab.sh --case double-nat \
  --output .agent-work/network-probe/artifacts/network/nat-double
bash .agents/skills/scnet-network-probe/scripts/nat-lab.sh --case hairpin \
  --output .agent-work/network-probe/artifacts/network/nat-hairpin
```

`punch` 要求 HolePunch 与可靠回显；`peer-blocked` 阻断对端直达、保留信令和中继，要求 Relay 与回显；
`udp-blocked` 阻断客户端全部 UDP，要求总体准备期限内返回 Timeout。每次在新拓扑中运行一个场景，输出目录不得已存在。
`endpoint-dependent` 使用受控目标相关映射：信令目标映射到 50000，其他目标映射到 50001；先校准同一 socket 的不同目标映射不一致及未联系来源被阻挡，再要求回退 Relay 并完成可靠回显。
`double-nat` 为每端增加独立内层路由器，UDP 先映射到内层 45000、再映射到外层 50000。校准最终映射与过滤，并保存内外层 UDP 包头和路由规则，要求 HolePunch 与可靠回显。
`hairpin` 将两个 LAN 接到同一 NAT，使用固定本地端口 41000 和公网映射 50000/50001，通过显式 DNAT/SNAT 模拟回环。直接跨 LAN 通信被过滤，只允许经公网映射返回；保存两个 LAN 的包头，要求 HolePunch 与可靠回显。
`hairpin-blocked` 使用相同拓扑但拒绝公网映射回环，默认要求 Relay 与回显。
`hairpin-local` 同样拒绝公网回环，但允许跨 LAN 路由；禁用中继时仍要求 HolePunch 与回显，用于验证私网候选。该拓扑保留源 NAT，包头可同时出现私网与转换后的源地址。
`--relay-policy Disabled|Allowed|Required` 选择路径策略，默认 Allowed。Disabled 在对端被阻断或目标相关映射场景要求 HolePunchUnavailable；Required 在 UDP 可用时要求 Relay。
`beacon-blocked` 只阻断客户端 UDP 信令端口，保留控制 HTTP 和中继：Allowed/Required 要求 Relay，Disabled 要求 UdpUnavailable。
`relay-blocked` 阻断两端到中继池的 UDP：Allowed/Disabled 要求 HolePunch，Required 要求 RelayUnavailable。
`address-filter` 使用有期限的已联系地址集合，允许该地址从其他端口发回报文，但拒绝未联系地址；显式固定映射 50000 到本地 41000。普通 `punch` 的 conntrack 配置则按地址和端口过滤。独立校准探针验证两者对“同地址换端口”的不同结果。
`public-endpoints` 把两端直接接入模拟公网，校准无 NAT、无入站过滤及本地端口与观察端口一致，再完成邀请码候选路径与可靠回显。这是隔离拓扑中的无 NAT 对照，不是实际公网可达性证明。
每端同时保存阶段与周期采样，成功或按预期失败后均检查最终宿主停止、资源计数归零。失败场景通过终止信号取消仍在等待的房主；POSIX 探针处理 SIGTERM，Ctrl+C 也走取消和关闭流程。

远端机器没有 .NET 时，将普通 Debug 构建目录复制过去，然后使用：

```bash
bash .agents/skills/scnet-network-probe/scripts/nat-lab.sh --case punch --probe-dir /path/to/probe --server-dir /path/to/signal \
  --runtime-image mcr.microsoft.com/dotnet/sdk:10.0 \
  --output /path/to/new-results
```

TLS 证书在实验中临时生成，只通过子进程环境或只读容器挂载信任，不修改系统信任库。
私钥保存在输出目录之外并在结束时删除；输出仅保留公开证书、路由器规则、校准结果、
UDP 包头（无 payload）、进程日志、构建哈希及 `summary.json`。失败也清理本次创建的容器与命名空间进程。

这些场景分别覆盖明确配置的 IPv4 映射与过滤，不能代表所有 NAT 行为、真实运营商或完整游戏体验。
每次结果以该轮校准与保存的证据为准，不将既往通过当作当前构建验证。

### 双栈候选与单族信标故障

`local-lab.py --family dual` 以 `localhost` 公告 UDP 信标，房主和加入端解析每种地址族的一个端点。
加入端共享一次加入登记和票据，并行验证候选；选定成功路径后取消并等待其他尝试清理。
只有全部直连候选失败后，Allowed 策略才申请中继；Required 策略直接使用中继。

在隔离 Linux namespace 中，`local-lab.sh --family dual --blocked-beacon-family ipv4`（或 `ipv6`）
仅丢弃指定地址族发往信标端口的 UDP，保留 HTTP 和另一地址族。脚本保存过滤规则并校准丢弃计数。
该选项要求隔离双栈实验，不能修改宿主网络，也不能用于推断公网 IPv6 可达性。

`--beacon-block-hook output` 可将同一丢弃规则放在输出链，以验证本地发送拒绝；默认 input 用于静默丢包。驱动器保留就绪前日志并等待明确的 READY 标记，不把第一行日志当作就绪信号。

`scenario-candidate-lease` 与 `scenario-lease` 都在路径确认后等待 70 秒并维护准备租约；
前者使用 Allowed 策略选择直连候选，后者强制中继。
`local-lab.py --family dual --mode scenario-candidate-lease --cancel-after-path`
可在双栈候选确认后取消父探针，并验证两端正常清理；使用原生运行时，无需等待完整租约时长。

### 链路 MTU 与分片恢复

`nat-lab.sh --link-mtu 576 --recovery` 在隔离 IPv4 拓扑的所有非 loopback 接口设置指定 MTU，
保存实际接口快照，并发送 80 条有序的 1024 字节可靠数据，要求回显、回执及队列最终收敛。
默认 MTU 为 1500，允许范围 576–9000；对照应使用相同构建和策略。
加入 `--relay-policy Required` 可验证带认证封装的中继路径。
这属于传输层验收，不能代替游戏地形收敛测试；链路 MTU 也不等同于应用载荷上限。

### 固定种子的突发丢包

`nat-lab.sh --case punch --recovery --burst-loss --seed 20261012` 在两台实验路由器的 WAN 出口
配置 `tc netem loss gemodel 2% 25% 100% 0%`，使用同一指定种子，保留配置及丢包统计。
该模型允许连续丢包；它不保证不同调度下逐包损伤完全相同。脚本要求两侧实际丢包计数大于零。
使用相同构建、MTU、策略和恢复负载运行无损对照；中继对照加 `--relay-policy Required`。
环境需要 `tc` 和内核 `sch_netem` 支持；模块未加载时由实验环境管理员配置，脚本不修改宿主模块。
规则仅在隔离 namespace 内创建，不改变宿主默认路由或 qdisc。

连接采样与游戏网络诊断同时记录 `Path` 和 `AddressFamily`（`InterNetwork` / `InterNetworkV6`）。直连或打洞状态本身不能证明使用了 IPv6；公网测试应同时检查地址族与实际游戏就绪状态。
