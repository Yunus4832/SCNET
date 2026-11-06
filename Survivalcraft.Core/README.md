# SCNET.Survivalcraft.Core

共享游戏规则、世界基础类型和内容数据库。仅依赖无图形的基础库，不引用窗口、渲染或平台宿主。
方块规则、地形生成与保存、物理、电路、库存、角色、AI、世界生命周期和复制状态由本程序集管理。
Authority 与 Client 分别创建自己的世界实例，共享规则算法。网络节点、资源加载和客户端表现由上层装配。
构建边界禁止引用 Hosting、Client、混合 Engine 和 Engine.Media。
