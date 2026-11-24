# 模组使用说明

1. 在「内容 → 在线内容」下载模组，或在「内容 → 模组」导入 `.scpkg` 文件。
2. 模组管理负责包缓存与全局配置；在世界创建或世界编辑中选择该世界使用的模组。下载或导入不会自动启用包。
3. 进入存档。如果目标组合和当前运行的模组不同，按提示重启。加入服务器时，游戏按服务器要求准备模组包。

## 核心概念

### `.scpkg`

`.scpkg` 是统一内容包。模组包可包含程序集、规则数据和资源。包清单及入口字段见[内容包协议](Protocol/ContentPackageProtocol.md)与[模组开发](Modding.md)。包清单的 `scope` 决定加载位置。同一份世界 Profile 可以同时列出三种模组。

| 清单作用域 | 权威端运行时 | 客户端运行时 | 客户端连接时需要该包 |
| --- | --- | --- | --- |
| `server` | 加载 | | |
| `client` | | 加载 | 是 |
| `both` | 加载 | 加载 | 是 |

服务端持有完整 Profile 和相应包，以便读取清单并确定客户端的精确要求。客户端按服务器指定的版本和哈希准备包。两端分别校验依赖，例如双端模组的必需依赖也必须在两端可用。

图形程序启动菜单时创建客户端运行时。打开本地世界时，游戏另外创建权威端运行时，即使玩家只进行单机游戏。两端各有自己的模组实例和模板数据库。菜单预览使用独立的方块目录，进入世界后按该存档的方块 ID 映射构建目录。

### 本地缓存

路径：`GamePaths.ContentPackageCache`

统一缓存按 PackageHash 存储所有类型的 `.scpkg`。Mod 运行时通过 `LocalModRepository` 查询适配器筛选
`type = Mod`，再查找匹配的 `ModId + Version + PackageHash`。

Mod 管理界面以缓存、全局 Profile、全部世界 Profile 和当前 Runtime 的精确并集显示条目，因此 Profile 已引用但尚未缓存的
版本也会显示为缺失。同版本不同 hash 是两个独立条目。导入只刷新缓存，不自动修改 Profile；导出通过 FilePicker 原样复制
缓存 `.scpkg`，不会重新封装、改变 PackageHash 或登记外部源路径。缺失条目的“在仓库中查找”只导航到统一在线内容页的精确
版本筛选，实际下载仍由在线内容页完成。

### ModProfile

`ModProfile` 决定本次运行加载哪些模组。

用户可编辑的 XML 只保存：

```xml
<ModProfile Id="default">
  <Packages>
    <Package ModId="scnet.territory" Version="0.1.0"
             PackageHash="0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef" />
  </Packages>
</ModProfile>
```

每个 requirement 必须同时包含 `ModId`、规范 SemVer `Version` 和小写 64 位 SHA-256 `PackageHash`。Profile 不保存仓库地址；
缺少 hash、hash 非规范或同一 ModId 出现互相冲突的 requirement 都会在加载前被拒绝。

## Profile 位置

- 全局 profile：`config:ModProfile.xml`
- 世界 profile：`<world>/WorldModProfile.xml`
- 会话 profile：`config:SessionProfiles/<sessionId>.xml`

进入世界前优先使用会话指定的 Profile（包括空组合），未指定时加载全局 Profile。全局 Profile 支持主菜单等前置 UI 扩展，同时为创建世界的模组草稿提供初始列表。
玩家可以在创建时调整或清空，确认后世界保存完整、独立的 Profile，空列表也会明确保存。
创建完成后不再动态合并全局配置；之后修改全局模组不会改变已有世界。

会话 Profile 用于跨重启准备精确运行环境及远程联机。已有本地世界以自己的 Profile 为准，
会话不能覆盖它；待创建世界可以通过会话保留玩家选择的初始组合。当前运行组合位于
`CurrentModRuntime.Value.LoadedProfile`。

## 编辑世界模组

