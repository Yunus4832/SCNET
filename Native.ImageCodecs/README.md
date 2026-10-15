# SCNET.Native.ImageCodecs

图像原生资产打包项目，包含 PNG、JPEG、WebP 及桌面 WebP 所需的 SharpYUV。
按 `runtimes/<rid>/native` 存放，支持 Linux x64、Windows x64 和 Android 四种 ABI。
Engine.Media 引用此包，应用通过 NuGet 标准 RID 机制自动选择和部署原生资产。
本项目不包含 OpenAL，不分发托管程序集。`dotnet build/pack` 不触发 C/C++ 编译。

仓库中的原生库是已经验证可用的稳定预编译产物。日常开发、首次构建、打包和发布直接使用
这些文件，无需安装 NDK 或重新编译原生库。只有升级原生库版本，或明确需要修复、补充
原生产物时，才运行以下构建脚本并更新 `runtimes`。重新编译本身不代表需要替换现有稳定产物；
替换前应完成对应平台的验证，并同步校验值和许可声明。

项目专用的构建脚本位于本项目的 `Scripts/`，以下命令从仓库根目录执行。它们使用
`mktemp` 在系统临时目录构建，可通过标准 `TMPDIR` 选择临时目录位置。构建成功后自动将
选定 RID 的产物放入本项目 `runtimes`、更新整个项目的 `SHA256SUMS`，并清理临时目录。
构建失败时保留临时目录并输出路径，供检查日志；构建失败不会复制未完成的产物。
源码仓库由 `IMAGE_CODEC_SOURCES` 指定，默认 `~/Desktop/code`；脚本不克隆源码。

## 原生库来源

| 库 | 桌面 | Android |
| --- | --- | --- |
| libpng | 自编译 1.6.59 | 四个 ABI 自编译 1.6.59 |
| libwebp | Imazen.WebP.NativeRuntime 的 Linux x64 / Windows x64 1.6.0 产物 | 1.6.0，四个 ABI 补编译 |
| libjpeg-turbo | Windows x64 使用 turbojpeg-VC.Redist 3.1.1；Linux x64 编译 3.2.0 | 3.2.0，四个 ABI 补编译 |

Android 使用 NDK r29（29.0.14206865）、API 23。PNG 的 zlib 1.3.2 与 WebP 的
SharpYUV 静态链接进对应共享库，应用不需要额外部署这些 Android 依赖。
桌面 WebP 的 SharpYUV 单独部署并预加载。Windows WebP 产物还依赖
Microsoft Visual C++ 2015–2022 x64 Redistributable（VCRUNTIME140.dll）；该系统运行库
不是本项目携带的文件，Windows 部署时需确保已安装。Linux JPEG 使用 C17 编译，当前产物的
最高 glibc 符号要求为 2.14。项目支持的桌面 RID 为 linux-x64 和 win-x64。

PNG、JPEG、WebP 均使用项目内的薄 P/Invoke 绑定；PNG 的所有原生库统一为 1.6.59。

## 重编译 Android

准备以下源码仓库及其版本标签，脚本会检查干净工作区并切换到对应标签：

- https://github.com/pnggroup/libpng （v1.6.59）
- https://github.com/webmproject/libwebp （v1.6.0）
- https://github.com/libjpeg-turbo/libjpeg-turbo （3.2.0）
- https://github.com/madler/zlib （v1.3.2）

Linux 构建机需要 Git、CMake、Make、NASM 和 Android NDK：

```bash
IMAGE_CODEC_SOURCES=/path/to/sources \
ANDROID_NDK_ROOT=/path/to/ndk/29.0.14206865 \
./Native.ImageCodecs/Scripts/build-image-codecs-android.sh
```

四种 ABI 全部构建成功后，脚本自动将 libpng.so、libwebp.so、libturbojpeg.so
复制到本项目 `runtimes/<rid>/native` 并更新 `SHA256SUMS`；不复制未使用的 demux/mux 库。
之后仍需核对 ELF 依赖、64 位的 16 KiB 页面对齐、导出函数，并验证 APK 打包与实际加载。

## 重编译桌面 PNG

使用相同的 libpng 和 zlib 源码版本，从 Linux 构建 Linux x64：

```bash
IMAGE_CODEC_SOURCES=/path/to/sources \
./Native.ImageCodecs/Scripts/build-libpng-zlib-desktop.sh linux-x64
```

Windows x64 使用 LLVM-MinGW UCRT 交叉编译。本次验证的工具链为
`llvm-mingw-20261006-ucrt-ubuntu-22.04-x86_64`：

```bash
IMAGE_CODEC_SOURCES=/path/to/sources \
IMAGE_CODEC_WINDOWS_TOOLCHAIN=/path/to/llvm-mingw \
./Native.ImageCodecs/Scripts/build-libpng-zlib-desktop.sh win-x64
```

PNG 的 zlib 静态链接进共享库，Windows PNG 不需要单独部署 zlib1.dll。
编译工具链的许可证也包含在解决方案根目录的 `ThirdPartyNotices.txt` 中。

Linux JPEG 的等价构建参数为 `-DCMAKE_C_STANDARD=17 -DCMAKE_BUILD_TYPE=Release
-DENABLE_SHARED=ON -DENABLE_STATIC=OFF`，构建目标为 `turbojpeg`。不同编译环境可能
提高 glibc 要求，需要重新检查符号版本，不能仅根据成功编译认定可部署。

校验值见 `SHA256SUMS`；许可声明统一维护在解决方案根目录 `ThirdPartyNotices.txt`。
打包入口见 [NuGet 文档](https://gitee.com/Yunus0712/scnet/blob/main/Doc/NuGet.md#本地打包)。
