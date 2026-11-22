# SCNET.Survivalcraft.Core

共享游戏规则、世界基础类型和内容数据库。仅依赖无图形的基础库，不引用窗口、渲染或平台宿主。
方块规则、地形生成与保存、物理、电路、库存、角色、AI、世界生命周期和复制状态由本程序集管理。
Authority 与 Client 分别创建自己的世界实例，共享规则算法。网络节点、资源加载和客户端表现由上层装配。
构建边界禁止引用 Hosting、Client、混合 Engine 和 Engine.Media。

## 开发入口

- 在 `Terrains/Generation/` 查找地形生成器和环境查询，在 `Terrains/Execution/` 查找区块任务调度。[地形分发与线程所有权](../Doc/TerrainDistribution.md)
- 方块、实体和世界规则的模组扩展入口见[模组开发](../Doc/Modding.md#模组定义与世界状态)。
- 从仓库根目录运行 `dotnet test Tests/Survivalcraft.Core.Test/` 验证核心规则。涉及组装后的游戏流程时，再选择 `Tests/Survivalcraft.Test/` 的对应测试。
