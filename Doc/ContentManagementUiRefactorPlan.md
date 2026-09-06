# 内容管理 UI 重构临时实施计划

> 状态：临时执行计划。作为 [内容管理迁移临时实施计划](./ContentManagementMigrationPlan.md) 的补充，
> 约束多内容仓库、在线目录、版本获取、本地包管理和 ModProfile UI 的最终职责边界。
> 两份临时计划必须全部完成、正式文档更新并经人工验收后，才可以一并删除及移除 README 入口。

## 目标

在上一期已经建立的统一 `.scpkg`、`ContentPackageCache`、安装器和启动补全流程之上，完成游戏端内容 UI 的解耦：

```text
内容仓库配置 ─> ClientFactory/ClientPool ─> 多仓库目录聚合 ─> 内容与版本选择
                                │                              │
                                └────> 统一下载入口 ───────────┴─> ContentPackageCache
                                                        │
                         ┌──────────────────────────────┘
                         ├─> Mod 管理 ─> 编辑 Profile
                         ├─> 本地内容包 ─> 导入/导出/移除/非 Mod 安装
                         └─> 各资产管理 ─> 管理安装后产生的本地资产

ModProfile 启动 ─> 本地缓存优先 ─> 缺失时查询候选仓库 ─> 下载到缓存 ─> 启动 Runtime
```

本计划主要处理 UI、仓库来源和版本管理，不重新定义上一期已经固化的内容包协议、PackageHash 算法、缓存原子性、
非 Mod 安装语义或 FilePicker 平台边界；但会明确调整联机握手协议中的精确 Mod 要求和临时候选仓库结构。
发生冲突时，先修订两份计划使其一致，再实施代码。

## 已确认的设计约束

以下内容视为本期实施不变量：

1. ContentServer 在客户端语义中是只读内容仓库源，负责目录查询和包下载，不负责本地内容、Profile 或安装资产管理。
2. 客户端允许配置多个内容仓库；仓库具有客户端生成的稳定 ID、显示名称、基础地址、启用状态和查询优先级。
3. 包身份仍是 `ContentType + Identifier + Version + PackageHash`。仓库 ID、仓库 URL 和服务端 `ContentId` 不参与包身份。
4. 相同 `Identifier + Version + PackageHash` 的多仓库结果合并为一个版本及多个可用来源；相同
   `Identifier + Version` 出现不同 PackageHash 时必须显示冲突，不允许后查询结果静默覆盖先查询结果。
5. 所有手动远程浏览和下载只出现在统一在线内容流程。Mod 管理 Screen 不持有 ContentServer 客户端、仓库地址输入框、
   远程目录刷新或直接下载实现。
6. 在线内容主目录按 `ContentType + Identifier` 聚合，不为每个版本平铺顶级条目；版本详情完整列出可获取版本。
7. 默认突出最新版本、Profile 引用版本、本地缓存版本和当前会话要求版本；其他历史版本可通过“全部版本”查看，不能永久隐藏。
8. 下载只把经验证原包写入 ContentPackageCache，不修改 ModProfile。Mod 启用只由 Mod 管理 Screen 修改 Profile。
9. Mod 管理 Screen 可以为缺失的 Profile 条目提供“在仓库中查找”，但该操作只能导航到带精确筛选条件的统一在线内容流程。
10. ModProfile 启动补全是独立的非 UI 流程：本地和联机 Profile 都以精确 PackageHash 先查缓存，缺失时才查询候选仓库，
    下载后再从本地缓存启动。
11. 用户本地 `ModProfile`、解析后的运行期 Profile 和联机握手中的 `RequiredModProfile` 都必须固定
    `ModId + Version + PackageHash`，且不绑定包最初下载自哪个仓库。项目未发布，不保留缺失 hash、单一
    `ModProfile.ContentServerUrl` 的兼容字段或双解析路径。
12. 联机服务器声明的会话内容源是当前连接准备阶段的临时候选源，不自动写入玩家的持久仓库列表。精确 PackageHash
    仍是远程补全的校验依据。
13. 非 Mod 包下载后可以从统一流程发起安装；安装后的世界、材质、皮肤和家具继续由各自资产管理 Screen 管理，
    不在在线目录或缓存页面建立来源安装关系。
14. 本地缓存中的包不依赖仓库继续存在。匹配精确 PackageHash 时，不得为了确认来源而访问网络。
15. Screen 不直接构造多仓库查询、冲突合并或下载策略；这些规则属于可单元测试的共享服务。
16. `ContentServerClientFactory` 负责根据仓库描述创建匿名客户端；`ContentServerClientPool` 按仓库作用域和稳定 ID 保存并复用客户端，
    集中处理配置刷新、客户端生命周期和可用仓库快照，调用方不自行缓存客户端。
17. 所有下载调用统一进入一个应用服务。调用方可以用稳定来源 ID 显式限定单个来源；未指定来源时，服务按默认策略从
    ClientPool 选择候选仓库，并在单个仓库失败后自动尝试其他声明同一 PackageHash 的仓库。
