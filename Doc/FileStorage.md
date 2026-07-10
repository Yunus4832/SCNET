# 文件定位

`Engine.FileStorage.Storage` 负责统一处理文件路径。项目中的大部分文件读写并不是直接使用系统路径，而是先使用带前缀的逻辑路径，再由 `Storage` 映射到当前平台上的实际位置。

## 解析方式

- 先识别路径前缀
- 再根据平台将路径转换为实际文件系统路径
- 最后执行读写、枚举、创建或删除操作

这样做的原因是：同一份代码需要同时运行在桌面端和 Android 端，直接使用绝对路径会使跨平台逻辑变得难以维护。

## 桌面端支持的预定义路径

- `app:`: 应用目录，通常对应程序入口所在目录
- `data:`: 数据目录，通常对应用户本地应用数据目录下的应用子目录
- `config:`: 配置目录，位于应用目录下的配置路径
- `system:`: 直接使用系统路径，不再额外拼接应用目录

## Android 端支持的预定义路径

- `app:`: 应用资源目录，对应 APK 内部资源访问
- `data:`: 应用数据目录，通常对应用户文档或应用可写目录
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
- `GamePaths.ModCache`: 按包 hash 保存的本地模组缓存
- `app:Content.zip`
- `data:` 下的世界、缓存和玩家数据

世界 Project 当前只使用 `Project.xml`。旧的 `Project.json`、`Project.mpk`、`Project.bak` 和 `Project.temp` 不再作为世界 Project 的磁盘序列化或恢复机制使用。保存时会先写入 `Project.xml.tmp`，校验后再替换 `Project.xml`。升级工具仍可在缺少 `Project.xml` 时读取旧 `Project.json` 并转换为 `Project.xml` 后继续升级。

## 说明

- `app:` 适合读取随程序分发的资源
- `data:` 适合保存运行时数据、日志、世界和缓存
- `config:` 适合保存启动设置、游戏设置和模组设置
- `android:` 主要用于 Android 平台上的外部存储访问
- `system:` 仅在桌面端可用，用于直接访问系统路径