创建世界时配置初始组合只产生一个存档。编辑已有世界时，选择页只修改草稿，最终点击“应用”才保存。
点击列表行选中模组，再通过底部按钮添加、替换或移除。应用模组组合变更时，确认框显示自动生成的副本名（编辑前的世界名称加 `@ddHHmmss` 本地时间戳）。手动克隆同样只需确认。再次克隆时替换末尾时间戳，保留名称中其他 `@`。确认模组变更后先克隆完整旧世界，再将变更应用到当前存档。
应用时先在临时目录准备完整存档和目标模组配置，再替换当前目录。磁盘需要同时容纳原存档、独立克隆和临时存档。冒险模式的重开快照也使用目标模组组合。
克隆失败则不应用。取消、恢复原组合或仅调整顺序不因模组规则产生克隆。

模组作者必须声明是否允许移除。不可移除的模组不能从已有世界移除；同一 ModId 更换版本或包 hash 仍允许，但需要克隆。
缺少原包、无法检查声明时也不允许移除。克隆是普通独立世界，没有主从或恢复关系；新组合失败后不会自动覆盖当前存档。
这保留了玩家修改前的内容，不保证作者声明错误时新组合依然可用，也不自动清理模组留下的数据。

## 内容仓库

ContentServer 是匿名目录和包分发服务。客户端可以在“内容 → 内容仓库”中维护多个持久仓库；每项具有稳定 ID、名称、
HTTP(S) 基础地址、启用状态和顺序。Profile 不关心仓库数量或地址。

统一下载流程先按 PackageHash 查询本地缓存。本地缺失时，默认按仓库顺序选择声明同一 hash 的来源；某个来源失败后只会回退到
同样声明该 hash 的其他来源。在线内容页也可以显式限定一个来源，此时失败不会隐式切换仓库。

联机服务器可在握手中下发匿名临时候选仓库。它们只在本次连接准备作用域内优先使用，失败后可以回退到持久仓库；不会写入
Settings、Profile、缓存索引或 pending session。公开与私有部署仓库使用相同匿名查询和下载协议，不存在认证分叉。

## 本地世界加载流程

GUI 启动时：

1. 解析当前启动会话对应的有效 `ModProfile`
2. 从统一 ContentPackageCache 查询所需包
3. 如果 profile 中的精确包本地缺失，按持久仓库集合下载到统一缓存
4. 使用解析到的包启动模组 runtime

之后玩家进入本地世界时：

1. 按目标 world 解析有效 `ModProfile`
2. 按完整 requirement 从缓存或持久仓库补全缺失 Mod
3. 比较目标 Profile 的客户端包集合和 `CurrentModRuntime.Value.LoadedProfile`
4. 如果相同，直接进入世界
5. 如果不同，创建临时 session profile 并请求重启

空世界 Profile 表示只加载内置内容；已有世界缺少 Profile 是配置错误，不回退全局。
空 Profile 也参与比较：当前 runtime 有模组而目标世界为空时，需要切换运行环境。

独立服务器启动时没有 GUI 中途切换流程。它会直接解析启动 session 对应的有效 profile，下载缺失包，并以服务端侧 runtime 启动。

## 联机加载流程

服务器从完整 Profile 选取 client 和 both 包生成 `RequiredModProfile`，其中每项都包含
`ModId + Version + PackageHash`。服务器信息包把临时候选仓库集合放在 Profile 之外。

客户端连接时：

1. 读取服务器下发的 required profile
2. 检查本地缓存是否已有 required mods
3. 缺失时依次使用临时候选仓库和持久仓库补全同一 PackageHash
4. 如果 `CurrentModRuntime.Value.LoadedProfile` 已经是同一组精确 requirement，直接继续连接
5. 否则创建临时 session profile 并请求重启

服务器没有启用模组时也会明确下发空 required profile。客户端若仍加载全局模组，同样会创建空的临时 session profile 并请求重启，以卸载这些模组后再连接。

非法、重复或互相冲突的 requirement 会在下载、创建 pending session 或重启前被拒绝。缓存准备完成后才会创建远程 pending
session；重启后即使仓库离线，也能从内容寻址缓存恢复。联机校验还会比较客户端包集合的指纹，客户端多加载、少加载或加载了不同版本的包都会被拒绝。World、材质、皮肤和家具包不进入 RequiredModProfile，也不会在联机准备阶段自动安装。

## 开发文档

创建和打包模组见 [Modding.md](./Modding.md)。

部署内容服务及其模组查询接口见 [ContentServer.md](./ContentServer.md)。