18. Profile 解析器向下载服务提交精确包身份，不提交仓库 ID、URL、仓库数量或重试策略；多仓库选择与故障转移完全封装在
    统一下载入口内部。下载服务永远不按 `ModId + Version` 猜测制品。
19. 公开和私有部署的 ContentServer 对客户端都匿名提供目录、精确查询和包下载；“私有”只表示部署范围或入口不公开，
    不引入客户端认证协议。联机服务器可以把客户端可达的私有仓库描述作为临时来源下发。
20. GUI 必须先通过统一下载服务完成缺失包准备，再创建 pending session 或重启；临时仓库失败时允许持久仓库回退。
    缓存已齐备时无需访问任何仓库；仅在全部候选来源失败且仍有缺失包时阻止重启。
21. 本期继续由本地持久化、运行期和联机协议复用同一个 `ModProfile`/`ModPackageRequirement` 类型，不提前拆分协议 DTO。
    共享类型统一约束 PackageHash 非空且格式规范；服务端发送前和客户端接受时还必须拒绝重复冲突或与实际 Runtime
    不一致的 requirement。协议模型的进一步分层留给后续整体网络协议重构。
22. 只有 Mod 进入联机预备要求：Mod 原包无需安装，由 GameModRuntime 直接从 ContentPackageCache 动态加载。World、
    BlocksTexture、CharacterSkin 和 FurniturePack 下载后必须显式安装为脱离来源包的本地资产，因此不进入 RequiredModProfile、
    不自动安装，也不因加入服务器而改变客户端资产配置。动态家具属于世界状态同步，同样不属于内容仓库要求。
23. 联机下发的 requirement 和临时仓库都是不可信网络输入，必须限制集合数量、字符串长度、URI scheme、重复项、重定向和响应大小；
    验证失败不得触发网络访问、缓存写入、session 创建或重启。
24. `Content.Packaging` 继续只定义与传输无关的 `.scpkg` 制品协议，包括 manifest、PackageHash、reader/writer 和各类型 payload 校验。
    ContentServer HTTP 请求/响应模型继续归属现有服务端与客户端边界，联机 Package 继续归属现有网络代码；本期不为二者新建、
    搬迁或拆分协议项目，公共协议项目和依赖层次留给后续整体协议重构。

## 当前实现基线与偏差

以下基线描述本期开始时的实现。工作区已有仓库配置、Factory/Pool 及测试的未提交实现草稿，
尚未完成应用接入和阶段门禁，不据此勾选完成项；本次计划提交不包含这些代码。

已经具备的基础：

- `ContentPackageCache` 按 PackageHash 保存所有类型的不可变原包，并支持查找、导入、导出、删除和损坏隔离。
- `ContentServerClient.DownloadToCacheAsync` 和 Mod 下载在命中 PackageHash 时可以直接复用本地缓存。
- `ModPackageRequirement` 可以记录 PackageHash；GUI 与 Headless 共用本地优先、远程补缺的启动解析器。
- ContentServer 已提供公共内容目录、Mod 精确版本查询、版本历史和按 PackageHash 下载能力。
- 在线内容、内容包、Mod 管理和安装后资产已经有独立 Screen 或 Manager 边界。

本期需要纠正的偏差：

- `Settings.ContentServerUrl` 和 `ModProfile.ContentServerUrl` 仍只能表达一个仓库。
- `ContentServerScreen` 只读取单一默认地址，主列表直接展示服务端返回的当前版本，没有跨仓库聚合和版本详情。
- `ModManagementScreen` 仍包含仓库地址输入、保存默认地址、远程刷新和下载操作，与上一期 UI 职责约束冲突。
- `ModManagementScreen` 按 `ModId + Version` 合并本地和远程条目，只保存一个 URL；多仓库下会覆盖来源，且无法正确表达同版本 hash 冲突。
- `ModProfileResolver` 只创建一个 ContentServer 客户端；联机信息也只传递一个 `ContentServerUrl`。
- `ServerInfoPackage` 已通过共享 `ModProfile` 读写 requirement 的 PackageHash，但当前共享类型仍允许空值，序列化会把空值
  写为空字符串且读取时不验证；需要把非空规范 hash 提升为该共享模型的统一约束。
- `ModProfileManager` 当前不会把 PackageHash 写入用户 Profile 文件，与多仓库下精确制品选择和自动补全要求冲突。
- Screen 直接创建 `ContentServerClient` 和 `LocalModRepository`，尚无统一 Factory、Pool、目录聚合、下载与状态映射应用服务。
- 版本分页按钮尚未连接实际分页或版本历史，旧版本没有明确的信息架构。

## 目标信息架构

内容主页面提供以下职责明确的入口：

