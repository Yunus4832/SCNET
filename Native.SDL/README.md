# SCNET.Native.SDL

SDL2 预编译原生资产，由 Engine 通过 NuGet 引用。普通 `dotnet build` 不编译 C/C++。

| RID | SDL 版本 | 文件 |
| --- | --- | --- |
| `linux-x64` | 2.32.10 | `libSDL2-2.0.so` |
| `win-x64` | 2.32.10 | `SDL2.dll` |
| `android-arm`、`android-arm64`、`android-x86`、`android-x64` | 2.32.10 | `libSDL2.so`、`libmain.so` |

所有平台统一使用 SDL 2.32.10。Android 的 Java 类也从同一份上游源码编译为
`buildTransitive/java/SDL2.jar`，以 `AndroidLibrary Bind="false"` 引入；托管绑定继续由
Silk.NET.Windowing.Sdl 2.23.0 提供。`bridge/main.c` 实现 SilkActivity 使用的
`sdSetMain` 和 `SDL_main` 两个入口，保留 Silk 原有的 Android 启动流程。

`buildTransitive/SCNET.Native.SDL.targets` 在 Android SDK 提取 AAR 后，识别带有 SDL2
原生库的原 AAR，移除其 `ExtractedJarImports` 中的 Java JAR 和 `AndroidNativeLibrary`
中的原 SDL2 和 main 库，由新的 Java JAR 和标准 RID 原生资产替换。该 hook 依赖 .NET Android
SDK 的 `_GetLibraryImports`、`ExtractedJarImports` 和 `ExtractedNativeLibraryImports`；
升级 SDK/Silk/SDL 时须检查 Java 绑定签名、最终 APK 没有重复或旧版本，并完成启动、输入与渲染验证。
桌面端在 `ResolvePackageAssets` 后按 `NuGetPackageId` 移除 `Ultz.Native.SDL` 的原生复制项，
同时覆盖有 RID 和无 RID 的消费项目。仅在 Engine 的 PackageReference 设置 ExcludeAssets
不会排除上层应用重新解析出的传递原生资产，因此替换逻辑统一放在 buildTransitive target。

## 重编译

从 <https://www.libsdl.org/release/> 获取源码：

```text
5f5993c530f084535c65a6879e9b26ad441169b3e25d789d83287040a9ca5165  SDL2-2.32.10.tar.gz
```

Android Java 部分使用 JDK 21（Java 8 字节码）和 Android SDK 35；原生部分使用 NDK r29（29.0.14206865）、API 23，两个共享库均设置 16 KiB ELF 段对齐：

```bash
SDL_SOURCE=/path/to/SDL2-2.32.10 ANDROID_NDK_ROOT=/path/to/android-ndk-r29 \
    ANDROID_JAR=/path/to/android-sdk/platforms/android-35/android.jar \
    bash Native.SDL/Scripts/build-sdl-android.sh
```

Linux 使用 Ubuntu 22.04 构建环境，安装 `build-essential cmake ripgrep pkg-config`
以及 X11、Wayland、xkbcommon、ALSA、PulseAudio、udev、DRM、GBM、EGL/OpenGL、DBus
开发包，保留桌面窗口和输入后端：

```bash
podman build -t scnet-sdl-linux -f Native.SDL/Scripts/Dockerfile.linux .
podman run --rm -v "$PWD:/repo:Z" -v /path/to/SDL2-2.32.10:/source:ro,Z \
    -e SDL_SOURCE=/source scnet-sdl-linux \
    bash /repo/Native.SDL/Scripts/build-sdl-desktop.sh linux-x64
```

Windows 使用 LLVM-MinGW 20261006 UCRT x86_64 工具链，从 Linux 交叉编译：

```bash
SDL_SOURCE=/path/to/SDL2-2.32.10 SDL_WINDOWS_TOOLCHAIN=/path/to/llvm-mingw \
    bash Native.SDL/Scripts/build-sdl-desktop.sh win-x64
```

脚本需要 Bash、CMake、Make 和 ripgrep。先验证上游源码压缩包 SHA256，再解压到独立目录；
不要修改上游源码。成功后替换对应 RID 的资产并更新 `SHA256SUMS`；失败保留临时构建日志。

更新资产后执行 `bash Scripts/pre-build.sh --refresh`，或使用 PowerShell 对应脚本，
刷新本地 NuGet 包。检查 Android APK 中两个库的哈希、ELF 段对齐与 ZIP 对齐；
在 16 KiB 页设备/模拟器上运行才能完成该页大小的运行时验收。

SDL 使用 zlib 许可证，见仓库 `ThirdPartyNotices.txt`。
