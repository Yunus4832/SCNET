# SCNET.Survivalcraft.Hosting

图形应用和独立服务器共用的无图形宿主服务。入口项目负责平台生命周期与应用装配，Hosting 负责启动会话、权威世界、连接和持久化。

- `GameWorldController` 负责权威世界加载和关闭，`AuthorityWorldProjectFactory` 装配内容目录、模板和生效策略。
- `AuthorityWorldExecutor` 在所属线程推进权威世界，处理模拟、世界命令和消息；客户端可以复用 Hosting 的连接与命令服务。
- 网络层消费 Core 的世界快照，负责地形编码、分片、修复和发送预算。
- `WorldRepository` 管理世界文件，`WorldProjectActivity` 将活动项目的操作投递到所属线程。正常关闭时，`WorldProjectPersistence` 捕获并异步写入快照，写入失败时保留世界以便重试。

[启动会话与配置](../Doc/StartupSessions.md) · [地形分发](../Doc/TerrainDistribution.md) · [构建与发行](../Doc/BuildAndConfig.md)