| 入口 | 职责 | 明确不负责 |
|---|---|---|
| 在线内容 | 聚合仓库目录、搜索筛选、查看版本、下载到缓存、下载后发起非 Mod 安装 | Profile 编辑、安装后资产管理 |
| 内容仓库 | 添加、编辑、删除、启用、排序和测试仓库 | 浏览包、下载包 |
| 本地内容包 | 展示缓存、FilePicker 导入/导出、从缓存移除、发起非 Mod 安装 | 远程目录查询、Profile 编辑 |
| Mod 管理 | 展示 Profile 与本地 Mod 状态，编辑全局/世界 Profile，定位缺失要求 | 远程目录、仓库配置、直接下载 |
| 各资产管理 | 管理安装后的世界、材质、皮肤和家具 | 包缓存和仓库来源 |

### 在线内容主目录

- 一个顶级条目对应一个 `ContentType + Identifier`。
- 条目显示名称、类型、最新或推荐版本、本地缓存版本数、Profile 引用状态、缺失状态、可用仓库数和冲突状态。
- 支持关键字、ContentType、仓库、缓存状态和 Profile 缺失状态筛选。
- 多仓库请求允许并发，但结果合并顺序必须确定；单个仓库失败不得丢弃其他仓库的成功结果，UI 单独显示部分失败。
- 列表分页属于聚合服务契约，Screen 不自行拼接各仓库页码。第一版可以采用有界逐仓库拉取后客户端聚合，
  但必须定义最大页数、取消和部分失败行为，不能无限读取全部目录。

### 版本详情

- 版本按 SemVer 降序显示，不使用字符串字典序。
- 默认区域固定显示：最新或推荐版本、任意 Profile 引用版本、本地缓存版本、当前会话要求版本。
- “全部版本”列出仓库仍公开的其余历史版本；旧版本不得因为不是最新版本而无法精确查找或下载。
- 每个版本显示 PackageHash 摘要、包大小、缓存状态、引用它的 Profile、可用来源和仓库冲突。
- 同 hash 的来源合并；下载按仓库优先级选择可用来源，失败时可在同 hash 的其他来源间回退。
- 同 `Identifier + Version` 不同 hash 时禁用默认下载，要求用户查看冲突详情；Profile 已固定 hash 时只允许选择匹配制品。
- 下载完成后只刷新缓存状态。Mod 显示“前往 Mod 管理”；非 Mod 可以继续发起统一安装对话框。

### Mod 管理

Mod 管理列表以 Profile 和缓存的并集为事实来源，至少表达：

- Profile 引用且已缓存；
- Profile 引用但本地缺失；
- 已缓存但未被任何 Profile 使用；
- 被全局 Profile、世界 Profile 或当前 Runtime 引用；
- 同 Identifier 的多个缓存版本。

对缺失条目显示“启动时将尝试自动获取”，并提供“在仓库中查找”。导航参数必须包含
`ContentType.Mod + Identifier + Version + PackageHash`。在线内容页只定位匹配制品，不在同版本不同 hash 之间替换；
下载完成后返回并重新读取缓存。

Mod 管理页保留 Profile 添加、移除、全局/世界选择、FilePicker 导入和原包导出；从缓存移除可以保留，
但继续遵守 Profile 和当前 Runtime 引用保护。不得重新加入远程目录列表或第二套下载客户端。

## 应用模型与共享服务

### 仓库配置

引入持久化仓库模型，最低字段为：

```text
Id          客户端生成的稳定仓库 ID
Name        用户可编辑显示名称
BaseUrl     规范化 HTTPS/HTTP 基础地址
IsEnabled   是否参与普通目录和启动解析
Priority    确定性查询和下载优先级
```

- 仓库 ID 不从 URL 派生，编辑地址时保持不变。
- BaseUrl 保存前移除首尾空白和末尾 `/`，并验证绝对 HTTP(S) URI；禁止保存重复规范化地址。
- 删除仓库不删除缓存包、Profile 或安装资产。
- 测试连接只验证仓库 API 可达和协议响应，不下载包。
- 用仓库集合替换 `Settings.ContentServerUrl`，同步更新设置序列化、默认值、界面和正式文档，不保留旧字段 fallback。

### 目录聚合

建立独立于 Screen 的目录聚合服务，输入仓库快照、分页/筛选和取消令牌，输出：

```text
ContentCatalogEntry
  ContentType + Identifier
  展示元数据
  Versions[]

ContentCatalogVersion
  Version + PackageHash
  Sources[]
  CacheState
  ProfileReferences[]
  ConflictState
```

仓库响应中的 `ContentId`、`VersionId` 和相对下载地址只能保留在对应 Source 中，不能提升为跨仓库内容身份。
聚合服务必须使用共享 SemVer 规则、规范化 Identifier 和 PackageHash 比较规则。

### 客户端工厂与客户端池

- `ContentServerClientFactory` 接收已经验证、规范化的仓库描述，创建匿名 ContentServer 客户端；超时、协议版本和 HTTP 管道等
  构造细节不散落到 Screen、Profile 解析器或下载服务。
- `ContentServerClientPool` 以 `RepositoryScope + RepositoryId` 为键保存客户端，向上层提供确定性快照。仓库地址或客户端相关配置改变时，
  只替换对应客户端；禁用或删除仓库时释放对应客户端；未变化的客户端继续复用。
