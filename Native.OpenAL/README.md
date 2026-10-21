# SCNET.Native.OpenAL

统一提供 OpenAL Soft 1.25.2 动态库，支持 linux-x64、win-x64，以及
android-arm、android-arm64、android-x86、android-x64。原生资产位于
`runtimes/<rid>/native`，由 Engine 引用本包，通过 NuGet 按应用 RID 选择。
Engine 沿用 Silk.NET 默认原生库加载方式，不强制选择随包版本；实际加载的库也可能来自系统。
托管绑定仍使用 `Silk.NET.OpenAL`；本项目不包含托管程序集，也不在 .NET 构建时编译 C/C++。

仓库中的库是预编译产物，日常构建和发布直接复用。只有升级版本或明确修复、补充产物时
才重新编译；替换前验证相应平台，并更新 `SHA256SUMS` 和根目录 `ThirdPartyNotices.txt`。
上游源码：https://github.com/kcat/openal-soft/tree/1.25.2
打包入口见 [NuGet 文档](../Doc/NuGet.md#本地打包)。

## 桌面库重编译

准备干净的上游 `1.25.2` 标签检出，设置 `OPENAL_SOURCE` 为源码目录。
在 Linux 上使用 CMake、Make、patch 和支持 C++20 的编译器运行：

```bash
OPENAL_SOURCE=/path/to/openal-soft bash Native.OpenAL/Scripts/build-openal-desktop.sh linux-x64
OPENAL_SOURCE=/path/to/openal-soft OPENAL_WINDOWS_TOOLCHAIN=/path/to/llvm-mingw \
    bash Native.OpenAL/Scripts/build-openal-desktop.sh win-x64
```

脚本在临时源码副本中应用 `Scripts/unique-ptr-transform.patch`，修正新 libc++ 对
`unique_ptr::operator*` 成员指针推导的歧义，使用等价 lambda，不改变音频逻辑。
脚本在临时目录编译，成功后将库放入项目对应 RID 目录并更新校验值；失败时保留临时目录及日志。
`OPENAL_BUILD_JOBS` 可调整并行数，默认 4。不会使用宿主机特定的 `-march=native` 优化。

Linux 使用 Ubuntu 22.04 构建环境、GCC 12、CMake、Make、patch、Git 和 PipeWire/PulseAudio/ALSA
开发头文件，运行时动态加载这些音频后端；GCC 运行库静态链接，避免额外的 libstdc++ 版本要求。
不要直接在新发行版上构建后假设兼容旧版 glibc。Windows 使用 LLVM-MinGW UCRT 工具链，
静态链接编译器运行库，避免额外分发 libc++/libunwind DLL；保留 WASAPI 后端；将上游默认的 `OpenAL32.dll` 命名为 Silk.NET 加载的 `soft_oal.dll`。

## Android 库重编译

使用 NDK `29.0.14206865`、CMake、Make 和 Git，在 Linux 上运行：

```bash
OPENAL_SOURCE=/path/to/openal-soft \
OBOE_SOURCE=/path/to/oboe \
ANDROID_NDK_ROOT=/path/to/android-ndk \
    bash Native.OpenAL/Scripts/build-openal-android.sh
```

Oboe 固定为原有提交 `5714d5362afac77778558e2ba9800b0f53a3f6ea`，
来源：https://github.com/google/oboe/tree/5714d5362afac77778558e2ba9800b0f53a3f6ea 。
脚本要求两个源码目录均为干净检出，依次编译四种 ABI，最低 API 23，静态链接 Oboe 和 C++ 运行库，
保留 Oboe、OpenSL ES 后端，ELF 段按 16 KiB 对齐。
32 位 ABI 关闭 NDK 29 对 `consteval` 大小常量转换误报的 `-Wfunction-effects` 构建检查，
不修改音频处理逻辑或运行时检查。四种 ABI 全部构建成功后才替换项目资产并更新校验值。
构建在临时目录进行，失败时保留日志。普通构建和本地 NuGet 准备不需要安装 NDK。
