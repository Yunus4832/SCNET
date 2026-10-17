# 文档索引

本目录保存项目的长期行为、架构、协议和开发约定。组件 README 说明该组件的用途与使用入口；
`.agents/skills/` 保存 Agent 执行操作时的工作流程；临时计划、环境记录和测试证据放在忽略的
`.agent-work/`，不作为正式文档的依赖。

## 开始使用与构建

- [仓库首页](../README.md)：项目概述、快速开始与项目结构。
- [构建与共享配置](BuildAndConfig.md)：工具链、构建、版本归属和应用发行。
- [NuGet 包](NuGet.md)：包清单、依赖边界、本地准备与选择打包。
- [外部依赖](Dependencies.md)：主要托管包、原生库和工具链。
- [Headless 模式](Headless.md)：无界面运行、命令行与游戏服务器容器部署。

## 架构与游戏系统

- [结构与分层](Architecture.md)：项目职责和依赖边界。
- [启动会话](StartupSessions.md)：启动请求、实例、会话、重启和桌面更新。
- [文件定位](FileStorage.md)：逻辑路径及平台文件访问。
- [命令与权限](CommandPermissions.md)：执行身份、权限和 HTTP 适配。
- [玩家传送与标记](Teleportation.md)：命令、坐标检查、授权与异步结果。
- [网络复制](NetworkReplication.md)：游戏状态同步与复制职责。
- [地形分发](TerrainDistribution.md)：区块权威内容、版本和线程所有权。

## 模组与内容协议

- [模组使用](Mods.md)：缓存、配置范围、内容来源和联机准备。
- [模组开发](Modding.md)：模板、生命周期、方块目录及构建。
- [内容包协议](Protocol/ContentPackageProtocol.md)：`.scpkg` 格式、清单与载荷。
- [内容创建协议](Protocol/ContentCreationProtocol.md)：游戏内创建内容包的流程。
- [服务器源协议](Protocol/ServerSourceProtocol.md)：只读目录契约，与临时 P2P 房间发现分离。

## 服务与部署

- [ContentServer](ContentServer.md)：领域职责、投稿审核、公告、版本与 API。
- [ContentServer HTTP API](ContentServerApi.md)：端点、认证、初始化及投稿格式。
- [ContentServer 部署](ContentServerDeployment.md)：配置、持久化、反向代理和容器。
- [信令服务部署](RendezvousDeployment.md)：HTTPS、UDP、中继端口与部署配置。
- [Web 工作台](../ContentWebUI/README.md)：前端开发、后端地址及跨域设置。

## 测试与工具

- [自动化接口](Automation.md)：游戏调试、界面查询和控制接口。
- [网络测试](NetworkTesting.md)：传输验证、弱网与容量测试的范围划分。
- [容量测试](ServerLoadTesting.md)：协议客户端、负载场景和结果解释。
- [NetworkProbe](../NetworkProbe/README.md)：传输层探针命令与参数。
- [GUI 图集工具](../GuiAtlasTool/README.md)：源资源、生成及 Alpha 处理。

## 文档维护

行为说明以对应源码、项目文件和配置模板为核对依据。命令示例应标明执行目录与必要前提；
部署文档使用示例地址和占位值，实际机器信息与凭据不进入仓库。修改行为时同步维护其所属文档，
其他页面通过链接引用，避免复制多份完整操作说明。

[免责声明](Disclaimer.md)