- Pool 快照必须通过租约、引用计数或等价机制保证正在执行的请求完成或取消后才释放旧客户端；配置编辑、仓库禁用和会话作用域结束
  不得在请求中途 Dispose 客户端，也不得让已结束作用域接受新请求。
- ClientPool 不承担包身份合并或业务重试。目录聚合服务和统一下载服务消费同一仓库快照，分别实现目录合并与下载策略。
- 持久仓库必须通过仓库 ID 选择，不允许上层以裸 URL 绕过 Factory/Pool。联机服务器下发的匿名仓库描述使用连接作用域 ID，
  连接准备结束后统一释放且不写入 Settings。

### 来源上下文与私有部署仓库

统一下载实现不等于所有调用方拥有相同来源集合。每次调用显式携带 `ContentSourceContext`：

```text
ContentSourceContext
  PersistentRepositories[]        玩家已启用的持久仓库
  SessionRepositories[]           当前游戏连接声明的客户端可达匿名仓库
```

- 在线内容 UI 默认只使用持久仓库，避免把加入某台服务器获得的私有目录误当成玩家长期订阅的内容源。
- 本地 Profile 自动补全使用持久仓库；联机服务器要求的 Profile 使用“会话仓库优先，再回退持久仓库”的组合上下文。
- 两者调用同一个下载入口、缓存和错误模型；差别只是由上层启动编排提供的来源上下文，不复制查询、传输、校验或重试代码。
- 私有部署仓库与公开仓库使用完全相同的匿名 ContentServer 协议。差别只有来源描述来自当前游戏服务器且不持久化；
  仓库必须是客户端可达地址，游戏服务器不代理包字节，也不向客户端下发任何凭据。
- 会话仓库只存活于连接准备作用域。GUI 在该作用域内完成下载后才允许写入 pending session 并重启；Headless 客户端在同一启动流程中完成下载。
  下载失败时保留连接准备页面和诊断信息，不创建一个重启后无法继续解析的半成品 session。

### 下载服务

建立供在线 UI、非 UI 调用方与启动补全共同使用的唯一下载入口。输入至少包含精确包身份、`ContentSourceContext`、
可选的仓库选择约束和取消令牌：

1. 先以期望 PackageHash 查询 ContentPackageCache。
2. 命中时返回本地条目，不创建 HTTP 请求。
3. 使用 `OnlyRepositoryId` 约束时，只使用 ClientPool 中对应作用域的客户端；该模式用于用户明确选择来源、仓库诊断或测试，
   不隐式改用其他仓库。普通下载不传该约束，避免“指定”究竟是优先还是唯一的语义歧义。
4. 未指定仓库 ID 时，查询或使用已知的同 hash Sources，按“会话临时源优先、Priority、稳定仓库 ID”的默认顺序尝试。
5. 单个来源出现连接、超时、服务端或内容校验失败时，记录失败并继续尝试声明同一 PackageHash 的下一候选来源；
   不在同版本不同 hash 之间回退。
6. 每次下载都通过现有流式缓存入口并复算 PackageHash。
7. 返回缓存结果、实际使用的仓库 ID 和前序失败摘要；来源只用于诊断和本次 UI，不写入包身份或 Profile。

统一入口不得要求调用方先取得或持有 `ContentServerClient`。目录页面可以把聚合结果中的 Sources 作为候选提示传入，
但最终客户端解析、缓存命中、来源有效性和重试仍由下载服务负责；Profile 解析器得到精确包身份后使用默认下载策略。

### Profile 与启动候选源

- `ModProfile` 删除 `ContentServerUrl`；每个本地 requirement 都持久化 `ModId + Version + PackageHash`。Mod 管理 UI 从已验证缓存包
  创建 requirement，不产生缺失 hash 的条目；在线目录下载完成后只导航到 Mod 管理，不直接编辑 Profile。
- 本地全局/世界 Profile 启动时使用已启用持久仓库，顺序按 Priority 和稳定 ID 确定。
- 联机会话把游戏服务器声明的匿名临时仓库放在候选集合前部，再追加客户端启用仓库；本地精确 hash 命中仍优先于全部候选源。
- 网络协议从单个 `ContentServerUrl` 调整为匿名临时候选仓库集合，并同步更新双方调用者和测试。
- 联机 GUI 在当前连接准备阶段完成全部缺失包下载后才创建 pending session/请求重启；临时仓库不随 session 序列化。
- GUI 与 Headless 必须调用同一个解析/下载服务，区别只限于进度和错误呈现。

### 联机握手协议

联机服务器继续使用共享 `ModProfile` 类型下发精确运行要求；本地持久化、运行期和联机场景使用同一数据模型与 hash 约束：

```text
RequiredModProfile
  Packages[]
    ModId
    Version
    PackageHash       必填的规范 SHA-256

TemporaryRepositories[]
  RepositoryId       仅在本次连接作用域稳定
  BaseUrl
  Priority
```

