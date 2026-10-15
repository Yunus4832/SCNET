# 外部依赖

## 平台与工具链

- [.NET 10 SDK](https://dotnet.microsoft.com/zh-cn/download/dotnet/10.0)
- Visual Studio 2022+ 或 Rider
- Android SDK
- Linux 桌面运行时需要可用的图形桌面环境
- Windows WebP 编解码需要 Microsoft Visual C++ 2015–2022 x64 Redistributable

## 运行时和第三方包

代码库当前显式引用的主要外部包包括：

- `LiteNetLib`：网络通信
- `MessagePack`：实体系统相关序列化
- `NAudio.Core`、`NAudio.Flac.Unknown`、`NLayer.NAudioSupport`、`NVorbis`：Engine.Media 音频解码；设备播放由 Engine 的 OpenAL 实现
- `Silk.NET.OpenAL`、`Silk.NET.OpenGLES`、`Silk.NET.OpenGL`、`Silk.NET.Input`、`Silk.NET.Windowing`：窗口、输入与图形抽象
- `Silk.NET.OpenAL.Soft.Native`：桌面端音频补充
- `SCNET.Native.OpenAL.Android`：Android 四种 ABI 的 OpenAL Soft 原生资产，仅由 Engine 的 Android 目标引用
- `Silk.NET.SDL`：Android 平台窗口/输入适配
- 图像编解码采用项目内的 P/Invoke 薄绑定；`Engine.Media` 使用 libpng、libwebp 与 libjpeg-turbo 编解码图像，公共 API 位于 `Engine.Media`。内容服务器图片上传在解析元数据前限定为 PNG；游戏支持 PNG、JPEG、WebP 和 BMP，不提供 TIFF 入口。
- 原生图像库的版本、来源和构建说明见 [Native.ImageCodecs](../Native.ImageCodecs/README.md)。
- `Svg.Skia`、`SkiaSharp`：构建期 SVG 栅格化、PNG 处理与 GUI 图集生成
- `Xamarin.AndroidX.Core`：Android 兼容支持

## 测试依赖

- `Microsoft.NET.Test.Sdk`
- `xunit`
- `xunit.runner.visualstudio`
- `coverlet.collector`
