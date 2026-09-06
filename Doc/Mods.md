# 模组使用说明

SCNET 的模组系统以统一内容包 `.scpkg` 和 `ModProfile` 为核心。

包是否存在不代表会被加载。只有当前有效 profile 中列出的模组才会进入运行时。

## 核心概念

### `.scpkg`

`.scpkg` 是统一内容包协议定义的 ZIP 容器。Mod payload 包含：

```text
manifest.json
payload/mod.json
payload/assemblies/*.dll
payload/data/**
payload/assets/<mod-id>/**
```

`manifest.json` 的公共字段及 Mod metadata 至少描述：

- `formatVersion` 与 `type`
- `identifier`（即 ModId）
- `name`
- `version`
- `payload`
- `metadata.side`
- `metadata.entrypoints`
- `metadata.dependencies`

`side` 可为：

- `common`
- `client`
- `server`

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
    <Package ModId="verification.block" Version="1.0.0"
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

世界 profile 的合并方式由世界设置里的 `ModProfileResolutionStrategy` 决定：

- `WorldOnly`
- `GlobalPlusWorld`
- `WorldPlusGlobal`

会话 profile 优先级最高，通常由进入 world 或远程联机前的重启流程生成。启动完成后，当前已生效的模组组合保存在 `CurrentModRuntime.Value.EffectiveProfile` 中。

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

之后玩家进入本地 world 时：

1. 按目标 world 解析有效 `ModProfile`
2. 按完整 requirement 从缓存或持久仓库补全缺失 Mod
3. 比较目标 profile 和 `CurrentModRuntime.Value.EffectiveProfile`
4. 如果相同，直接进入 world
5. 如果不同，创建临时 session profile 并请求重启

如果 profile 为空或缺失，则只加载内置内容。空 profile 也会参与比较；例如当前 runtime 已加载模组，而目标 world 不启用模组时，也需要重启来卸载模组。

Headless 启动时没有 GUI 中途切换流程。它会直接解析启动 session 对应的有效 profile，下载缺失包，并以服务端侧 runtime 启动。

## 联机加载流程

服务器启动 runtime 后，会根据实际有效 Profile 和已加载包生成 `RequiredModProfile`，其中每项都包含
`ModId + Version + PackageHash`。服务器信息包把临时候选仓库集合放在 Profile 之外。

客户端连接时：

1. 读取服务器下发的 required profile
2. 检查本地缓存是否已有 required mods
3. 缺失时依次使用临时候选仓库和持久仓库补全同一 PackageHash
4. 如果 `CurrentModRuntime.Value.EffectiveProfile` 已经是同一组精确 requirement，直接继续连接
5. 否则创建临时 session profile 并请求重启

非法、重复或互相冲突的 requirement 会在下载、创建 pending session 或重启前被拒绝。缓存准备完成后才会创建远程 pending
session；重启后即使仓库离线，也能从内容寻址缓存恢复。联机校验还会比较运行时计算的 mod data hash，客户端和服务端有效模组
不同会被拒绝。World、材质、皮肤和家具包不进入 RequiredModProfile，也不会在联机准备阶段自动安装。

## 示例模组

仓库内置示例：

- 项目：`VerificationBlockMod/`
- 模组 ID：`verification.block`
- 版本：`1.0.0`

构建：

```bash
dotnet build VerificationBlockMod/VerificationBlockMod.csproj -c Debug
```

输出：

```text
VerificationBlockMod/bin/Debug/net10.0/packages/verification.block.scpkg
```

## 开发文档

创建和打包模组见 [Modding.md](./Modding.md)。

部署内容服务及其模组查询接口见 [ContentServer.md](./ContentServer.md)。