- `RequiredModProfile` 与 `TemporaryRepositories` 是并列字段；包要求不包含仓库地址，仓库描述也不参与包身份。
- 服务端从当前 `EffectiveProfile` 和实际已加载包生成 requirement，禁止发送未知或空 PackageHash。
- 客户端在任何缓存查询、仓库访问、session 创建或重启之前完整验证消息；非法 requirement 直接终止连接准备并返回协议错误。
- 客户端同时验证临时仓库的数量上限、作用域 ID、BaseUrl 长度、绝对 HTTP(S) URI、重复规范化地址和 Priority 范围；
  ContentServerClient 对重定向次数、响应头和包体积继续使用统一安全上限。
- 客户端缓存命中后仍核对 manifest 中的 ModId、Version 和计算得到的 PackageHash；下载时只接受三者全部匹配的包。
- 本期不为握手拆分独立 DTO 或重构协议层；只同步修改共享 Profile 约束、`ServerInfoPackage` 中临时仓库集合的序列化、
  必要的协议版本、服务端构造、客户端解析、服务器浏览信息和测试，不保留旧 `ContentServerUrl` 或缺失 hash 的读取分支。

### RequiredModProfile 仅包含 Mod

`RequiredModProfile` 不泛化为通用内容要求。Mod 是唯一无需安装、由 GameModRuntime 直接从缓存原包动态加载且必须在 Runtime
创建前准备的内容类型。其他内容包均遵守“下载到缓存、用户显式安装、产生脱离来源包的本地资产、再由对应游戏逻辑使用”的流程。

- World、BlocksTexture、CharacterSkin 和 FurniturePack 不由联机握手声明、下载或安装，服务器也不覆盖客户端对这些资产的选择。
- FurniturePack 只是向世界导入 FurnitureDesign/FurnitureSet 的输入包；安装后世界家具不再绑定来源 PackageHash。
- 锤子动态创建的 FurnitureDesign/FurnitureSet 是服务端权威世界状态：加入时由 Bootstrap `ProjectData` 同步，运行中由
  `FurniturePackage` 增量同步，不进入 ContentPackageCache 或 RequiredModProfile。
- 地形、实体和其他可变世界数据继续使用各自的世界快照与增量协议，不借用内容仓库下载模型。

## 分阶段实施

### 验收样例与文档对齐依据

本节定义目标行为；正式使用文档随对应代码变更更新，不能把尚未实现的行为描述为当前能力。

| 当前文档 | 本期调整 | 交付阶段 |
|---|---|---|
| ContentServer.md 的单地址配置 | 多仓库配置及匿名客户端统一入口 | 配置切换时 |
| Mods.md、StartupSessions.md 的不持久化 hash | 本地及 session Profile 保存精确 hash | 阶段 3 |
| 上一期计划的内容包页面兼管远程目录 | 远程浏览统一在线内容，本地页只管缓存 | 阶段 5–6 |
| 上一期计划的包协议、匿名分发和安装语义 | 保持现有规则 | 全阶段 |

样例中 H1、H2、H3 是三个不同的合法 PackageHash 的缩写，实际测试必须使用完整规范 hash。
仓库 A、B 是持久仓库，S 是游戏服务器下发的匿名临时仓库。

| 场景 | 输入 | 期望结果 |
|---|---|---|
| 来源合并 | A、B 都提供 demo@1.0.0/H1 | 一个版本条目，两个来源 |
| 冲突 | A 提供 1.0.0/H1，B 提供 1.0.0/H2 | 两个制品及冲突提示，不静默覆盖 |
| 旧版本 | 最新 2.0.0/H3，Profile 要求 1.0.0/H1 | 详情突出两者，启动只获取 H1 |
| 仅缓存 | H1 已缓存，所有仓库离线 | 本地管理可见，精确启动零网络请求 |
| 临时源回退 | Profile 要求 H1，S 不可达，A 提供 H1 | 从 A 获取，S 不写入设置 |
| 无源 | Profile 要求 H1，缓存缺失且候选源为空 | 明确缺失，不创建 pending session |
| 非法握手 | 空 hash、非法 hash 或冲突重复 ModId | 验证失败，未发起下载 |
| 网络故障 | 合法 HTTP(S) 仓库不可达 | 属于来源失败而非非法握手，继续同 hash 回退 |

Mod 管理的“在仓库中查找”传递 ModId、Version、PackageHash；在线页只负责定位及获取该制品，返回后重新读取缓存。
普通在线页默认使用持久来源；从连接准备流程进入的精确查找应保持该连接的来源上下文，直到操作结束。
已有缓存中的同版本不同 hash 仍遵守上一期缓存冲突规则，不因跨仓库 UI 显示冲突而自动替换缓存制品。

