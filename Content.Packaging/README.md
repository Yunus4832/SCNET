# SCNET.Content.Packaging

SCNET `.scpkg` 内容包的协议、读写与校验库，面向 .NET 10，命名空间为 `Content.Packaging`。

- `ContentPackageManifest`：内容类型、标识、版本、简介和说明。
- `ContentPackageReader.Inspect`：读取包、校验结构与载荷，并计算内容哈希。
- `ContentPackageWriter`：写入符合协议的内容包。
- `Content.Packaging.Payloads`：模组、世界、图片、家具包等载荷校验。

依赖 `SCNET.Engine.Media` 处理图像载荷，原生编解码依赖由包依赖自动传递；
不依赖游戏、窗口或 ContentServer。普通使用方无需自行复制图像原生库。

需要命令行打包时使用 `SCNET.ContentTool`，而不是直接运行此类库。

[内容包协议](https://gitee.com/Yunus0712/scnet/blob/main/Doc/Protocol/ContentPackageProtocol.md)
