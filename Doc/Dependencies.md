# 外部依赖

## 平台与工具链

- [.NET 10 SDK](https://dotnet.microsoft.com/zh-cn/download/dotnet/10.0)
- 支持 .NET 10 SDK 和所用 Android 工作负载的 IDE（例如 Rider）；以 IDE 对目标 SDK 的支持为准
- Android SDK
- Node.js 与 npm：构建 ContentServer/Web 工作台前，在仓库根目录执行 `npm --prefix ContentWebUI ci` 安装前端依赖；完整解决方案构建会调用 `npm run build`，详见 [Web 工作台](../ContentWebUI/README.md)
- Linux 桌面运行时需要可用的图形桌面环境
- Windows WebP 编解码需要 Microsoft Visual C++ 2015–2022 x64 Redistributable

## 运行时和第三方包

代码库当前显式引用的主要外部包包括：

- `LiteNetLib`：可靠 UDP 网络通信
- `BouncyCastle.Cryptography`：Engine.Network 的 DTLS 1.2 握手与 ChaCha20-Poly1305 记录保护
- `MessagePack`：实体系统相关序列化
- `SharpGLTF.Core`：标准 glTF 2.0 / GLB 解析与格式验证；场景数据及渲染边界见 [模型与 glTF](Models.md)
- `NAudio.Core`、`NAudio.Flac.Unknown`、`NLayer.NAudioSupport`、`NVorbis`：Engine.Media 音频解码；设备播放由 Engine 的 OpenAL 实现
- `Silk.NET.OpenAL`、`Silk.NET.OpenGLES`、`Silk.NET.OpenGL`、`Silk.NET.Input.Common`、`Silk.NET.Input.Sdl`、`Silk.NET.Windowing.Common`、`Silk.NET.Windowing.Sdl`：窗口、输入与图形抽象
- `SCNET.Native.OpenAL`：Windows、Linux 和 Android 四种 ABI 的 OpenAL Soft 原生资产，由 Engine 统一引用
- `Silk.NET.SDL`：桌面与 Android 的 SDL 平台适配
- 图像编解码采用项目内的 P/Invoke 薄绑定；`Engine.Media` 使用 libpng 与 libwebp 编解码图像，图像公共 API 位于 `Engine.Media.Images`，具体格式接口位于 `Engine.Media.Images.Formats`。内容服务器图片上传在解析元数据前限定为 PNG；游戏支持 PNG、WebP 和 BMP，不提供 TIFF 入口。
- 原生图像库的版本、来源和构建说明见 [Native.ImageCodecs](../Native.ImageCodecs/README.md)。
- `Svg.Skia`、`SkiaSharp`：构建期 SVG 栅格化、PNG 处理与 GUI 图集生成
- `Xamarin.AndroidX.Core`：Android 兼容支持

## 测试依赖

- `Microsoft.NET.Test.Sdk`
- `xunit`
- `xunit.runner.visualstudio`
- `coverlet.collector`