仓库样例：A 的 ID 为 `11111111-1111-1111-1111-111111111111`，地址 `https://a.example/content/`，
启用且 Priority 为 0；B 的 ID 为 `22222222-2222-2222-2222-222222222222`，地址 `https://b.example/`，
启用且 Priority 为 1。保存后移除末尾斜杠，构建 HTTP BaseAddress 时恢复一个斜杠，保留 `/content` 路径前缀。
S 的 ID 由客户端在连接作用域内解释，与持久 ID 不共享命名空间；跨作用域同地址候选按规范化地址去重，保留优先来源。
同一连接中，缓存准备完成后保存的 session Profile 必须保留 H1；后续运行重新连接时再校验服务端要求，
不能因旧缓存已经存在而跳过新的握手一致性检查。

导航验收：内容主页面可进入五类职责页面；仓库页返回在线页后刷新来源；在线详情下载后刷新缓存状态；
Mod 缺失导航携带完整精确身份，返回后读取缓存；本地包安装完成后交由资产页管理。页面离开取消该页面请求，
启动准备的请求由启动流程持有，不能随无关在线页面关闭而取消。

阶段严格按依赖顺序执行。每个交付项只归属一个阶段，未通过当前门禁前不进入下一阶段。
基础服务先建立，现有调用方在其所属阶段一次性切换；切换前的旧实现仅是待迁移代码，不增加兼容适配或双解析路径。
复选框表示对应交付和验证均完成，不表示仅已写入方案；尚未执行的设备验收必须保持未勾选。

### 阶段 0：固化补充方案与验收样例

- [x] 将本计划与上一期计划、`Doc/ContentServer.md`、`Doc/Mods.md`、`Doc/StartupSessions.md` 的现状逐项对齐。
- [x] 固定仓库配置、ContentServerClientFactory、ContentServerClientPool、ContentSourceContext、统一下载入口、内容聚合、
      版本合并、hash 冲突、匿名会话仓库和精确 RequiredModProfile 的术语与数据示例。
- [x] 固定联机握手协议样例及非法输入：空 hash、非规范 SHA-256、重复同包、同 ModId/Version 异 hash、仓库不可达。
- [x] 固定五类 UI 职责矩阵和页面导航参数，不在实现阶段临时移动职责。
- [x] 固定多版本展示样例：最新版本、旧 Profile 版本、仅缓存版本、缺失版本和冲突版本。
- [x] 明确本期不改变 `.scpkg`、PackageHash、非 Mod 安装结果和 FilePicker 协议。
- [x] 确认 `Content.Packaging`、ContentServer HTTP 契约和联机 Package 的现有项目边界，本期不提前实施协议项目重构。
- [x] 固定 RequiredModProfile 只包含 Mod；确认其他可安装内容和动态世界状态不会进入联机仓库预下载。

门禁：评审确认仓库、下载、缓存、Profile、联机握手和安装资产的生命周期边界；所有后续阶段只实现已确认模型。

### 阶段 1：建立多仓库配置内核

实施记录：已接入 Settings 初始化和仓库配置保存回调；保存失败回滚内存仓库集合并向调用方报告失败，
设置通过同目录临时文件写完后替换。Factory/Pool 的延迟创建会使用最新仓库元数据。
仓库模型、XML 往返、配置服务、健康检查及 Pool 的 20 项定向测试通过；测试覆盖配置保存失败不改变服务和客户端状态、
禁用仓库不发起健康检查，以及仓库刷新或会话结束时旧客户端在进行中租约释放后才销毁。阶段门禁通过。

- [x] 建立仓库配置实体、集合、规范化、唯一性、启用状态和确定性排序规则。
- [x] 增加仓库集合的设置持久化及应用入口；旧单地址调用按阶段 3–6 的职责迁移，最终在阶段 6 删除旧设置字段，不增加兼容读取。
- [x] 建立仓库配置的读取、保存、添加、编辑、删除、排序和连接测试服务。
- [x] 建立 `ContentServerClientFactory` 和按仓库作用域/稳定 ID 管理生命周期的 `ContentServerClientPool`，同时容纳持久和会话仓库。
- [x] 为 URL 规范化、重复地址、稳定 ID、排序、设置往返、客户端复用/替换/释放增加单元测试。
- [x] 增加配置刷新、仓库禁用和会话结束与进行中请求并发时的客户端租约/延迟释放测试。

门禁：无 UI 条件下可以可靠维护多个仓库及其客户端生命周期；新服务不依赖 `Settings.ContentServerUrl`，
并且只通过 Factory/Pool 获取仓库客户端。现有 UI 和启动调用的迁移分别在后续所属阶段验收。

### 阶段 2：建立目录聚合、版本与下载服务

实施记录：已增加有界目录页和版本历史页客户端 API，并建立并发目录聚合与统一下载服务。聚合按共享 SemVer 排序，
合并同 hash 来源、保留同版本异 hash 冲突，并把协议错误或请求失败隔离为单仓库失败；下载先查精确缓存，
支持显式来源及同 hash 来源顺序回退。缓存导入会在落盘前核对类型、Identifier、Version 和 PackageHash，错误响应不会污染缓存。
`ContentSourceContext` 统一持久和会话来源，会话来源优先并按规范化地址去重；目录请求具有并发上限，结束的会话作用域不能重开。
目录、下载、来源上下文、Pool、缓存和客户端共 23 项定向测试通过。启动和 UI 调用按其所属阶段迁移，阶段 2 门禁通过。

