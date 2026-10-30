# Engine.Media

不依赖窗口、GPU、音频设备或游戏文件存储的媒体数据与编解码库，供游戏、模组和内容工具共同使用。
目录与命名空间按职责对应：

| 目录 / 命名空间 | 职责 |
| --- | --- |
| `Images/` / `Engine.Media.Images` | 图像、像素缓冲、格式识别及编解码入口 |
| `Images/Formats/` / `Engine.Media.Images.Formats` | 内部 PNG、WebP、BMP 格式实现，不作为公共调用入口 |
| `Images/Native/` / `Engine.Media.Images.Native` | 内部原生图像编解码绑定 |
| `Audio/` / `Engine.Media.Audio` | 音频数据、流式解码入口及音频内部的探测流 |
| `Audio/Formats/` / `Engine.Media.Audio.Formats` | WAV、Ogg、FLAC、MP3 具体格式接口 |
| `Models/` / `Engine.Media.Models` | 标准 glTF 场景、节点、网格、访问器、材质、蒙皮与动画数据 |
| `Models/Formats/` / `Engine.Media.Models.Formats` | 内部 glTF/GLB 读取实现 |

图像支持 PNG、WebP 编解码及 BMP 读写；`Image` 与 `ImageBuffer` 使用 `Engine.Core.Mathematics.Color`
连续 RGBA8 数组，采用非预乘 Alpha。音频支持 WAV、Ogg、FLAC、MP3 读取和流式解码，
以及 WAV 写入；不负责音频播放。模型数据不创建 GPU 缓冲。顶点声明、元素格式与语义属于 `Engine.Graphics`，
由图形层在准备渲染数据时使用；媒体库只保留模型文件中的访问器与几何数据。

## 图像 API

图像格式统一使用 `ImageFormat`（`Png`、`WebP`、`Bmp`）。`ImageCodec` 提供公共识别、元数据与编解码入口：

- `DetectFormat` 根据扩展名或编码字节识别格式；扩展名识别不验证文件内容。
- `Identify` 返回统一的 `ImageInfo`：尺寸、文件格式和帧信息，不暴露各格式的源色型枚举；WebP 只以大于 1 的值标记动画，不枚举实际帧数。
- `Decode` 返回顶行优先、非预乘 Alpha 的 RGBA8 `ImageBuffer`；可指定预期格式，不匹配时拒绝读取。
- `Encode` 根据格式编码，`saveAlpha=false` 输出不透明像素，不修改源缓冲。

`Image.Load/Save` 是同一条编解码路径的对象便捷入口。三种格式都接受非定位流和短读流；
`Identify(Stream)` 在流可定位时恢复原位置，否则消耗输入；解码从当前位置读取并消耗编码数据。
图像文件保持普通直通 Alpha；图形层在纹理上传时自动预乘，媒体解码不猜测或改变 Alpha 表示。
这些方法不关闭调用方传入的流。PNG 和 WebP 使用原生库，BMP 支持未压缩的 24/32 位像素、
四字节行对齐和正负高度。动画帧可被识别，但不能解码为单张图像。

API 接受 `Stream`，调用方负责打开文件和解析游戏存储路径。纹理、模型上传、声音播放和
`BitmapFont` 留在 `Engine` 的 Graphics/Audio 层。托管依赖包括 Engine.Core、
NAudio、NLayer、NVorbis 和 SharpGLTF.Core；图像原生库由独立的原生资产包提供。

原生图像库的版本、来源及重编译步骤见
[Native.ImageCodecs](https://gitee.com/Yunus0712/scnet/blob/main/Native.ImageCodecs/README.md)。

## 分发

原生库集中位于 `../Native.ImageCodecs/runtimes/<rid>/native`，由 `SCNET.Native.ImageCodecs`
按标准 RID 资产打包。Media 通过 PackageReference 依赖该包；源码项目引用和 NuGet 消费
都能传递原生依赖，应用无需导入复制 props。

ContentTool 将完整运行输出打入工具目录，包含托管依赖、原生库和 `.deps.json`，可独立执行。
Android 图像库通过原生包依赖进入 APK；OpenAL 则通过 Engine 的统一 OpenAL 原生包依赖提供。

许可声明统一维护在根目录的 `ThirdPartyNotices.txt`，随 NuGet、桌面发布包和 APK 分发；
更新依赖时需同步更新。

游戏截图统一保存为不含透明通道的 PNG。媒体接口不提供 JPEG 或 TIFF 编解码。

模型数据、资源解析和渲染能力边界见 [模型与 glTF](../Doc/Models.md)。

WAV 的 RIFF/PCM 头和样本采用格式本地的小端读写，不依赖通用归档框架。
支持单/双声道 16 位 PCM、扩展 fmt 块和带填充的未知块，拒绝声明越界或截断的数据；
检测与普通加载/保存不接管调用方的流，流式读取的所有权由 `leaveOpen` 指定。
