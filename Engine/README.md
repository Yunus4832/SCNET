# SCNET.Engine

SCNET 的跨平台引擎运行时，提供图形、窗口、输入、音频、媒体与存储能力。
支持 .NET 10 和 .NET 10 for Android；具体平台启动由应用项目负责。

依赖基础类型、序列化与媒体模块。图形、窗口和输入使用 Silk.NET；音频播放通过 OpenAL。
独立的 `SCNET.Engine.Media` 负责图像、音频和模型编解码，工具或模组可直接使用其流接口。

- 图像原生库由 `SCNET.Engine.Media` 的包依赖提供。
- Android OpenAL 由 `SCNET.Native.OpenAL.Android` 提供。
- 桌面 OpenAL、SDL 继续由对应第三方包提供。
- 消费应用无需声明这些原生文件的复制路径或 ABI 映射。

本包不包含游戏规则或模组构建工具。编写游戏模组通常引用 `SCNET.Survivalcraft` 即可。

[依赖说明](https://gitee.com/Yunus0712/scnet/blob/main/Doc/Dependencies.md)