- [x] 扩展 ContentServer 客户端以分页查询内容聚合、版本历史和精确版本，不在 Screen 内拼 URL。
- [x] 实现跨仓库目录聚合、SemVer 排序、同 hash 来源合并和同版本异 hash 冲突模型。
- [x] 实现有界分页、取消、并发查询和部分仓库失败结果。
- [x] 实现缓存优先的统一下载入口，支持显式限定仓库及未指定仓库时的默认选择、同 hash 自动回退；启动和 UI 调用分别在阶段 3、5–6 迁移。
- [x] 增加聚合、冲突、部分失败、缓存命中零网络请求、显式单仓库、默认选择、下载回退和 hash 校验测试。

门禁：使用两个伪仓库可以稳定得到顺序确定的聚合目录；相同包去重，冲突包不覆盖，缓存命中不访问网络。

### 阶段 3：迁移 Profile 与启动补全

- [ ] 从 ModProfile、合并逻辑和序列化中删除单一 `ContentServerUrl`；继续复用共享
      `ModProfile`/`ModPackageRequirement`，要求本地持久化、运行期和联机 requirement 都包含规范 PackageHash，
      删除缺失 hash 的读取和解析路径，本期不拆分协议 DTO。
- [ ] 把 GUI 与 Headless 的 Mod 缺失补全迁移到统一下载入口，只传精确包身份并使用未指定仓库的默认策略。
- [ ] 调整 `ServerInfoPackage` 序列化、协议版本、服务端构造和客户端解析，使临时候选仓库集合与 RequiredModProfile 分离。
- [ ] 服务端发送前和客户端接收时验证完整 requirement；删除空 hash、单 `ContentServerUrl` 和旧握手格式的兼容路径。
- [ ] 为临时仓库集合增加数量、长度、URI、重复项、Priority、重定向和响应大小限制，并覆盖恶意握手输入测试。
- [ ] 调整 GUI 联机编排，确保连接准备阶段通过统一下载入口完成精确包准备，再创建 pending session 或重启；缓存优先，临时来源失败可向持久来源回退。
- [ ] 更新启动日志，使其区分本地命中、候选仓库查询、来源回退和最终缺失，不记录敏感查询信息。
- [ ] 覆盖空缓存、本地命中、首选仓库命中、备用仓库回退、全部离线、hash 冲突和联机临时源测试。
- [ ] 验证 RequiredModProfile 只接受 Mod requirement，其他 ContentType 不触发联机下载或安装。

门禁：GUI 与 Headless 对相同 Profile 和仓库集合解析出相同包；本地命中可在全部仓库离线时启动；抓取握手可确认每个
requirement 都携带非空规范 PackageHash，临时仓库不嵌入 Profile，任一非法 requirement 在下载或重启之前被拒绝。

### 阶段 4：实现内容仓库管理 UI

- [ ] 在内容主页面增加独立“内容仓库”入口。
- [ ] 实现仓库列表、添加、编辑、删除、启用、排序和测试连接交互。
- [ ] 提供供在线内容及 Mod 管理使用的仓库管理导航入口；原页面的地址输入随阶段 5–6 页面迁移删除。
- [ ] 为无仓库、地址非法、重复仓库、测试失败和部分仓库禁用提供明确文案。
- [ ] 使用 LanguageTool 增加并验证本期多语言键。

门禁：用户不进入 Mod 管理页即可完成全部仓库配置；仓库编辑不影响缓存包、Profile 和安装资产。

### 阶段 5：重构统一在线内容与版本 UI

- [ ] 将在线内容主列表改为按 ContentType + Identifier 聚合，并接入搜索、类型、仓库和状态筛选。
- [ ] 删除在线内容页地址输入及单地址客户端构造，使用仓库管理导航和共享服务。
- [ ] 实现版本详情，默认突出最新、Profile 引用、缓存和当前会话版本，并提供完整历史版本列表。
- [ ] 展示每个版本的 hash、大小、缓存、Profile 引用、来源、冲突和部分仓库失败状态。
- [ ] 所有类型通过统一下载服务进入缓存；Mod 下载后不修改 Profile，非 Mod 下载后可发起已有安装流程。
- [ ] 完成真实分页/加载更多、取消、重试、离线缓存状态和返回后状态刷新。
- [ ] 为同版本多来源、同版本冲突、旧版本定位和精确筛选导航增加 UI 状态测试。

门禁：在线内容统一承接各类型的手动远程下载；用户能够获取 Profile 所需旧版本，且不会把不同 hash 的同版本误认为同一包。
Mod 管理中待删除的旧下载入口在阶段 6 收口，最终只保留在线内容入口。

### 阶段 6：收口 Mod 管理与本地包 UI

