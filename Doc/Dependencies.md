# 外部依赖

## 平台与工具链

- [.NET 10 SDK](https://dotnet.microsoft.com/zh-cn/download/dotnet/10.0)
- Visual Studio 2022+ 或 Rider
- Android SDK
- Linux 桌面运行时需要可用的图形桌面环境

## 运行时和第三方包

代码库当前显式引用的主要外部包包括：

- `LiteNetLib`：网络通信
- `Newtonsoft.Json`：JSON 读写
- `MessagePack`：实体系统相关序列化
- `NAudio.Core`、`NAudio.Flac.Unknown`、`NLayer.NAudioSupport`、`NVorbis`：音频解码与播放
- `Silk.NET.OpenAL`、`Silk.NET.OpenGLES`、`Silk.NET.OpenGL`、`Silk.NET.Input`、`Silk.NET.Windowing`：窗口、输入与图形抽象
- `Silk.NET.OpenAL.Soft.Native`：桌面端音频补充
- `Silk.NET.SDL`：Android 平台窗口/输入适配
- `SixLabors.ImageSharp`：图片处理
- `Xamarin.AndroidX.Core`：Android 兼容支持

## 测试依赖

- `Microsoft.NET.Test.Sdk`
- `xunit`
- `xunit.runner.visualstudio`
- `coverlet.collector`
