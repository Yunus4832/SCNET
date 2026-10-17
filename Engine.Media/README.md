# Engine.Media

不依赖窗口、GPU、音频设备或游戏文件存储的媒体数据与编解码库。公共类型位于
`Engine.Media` 命名空间，供游戏、模组和内容工具共同使用：

- 图像：PNG、WebP 编解码及 BMP 读写；`Image` 与 `ImageBuffer` 使用
  `Engine.Core.Color` 连续 RGBA8 数组，采用非预乘 Alpha。
- 音频：WAV、Ogg、FLAC、MP3 读取和流式解码，WAV 写入；不负责音频播放。
- 模型：Collada 解析、骨骼、网格和顶点数据描述；不创建 GPU 缓冲。

API 接受 `Stream`，调用方负责打开文件和解析游戏存储路径。纹理、模型上传、声音播放和
`BitmapFont` 留在 `Engine` 的 Graphics/Audio 层。托管依赖包括 Engine.Core、
Engine.Serialization、NAudio、NLayer 和 NVorbis；图像原生库由独立的原生资产包提供。

原生图像库的版本、来源及重编译步骤见
[Native.ImageCodecs](https://gitee.com/Yunus0712/scnet/blob/main/Native.ImageCodecs/README.md)。

## 分发

原生库集中位于 `../Native.ImageCodecs/runtimes/<rid>/native`，由 `SCNET.Native.ImageCodecs`
按标准 RID 资产打包。Media 通过 PackageReference 依赖该包；源码项目引用和 NuGet 消费
都能传递原生依赖，应用无需导入复制 props。

ContentTool 将完整运行输出打入工具目录，包含托管依赖、原生库和 `.deps.json`，可独立执行。
Android 图像库通过原生包依赖进入 APK；OpenAL 则通过 Engine 的 Android 专用包依赖提供。

许可声明统一维护在根目录的 `ThirdPartyNotices.txt`，随 NuGet、桌面发布包和 APK 分发；
更新依赖时需同步更新。

游戏截图统一保存为不含透明通道的 PNG。媒体接口不提供 JPEG 或 TIFF 编解码。