- [ ] 从 ModManagementScreen 删除服务器地址、保存默认地址、远程刷新、远程条目和直接下载代码。
- [ ] 以 Profile requirements 与本地缓存 Mod 的并集重建列表，明确显示已缓存、缺失、全局、世界和当前 Runtime 状态。
- [ ] 为缺失 requirement 实现“在仓库中查找”，只导航到统一在线内容的精确版本筛选。
- [ ] 保留并验证 Profile 编辑、FilePicker 导入/导出和受引用保护的缓存移除。
- [ ] 审查本地内容包 Screen，使远程目录和版本获取不泄漏到缓存管理职责中。
- [ ] 删除失去用途的 Mod 管理 UI 专用远程条目模型、UI 文案和重复下载路径，不触及后续协议层 DTO 重构。
- [ ] 确认现有调用均已迁移后，删除 `Settings.ContentServerUrl` 及其剩余序列化和配置说明，不保留兼容读取。

门禁：Mod 管理页断网可完整管理已有 Profile 与缓存；代码中不引用 ContentServerClient 或仓库地址；下载不会隐式启用 Mod。

### 阶段 7：集成验收、正式文档与清理

- [ ] 验证 Windows、Linux、Android 的仓库管理、在线目录、版本详情、下载、返回刷新和 Mod 缺失定位。
- [ ] 验证 GUI 与 Headless 启动自动补全，多仓库回退和完全离线本地命中。
- [ ] 验证 ContentServer 单仓库部署仍可作为只有一个仓库的普通配置工作，但不保留单地址专用代码路径。
- [ ] 更新 `Doc/Architecture.md`、`Doc/ContentServer.md`、`Doc/Mods.md`、`Doc/StartupSessions.md` 和相关 UI 文档。
- [ ] 全仓搜索并清理单服务器 UI、`ContentServerUrl`、Mod 远程下载按钮和旧职责描述。
- [ ] 对照上一期计划复核全部门禁；记录必须由人工设备验证的项目。

门禁：本计划与上一期计划全部验收项完成，正式文档准确描述最终行为，才允许删除两份临时计划和 README 入口。

## 必须覆盖的测试与验证

### 仓库与聚合

- 多仓库设置持久化、排序、启用、禁用、编辑和删除。
- 相同包跨仓库合并来源；同 Identifier + Version 不同 hash 显示冲突。
- 仓库超时、无效响应、取消和部分失败不破坏其他仓库结果。
- 版本使用 SemVer 排序；最新、旧 Profile、缓存和缺失版本均可定位。

### 下载与缓存

- 精确 PackageHash 本地命中时零网络请求。
- 显式指定仓库时只调用该仓库，失败结果不隐式切换来源。
- 未指定仓库时按确定性默认策略选择，并且调用方不感知仓库数量。
- 首选来源失败后只向声明同 hash 的备用来源回退。
- 下载内容与目录 hash 不符时拒绝提交，并保留既有有效缓存。
- 重复下载同 hash 幂等；删除仓库不删除已缓存包。

### ModProfile 与启动

- Profile 精确旧版本已缓存时离线启动。
- Profile 精确旧版本缺失时自动从候选仓库下载。
- 联机握手的每个 requirement 都包含非空规范 PackageHash；空值、非法格式和重复冲突会在任何副作用前被拒绝。
- 服务端声明的 ModId、Version、PackageHash 与其实际 EffectiveProfile/已加载包完全一致。
- 联机临时候选源不写入持久仓库设置。
- 私有部署仓库与公开仓库使用相同匿名查询和下载协议；临时仓库不会进入 Profile、Settings、缓存索引或 pending session。
- GUI 在联机缺失包下载完成前不重启；重启后断网仍能从缓存恢复目标 session。
- 临时候选仓库不可达时可以回退到持久仓库中声明同一 PackageHash 的来源，全部失败时不创建 pending session。
- 缺失、全部仓库离线、版本不存在和 hash 冲突返回可诊断错误。
- 下载、导入和启动补全均不擅自修改 Profile。
- 动态家具继续由 Bootstrap 世界状态和 FurniturePackage 增量事件同步，不产生仓库下载要求。
- World、BlocksTexture、CharacterSkin 和 FurniturePack 不出现在 RequiredModProfile 中，也不会在联机准备阶段自动安装。

### UI 职责

- 内容仓库页不浏览或下载包。
- 在线内容页不编辑 Profile 或管理安装后资产。
- Mod 管理页不查询远程目录或直接下载；缺失条目只导航到统一入口。
- 本地内容包页不保存仓库配置或维护非 Mod 安装来源关系。
- 返回、取消、异步失败和 Screen 离开后回调不会更新已失效 UI。

## 完成与临时文档删除条件

只有同时满足以下条件，才可以提出删除本计划和上一期计划：

1. 两份计划的全部阶段、门禁和人工验收均完成。
2. 正式协议和架构文档已经吸收仍长期有效的规则。
3. 全仓不存在单内容服务器设置、Mod 管理页远程下载或同版本来源覆盖路径。
4. GUI、Headless 和联机启动都使用相同的本地优先多仓库补全服务。
5. README 同时移除两份临时计划入口，不能只删除其中一份。
