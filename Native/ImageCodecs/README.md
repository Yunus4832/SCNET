# SCNET.Native.ImageCodecs

提供 PNG、WebP 及桌面 WebP 所需的 SharpYUV。Engine.Media 通过 NuGet 引用，应用按 RID 使用
`runtimes/<rid>/native` 中的 Linux x64、Windows x64 或 Android 资产。日常构建直接使用预编译库。

重编译命令从仓库根目录执行。脚本在临时目录构建，可通过 `TMPDIR` 指定位置。成功后更新选定 RID 的
资产和 `SHA256SUMS`，失败时保留日志。用 `IMAGE_CODEC_SOURCES` 指定已准备的源码目录，默认是
`~/Desktop/code`。更新资产后按[本地打包](../../Doc/NuGet.md#本地打包)刷新 NuGet 并验证目标平台。

## 原生库来源

| 库 | 桌面 | Android |
| --- | --- | --- |
| libpng | 自编译 1.6.59 | 四个 ABI 自编译 1.6.59 |
| libwebp | Imazen.WebP.NativeRuntime 的 Linux x64 / Windows x64 1.6.0 产物 | 1.6.0，四个 ABI 补编译 |

Android 使用 NDK r29（29.0.14206865）、API 23。PNG 的 zlib 1.3.2 与 WebP 的
SharpYUV 静态链接进对应共享库，应用不需要额外部署这些 Android 依赖。
桌面 WebP 的 SharpYUV 单独部署并预加载。Windows WebP 产物还依赖
Microsoft Visual C++ 2015–2022 x64 Redistributable（VCRUNTIME140.dll）；该系统运行库
不是本项目携带的文件，Windows 部署时需确保已安装。项目支持的桌面 RID 为 linux-x64 和 win-x64。

## 重编译 Android

准备以下源码仓库及其版本标签，脚本会检查干净工作区并切换到对应标签：

- https://github.com/pnggroup/libpng （v1.6.59）
- https://github.com/webmproject/libwebp （v1.6.0）
- https://github.com/madler/zlib （v1.3.2）

Linux 构建机需要 Git、CMake、Make 和 Android NDK：

```bash
IMAGE_CODEC_SOURCES=/path/to/sources \
ANDROID_NDK_ROOT=/path/to/ndk/29.0.14206865 \
./Native/ImageCodecs/Scripts/build-image-codecs-android.sh
```

四种 ABI 全部构建成功后，脚本自动将 libpng.so、libwebp.so
复制到本项目 `runtimes/<rid>/native` 并更新 `SHA256SUMS`；不复制未使用的 demux/mux 库。
之后仍需核对 ELF 依赖、64 位的 16 KiB 页面对齐、导出函数，并验证 APK 打包与实际加载。

## 重编译桌面 PNG

使用相同的 libpng 和 zlib 源码版本，从 Linux 构建 Linux x64：

```bash
IMAGE_CODEC_SOURCES=/path/to/sources \
./Native/ImageCodecs/Scripts/build-libpng-zlib-desktop.sh linux-x64
```

Windows x64 使用 LLVM-MinGW UCRT 交叉编译。本次验证的工具链为
`llvm-mingw-20261006-ucrt-ubuntu-22.04-x86_64`：

```bash
IMAGE_CODEC_SOURCES=/path/to/sources \
IMAGE_CODEC_WINDOWS_TOOLCHAIN=/path/to/llvm-mingw \
./Native/ImageCodecs/Scripts/build-libpng-zlib-desktop.sh win-x64
```

PNG 的 zlib 静态链接进共享库，Windows PNG 不需要单独部署 zlib1.dll。
编译工具链的许可证也包含在解决方案根目录的 `ThirdPartyNotices.txt` 中。

校验值见 `SHA256SUMS`；许可声明统一维护在解决方案根目录 `ThirdPartyNotices.txt`。
打包入口见 [NuGet 文档](https://gitee.com/Yunus0712/scnet/blob/main/Doc/NuGet.md#本地打包)。
