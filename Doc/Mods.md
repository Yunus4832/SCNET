# 模组使用说明

SCNET 的模组系统以 `.scpak` 包和 `ModProfile` 为核心。

包是否存在不代表会被加载。只有当前有效 profile 中列出的模组才会进入运行时。

## 核心概念

### `.scpak`

`.scpak` 是一个 zip 包，内部包含：

```text
manifest.json
assemblies/*.dll
data/**
assets/<mod-id>/**
```

`manifest.json` 至少描述：

- `id`
- `name`
- `version`
- `side`
- `entrypoints`

`side` 可为：

- `common`
- `client`
- `server`

### 本地模组目录

路径：`GamePaths.Mods`

这是用户手动放置 `.scpak` 的入口目录。启动时，程序会扫描这个目录，把包导入到本地缓存。

### 本地缓存

路径：`GamePaths.ModCache`

缓存按包 hash 存储 `.scpak`，用于避免重复保存同一个包。运行时真正解析 required mods 时，会从缓存中查找匹配的 `ModId + Version`。

### ModProfile

`ModProfile` 决定本次运行加载哪些模组。

用户可编辑的 XML 只保存：

```xml
<ModProfile Id="default" RepositoryUrl="http://example.com:9527">
  <Packages>
    <Package ModId="verification.block" Version="1.0.0" />
  </Packages>
</ModProfile>
```

`PackageHash` 不写入用户配置。hash 仍然会在运行时用于缓存定位、联机校验和服务器 required profile。

## Profile 位置

- 全局 profile：`config:ModProfile.xml`
- 世界 profile：`<world>/WorldModProfile.xml`
- 会话 profile：`config:SessionProfiles/<sessionId>.xml`

世界 profile 的合并方式由世界设置里的 `ModProfileResolutionStrategy` 决定：

- `WorldOnly`
- `GlobalPlusWorld`
- `WorldPlusGlobal`

会话 profile 优先级最高，通常由进入 world 或远程联机前的重启流程生成。启动完成后，当前已生效的模组组合保存在 `CurrentModRuntime.Value.EffectiveProfile` 中。

## 仓库地址

模组仓库用于按 `ModId + Version` 下载缺失包。

解析顺序：

1. 当前有效 `ModProfile.RepositoryUrl`
2. `Settings.DefaultModRepositoryUrl`

远程服务器下发的 `RequiredModProfile.RepositoryUrl` 优先级最高。客户端连接远程服务器时，不会用本地默认仓库覆盖服务器声明的仓库。

## 本地世界加载流程

GUI 启动时：

1. 扫描 `GamePaths.Mods`
2. 导入 `.scpak` 到 `GamePaths.ModCache`
3. 解析当前启动会话对应的有效 `ModProfile`
4. 如果 profile 中的包本地缺失，尝试从仓库下载
5. 使用解析到的包启动模组 runtime

之后玩家进入本地 world 时：

1. 按目标 world 解析有效 `ModProfile`
2. 下载缺失的 required mods
3. 比较目标 profile 和 `CurrentModRuntime.Value.EffectiveProfile`
4. 如果相同，直接进入 world
5. 如果不同，创建临时 session profile 并请求重启

如果 profile 为空或缺失，则只加载内置内容。空 profile 也会参与比较；例如当前 runtime 已加载模组，而目标 world 不启用模组时，也需要重启来卸载模组。

Headless 启动时没有 GUI 中途切换流程。它会直接解析启动 session 对应的有效 profile，下载缺失包，并以服务端侧 runtime 启动。

## 联机加载流程

服务器启动 runtime 后，会根据当前有效 profile 生成 `RequiredModProfile` 并放入服务器信息包。

客户端连接时：

1. 读取服务器下发的 required profile
2. 检查本地缓存是否已有 required mods
3. 缺失时从服务器声明的仓库下载
4. 如果 `CurrentModRuntime.Value.EffectiveProfile` 已经是同一组 `ModId + Version`，直接继续连接
5. 否则创建临时 session profile 并请求重启

联机校验使用运行时计算的 mod data hash。客户端和服务端有效模组不同会被拒绝。

## 默认模组仓库

`Settings.DefaultModRepositoryUrl` 是本地默认仓库地址，供模组管理界面和没有显式仓库地址的 profile 使用。

它不是“当前联机服务器地址”。服务器对客户端声明的仓库地址来自当前有效 profile，profile 为空时才 fallback 到默认仓库。

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
VerificationBlockMod/bin/Debug/net10.0/packages/verification.block.scpak
```

## 开发文档

创建和打包模组见 [Modding.md](./Modding.md)。

部署私有仓库见 [ModServer.md](./ModServer.md)。
