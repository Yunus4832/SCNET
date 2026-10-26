# SCNET.Engine

SCNET 的跨平台引擎运行时，提供图形、窗口、输入、音频、媒体与存储能力。
支持 .NET 10 和 .NET 10 for Android；具体平台启动由应用项目负责。

依赖基础类型、序列化与媒体模块。图形、窗口和输入使用 Silk.NET；音频播放通过 OpenAL。
独立的 `SCNET.Engine.Media` 负责图像、音频和模型编解码，工具或模组可直接使用其流接口。

- 图像原生库由 `SCNET.Engine.Media` 的包依赖提供。
- 各平台 OpenAL 原生库由 `SCNET.Native.OpenAL` 提供。
- OpenAL 托管绑定与 SDL 继续由对应 Silk.NET 包提供。
- 消费应用无需声明这些原生文件的复制路径或 ABI 映射。

本包不包含游戏规则或模组构建工具。编写游戏模组通常引用 `SCNET.Survivalcraft` 即可。

[依赖说明](https://gitee.com/Yunus0712/scnet/blob/main/Doc/Dependencies.md)

## 字体绘制

引擎不内置默认字体。使用者加载 `BitmapFont`，并在调用 `FontBatch` 或构造
`FontBatch2D` / `FontBatch3D` 时显式传入。字体及其纹理由使用者管理生命周期。

顶点布局类型（`VertexDeclaration`、`VertexElement` 及其格式、语义和扩展方法）位于
`Engine.Graphics`，由缓冲上传和着色器绑定使用。媒体模型数据通过独立访问器描述文件属性，
不依赖这些渲染布局类型。

窗口异常事件数据 `UnhandledExceptionInfo` 与垂直同步枚举 `PresentationIntervalEnum` 由
`Engine.Windowing` 管理；基础数学、集合、日志和运行模式由 Core 的对应子命名空间提供。

模型运行时由共享的 `Model`、只读 `ModelNode`、共享几何 `ModelMesh`/`ModelPrimitive` 和
每实例的 `ModelPose` 构成。场景节点与网格资源名称分开，节点可共享几何；不同默认形变权重
使用独立的几何变体。资源释放和当前姿态互不混用，具体约定见 [模型与 glTF](../Doc/Models.md)。

通用子纹理、纹理图集、路径/斜面绘图及基础天空 shader 位于 `Engine.Graphics`。
依赖游戏相机、内容管理器或世界状态的渲染与缓存仍属于游戏模块，不由引擎反向加载。
