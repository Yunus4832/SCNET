# 文件定位

`Engine.FileStorage.Storage` 负责统一处理文件路径。项目中的大部分文件读写并不是直接使用系统路径，而是先使用带前缀的逻辑路径，再由 `Storage` 映射到当前平台上的实际位置。

## 解析方式

- 先识别路径前缀
- 再根据平台将路径转换为实际文件系统路径
- 最后执行读写、枚举、创建或删除操作

这样做的原因是：同一份代码需要同时运行在桌面端和 Android 端，直接使用绝对路径会使跨平台逻辑变得难以维护。

## 桌面端支持的预定义路径

- `app:`: 应用目录，通常对应程序入口所在目录
- `external:`: 当前数据实例根目录
- `data:`: 当前数据实例的数据目录
- `config:`: 当前数据实例的配置目录
- `system:`: 直接使用系统路径，不再额外拼接应用目录

## Android 端支持的预定义路径

- `app:`: 应用资源目录，对应 APK 内部资源访问
- `external:`: 当前 Android 数据实例根目录
- `data:`: 当前 Android 数据实例的数据目录
- `android:`: Android 外部存储目录下的 `scnet` 子目录
- `config:`: 配置目录，位于 Android 运行环境下的配置路径

## 典型用法

仓库中常见的路径有：

- `config:RunningSetting.xml`
- `config:SessionInfo.xml`
- `config:Settings.xml`
- `config:ModProfile.xml`
- `config:SessionProfiles/<sessionId>.xml`
- `<world>/Project.xml`
- `<world>/WorldModProfile.xml`
- `GamePaths.Mods`: 用户手动放置 `.scpak` 的入口目录
- `GamePaths.ContentPackageCache`: 按 PackageHash 保存所有类型原始 `.scpkg` 的统一缓存；`GamePaths.ModCache` 是迁移期别名
- `GamePaths.BlockTextures`、`GamePaths.CharacterSkins`、`GamePaths.FurniturePacks`: 已安装资产目录；用户资产以稳定 GUID `AssetKey` 保存，显示名称位于伴随元数据中，与缓存包独立删除

材质、皮肤和家具替换先写入并验证临时数据，再保留原 AssetKey 交换数据与元数据；World 覆盖使用同一 Worlds 根目录下的 staging/backup 目录完成可恢复替换。材质或皮肤删除前，管理界面要求为所有 World 与当前会话引用选择同类型替代资产，并以暂存 `Project.xml` 批量提交。

游戏内容制造先在 `GamePaths.ContentPackageCreationTemp` 生成并通过共享 Reader 复验临时 `.scpkg`，调用方只获得可读取且可释放的临时制品。保存制品不写入 ContentPackageCache，也不自动安装；“创建新版本”只从同类型基线包继承 Identifier。

本地输入流与 ContentServer 下载统一进入 `ContentPackageWorkflow`：先由 ContentPackageCache 原子提交和复验，再从缓存打开实际文件交给安装器。来源只影响 UI 的信任提示，不改变缓存或安装结果。
- `app:Content.zip`
- `data:` 下的世界、缓存和玩家数据

Starter 会先注册程序基础目录并读取 `Starter.xml`，再把选中的 `Instances/<name>` 重新注册为当前进程的 `external:`、`data:` 和 `config:`。桌面端可并发运行多个隔离实例；Android 同样隔离实例数据，但单个已安装包不提供并发游戏进程。不同实例拥有彼此隔离的 session、设置、世界、模组和日志；逻辑路径相同并不代表落到同一个物理目录。

世界 Project 当前只使用 `Project.xml`。旧的 `Project.json`、`Project.mpk`、`Project.bak` 和 `Project.temp` 不再作为世界 Project 的磁盘序列化或恢复机制使用。保存时会先写入 `Project.xml.tmp`，校验后再替换 `Project.xml`。升级工具仍可在缺少 `Project.xml` 时读取旧 `Project.json` 并转换为 `Project.xml` 后继续升级。

## 说明

- `app:` 适合读取随程序分发的资源
- `external:` 是当前实例根目录，通常由 Starter 管理
- `data:` 适合保存当前实例的运行时数据、日志、世界和缓存
- `config:` 适合保存当前实例的启动设置、session、游戏设置和模组设置
- `android:` 主要用于 Android 平台上的外部存储访问
- `system:` 仅在桌面端可用，用于直接访问系统路径
