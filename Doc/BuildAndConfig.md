# 构建与共享配置

## 共享配置

仓库将许多跨项目约定集中到几个共享文件里，避免每个工程重复配置。对于联机项目而言，这有助于降低跨平台配置不一致的风险。

### `Directory.Build.props`

这是全局构建属性入口，主要定义了：

- `GlobalTargetFramework`
- `AndroidTargetFramework`
- `MinAndroidApiVersion`
- `AndroidVersionCode`（发布新版 APK 时递增，且同一签名密钥保持不变）
- `LangVersion`
- `Nullable`
- `WarningsAsErrors`
- `ImplicitUsings`
- 解决方案级别的 `Configurations`

这些属性会被大多数项目自动继承，所以单个项目里通常只需要写差异部分。

### `Directory.Build.targets`

这是全局构建目标入口，主要负责：

- 在资源打包前根据独立 SVG 源文件增量生成 GUI 图集
- 把 `Content/` 打成 `Content.zip`
- 在 `Publish` 后打包桌面发布输出
- 在 Android 构建后重命名 APK

也就是说，资源打包和发布产物的处理不再分散在各个项目中，而是统一由这里接管。

### `SharedProperties/GlobalUsing.props`

这是全局 `using` 的集中入口。它为许多常用命名空间做了隐式导入，例如：

- `Engine.Core`
- `Engine.FileStorage`
- `Engine.Windowing`
- `Game.Managers`
- `Game.Screens`
- `Game.Subsystems`
- `Game.Components`
- `Game.ModManager`

这也是仓库里许多代码文件可以省略大量 `using` 声明的原因——目的是降低共享代码中的重复成本。

### `SharedProperties/Android.props`

这是 Android 相关的共享属性，集中定义了：

- `ApplicationId`
- 签名相关配置
- `AndroidDexTool`
- `AndroidPackageFormat`
- `AndroidStripILAfterAOT`
- `AndroidUseInterpreter`
- `EnableLLVM`
- `RunAOTCompilation`
- `AndroidLinkMode`
- `LinkAll`

Android 的几个变体项目会复用这份配置，只在自己的 `.csproj` 里覆盖架构、输出名和引用方式。这样一来就可以集中管理移动端的构建差异。

### `SharedProperties/AndroidApplication.props`

这是 Android 可运行启动器的公共项目配置，集中定义目标框架、程序集名称、最低系统版本、
`Content.zip` 资产声明、共享 Manifest 和全局 `using`。每个架构项目仍显式声明自己的
`RuntimeIdentifier`、APK 名称，并在 `ProjectReference` 中硬编码相同 RID，以避免 Rider 设计时
构建和 MSBuild 项目引用求值产生架构偏差。

公共 Manifest 位于 `SharedProperties/AndroidManifest.xml`，所有 Android 启动器使用同一个模板。
应用通过限制到缓存 `updates/` 子目录的 FileProvider 把已校验 APK 交给 Android 系统安装器；各架构变体必须链接同一份 `update_paths.xml`。
架构项目不应保存独立副本；需要平台级声明时修改公共模板，需要真正的架构差异时再使用条件配置
或 Manifest Overlay。

### `SharedProperties/AndroidVariant.props`

Arm32、x64 和 x86 变体通过这份配置链接 `Survivalcraft.Android/` 中的公共启动器源码与 Android
资源。各变体继续保留独立的项目、`bin/obj`、`Content.zip` 和对应 ABI 的 OpenAL 库，使 Rider
可以直接选择匹配设备架构的项目运行，同时避免不同 RID 的中间产物互相污染。

## 构建

```bash
# 首次构建：准备本地 NuGet 包并还原依赖
./Scripts/pre-build.sh

# Debug 构建
dotnet build SCNET.slnx

# Release 构建
dotnet build SCNET.slnx --configuration Release

# Linux/macOS：顺序生成应用和服务发行产物
./Scripts/publish.sh

# PowerShell：顺序生成应用和服务发行产物
./Scripts/publish.ps1
```

`pre-build.sh` 检查当前版本的完整本地包集合。缺包时调用 `pack-nuget.sh` 按依赖顺序生成到
`Publish/NuGet`，然后还原解决方案；包齐全时直接还原。PowerShell 使用 `./Scripts/pre-build.ps1`。
脚本完成后再执行 `dotnet build`，或从 IDE 构建、调试。普通构建不会自动打包。

修改原生资产、Mod SDK 或 ContentTool 后，即使版本号未变，也应执行
`./Scripts/pre-build.sh --refresh` 或 `./Scripts/pre-build.ps1 -Refresh`。重新打包前仅清除
本次 SCNET 包集合当前版本的 NuGet 缓存，避免旧 SDK 被再次使用；其他依赖缓存保留。
打包默认使用 Release，Bash 可通过 `CONFIGURATION` 环境变量调整，PowerShell 使用 `-Configuration`。
准备工作仍需要安装解决方案使用的 .NET SDK、Android 工作负载及相应构建依赖。

### 可选构建配置

- `Debug`
- `Release`

平台不再使用解决方案配置区分。开发、调试或验证单一平台时，直接选择对应的启动项目，
例如：

```bash
dotnet build Survivalcraft.Linux/Survivalcraft.Linux.csproj
dotnet build Survivalcraft.Android/Survivalcraft.Android.csproj
```

### 引擎数学基准

`Engine.Core.Benchmarks` 提供 Matrix 运算和批量向量变换的标量/SIMD 对照。基准不参与 Release
解决方案构建，也不使用第三方基准框架；运行时会报告进程架构、`Vector128` 硬件加速状态、两种实现
的中位耗时和倍率。应使用 Release 配置运行，并在目标 x64 或 ARM64 设备上分别记录结果：

```bash
dotnet run --configuration Release --project Engine.Core.Benchmarks/Engine.Core.Benchmarks.csproj
```

这些数字只衡量对应数学热点，不代表整帧或整服性能提升。修改 SIMD 实现时，还应运行
`Engine.Test` 中的逐位一致性测试。

### 配置语义

- `Debug` 和 `Release` 是通用配置，会走完整解决方案编译流程
- `Engine` 中的 Android 和桌面平台源码由目标框架选择，不依赖解决方案配置或手写的条件编译常量
- `Debug` 更适合开发调试，`Release` 更适合发布打包
- 测试工程在 `Release` 下会被排除，部分 Android 变体也会在 `Release` 下被排除
- 当前代码库大量使用反射，因此不启用 `trim`，也不支持通过裁剪方式进行发布优化

`Scripts/publish.sh` 和 `Scripts/publish.ps1` 会依次发布各发行入口，并为每个入口启动独立、串行的 `dotnet publish`，避免多目标共享项目在并行还原和发布时竞争中间文件。当前包含 Linux、Windows、Android Arm64、Android Arm32、ContentServer、RendezvousServer 和 NetworkProbe。各项目使用独立命令，便于以后配置平台专属参数；任一项目失败时，脚本会立即停止并返回失败。

不指定项目时发布全部。指定项目时只发布选中的入口；多个项目仍按脚本中的固定顺序发布。项目名称为 `Survivalcraft.Linux`、`Survivalcraft.Windows`、`Survivalcraft.Android`（Arm64）、`Survivalcraft.Android.Arm32`、`ContentServer`、`RendezvousServer` 和 `NetworkProbe`。例如：

```bash
./Scripts/publish.sh --project ContentServer
./Scripts/publish.sh --project Survivalcraft.Linux --project ContentServer
./Scripts/publish.sh --project ContentServer -- --no-restore
./Scripts/publish.sh --project RendezvousServer --project NetworkProbe
```

```powershell
./Scripts/publish.ps1 -Project ContentServer
./Scripts/publish.ps1 -Project Survivalcraft.Linux,ContentServer
```

Bash 脚本接受重复的 `--project` 或 `--project=名称`；`--` 后面的参数会传给所选项目的 `dotnet publish`。PowerShell 脚本使用 `-Project` 指定一个或多个项目，其他参数仍通过剩余参数传给 `dotnet publish`。

NuGet 包是另一条发行流程，不由应用发布脚本生成。使用 `Scripts/pack-nuget.sh` 或 `Scripts/pack-nuget.ps1`，具体包边界和使用方式见 [NuGet 包](./NuGet.md)。不要使用解决方案级 `dotnet publish` 或 `dotnet pack` 代替这些脚本；多目标共享项目应由下游入口按顺序独立处理。

根目录 `Publish` 中的桌面压缩包、重命名后的 Android APK、ContentServer 的 portable 和容器镜像包，以及 Linux 游戏服务器镜像包只在 `Publish` 阶段生成；普通 `Build` 不会更新这些发行产物。Linux 游戏服务器部署方式见 [Headless 模式](./Headless.md#容器部署)。

### 打包行为

仓库使用 `Directory.Build.targets` 统一处理资源和发布结果：

- `GuiAtlasTool/Assets/` 中任一 SVG、PNG 或 `GuiAtlasTool` 源码发生变化时，会先重新生成 GUI 图集
- 构建前会把 `Content/` 打成 `Content.zip`
- 桌面发布会将 `$(PublishDir)` 生成的发布输出压缩到仓库根目录下的 `Publish/` 目录
- Android 发布会生成并重命名 APK 到 `Publish/`

这也是为什么不同平台项目里会看到 `UsePackResourceTarget`、`UsePackOutputTarget` 和 `UseRenameApkTarget` 之类的属性。这些属性用于集中处理资源和发布产物。

### GUI 图集

`GuiAtlasTool/Assets/` 保存独立、透明且可直接渲染的 SVG 和 PNG。文件名就是图集条目名；例如
`PlayerList.svg` 会生成 `Textures/Atlas/PlayerList`。SVG 必须使用整数像素声明 `width` 和
`height`，并应提供对应的 `viewBox`；PNG 必须使用未预乘 Alpha 的 RGBA 数据。同一条目不能同时
存在 SVG 和 PNG。不要把 Inkscape 工程文件、隐藏设计图层或背景画布放入该目录。

`GuiAtlasTool` 会按尺寸和名称稳定排序源文件，自动排版、栅格化 SVG，并对所有条目统一预乘
Alpha。SVG 的缩小采样在预乘状态下完成，避免半透明边缘被压暗，然后生成：

- `Content/Assets/Atlases/AtlasTexture.png`
- `Content/Assets/Atlases/Atlas.txt`

这两个文件是忽略的构建产物，不应提交。使用以下命令可以手动重新生成：

```bash
dotnet run --project GuiAtlasTool/GuiAtlasTool.csproj -- generate \
  GuiAtlasTool/Assets \
  Content/Assets/Atlases/AtlasTexture.png \
  Content/Assets/Atlases/Atlas.txt
```

需要导入普通 PNG、反预乘旧 PNG，或从已有预乘图集提取条目时，使用 `GuiAtlasTool` 的
`prepare-png` 和 `extract` 命令；具体命令见 [`GuiAtlasTool/README.md`](../GuiAtlasTool/README.md)。

启用了 `UsePackResourceTarget` 的平台项目会在 `PackResource` 前自动执行相同流程。MSBuild 使用
SVG、PNG 和工具源码作为输入、图集文件作为输出，因此输入没有变化时不会重复生成。

### 信令服务与探针

`RendezvousServer` 和 `NetworkProbe` 发布为依赖 .NET 10 运行时的可移植 ZIP，分别为
`Publish/rendezvous-server-<版本>-portable.zip` 和 `Publish/network-probe-<版本>-portable.zip`。
信令服务 portable ZIP 需要 ASP.NET Core 运行时，探针需要 .NET 运行时；ZIP 不携带运行时或平台 apphost。
Linux 发布信令服务还生成 `Publish/rendezvous-server-<版本>-linux-amd64.tar.gz`，包含运行时镜像、Compose、
部署脚本和配置模板，需要本机容器引擎及 ASP.NET Core 基础镜像；其他平台跳过容器打包。
服务包含 `appsettings.json` 和 `Deployment/run.sh`；探针包含 `README.md`。
Agent 使用的本机、地址族隔离与 NAT 实验脚本集中在 `scnet-network-probe` 技能中，建议在 Linux
环境执行，不随探针发行包分发。
脚本使用 Python 3 标准库；隔离场景还需要文档中列出的 Linux 网络工具。配置、HTTPS/UDP 开放与探针使用见
[信令服务部署](RendezvousDeployment.md)。普通 `dotnet build` 不生成这些发行物。

### 桌面更新包

Windows/Linux 的便携 ZIP 包携带 `DesktopRelease.xml`，声明平台和由更新管理的根目录发行文件。
解决方案的 Desktop 分组包含共享库 `Desktop.UpdateTransactions` 和测试项目
`Desktop.UpdateTransactions.Test`。共享库使用命名空间 `Scnet.Desktop.Updates`，负责清单验证、暂存、进程交接、
文件更新事务、回滚和中断恢复，不依赖游戏或 Engine。Windows/Linux Starter 通过项目引用调用共享库，
平台文件替换和通知保留在各自项目中。游戏通过退出动作提交更新请求，由 Starter 在游戏运行结束后接管。类库随单文件发布合并，不额外发布更新器或依赖 shell/PowerShell。
当前 Linux 集合为 `SurvivalcraftStarter`、`Content.zip`、`libSDL2-2.0.so`、`libopenal.so`；Windows 集合为
`SurvivalcraftStarter.exe`、`Content.zip`、`SDL2.dll`、`soft_oal.dll`。两平台还包含各自的四个图像原生库和
`ThirdPartyNotices.txt`，具体文件见对应 `DesktopRelease.xml`。修改发行文件集合时须同步对应清单。

ContentServer 的 `linux-x64`/`windows-x64` 版本下载地址应指向完整便携 ZIP。客户端验证 SHA-256 后检查清单、平台与
ZIP 文件集合，拒绝目录穿越、符号链接、实例/配置覆盖及清单外文件。执行和恢复顺序见 [启动会话](./StartupSessions.md#桌面程序更新)。

清单使用 XML，`Files` 只列更新管理的根目录文件，清单自身由更新流程统一处理，不列入 `Files`：

```xml
<DesktopReleaseManifest>
    <Platform>linux-x64</Platform>
    <Files>
        <string>SurvivalcraftStarter</string>
        <string>Content.zip</string>
        <string>libSDL2-2.0.so</string>
        <string>libopenal.so</string>
        <string>libpng.so</string>
        <string>libwebp.so</string>
        <string>libsharpyuv.so</string>
        <string>libturbojpeg.so</string>
        <string>ThirdPartyNotices.txt</string>
    </Files>
</DesktopReleaseManifest>
```

更新事务的聚焦测试：

```bash
dotnet test Desktop.UpdateTransactions.Test/Desktop.UpdateTransactions.Test.csproj
```

测试覆盖清单校验、文件替换、回滚、恢复、其他实例运行时的阻止行为和清理。
真实进程交接还需通过隔离安装目录验证；Windows 文件锁行为需在 Windows 上验证。

## 图像编解码原生库

`Engine.Media` 提供图像、音频和模型数据的读取与编解码，供游戏、模组与内容工具共同使用。
媒体 API 使用流，文件路径和游戏存储由调用方处理；字体纹理、GPU 缓冲和音频播放仍由 Engine 负责。
图像原生库位于 `Native.ImageCodecs/runtimes/<rid>/native`，Android OpenAL 位于
`Native.OpenAL.Android/runtimes/<rid>/native`。`Native.ImageCodecs.csproj` 将图像库打包为
`SCNET.Native.ImageCodecs`，Media 引用该包，由 SDK 按 RID 选择并复制或打包原生资产。
应用、服务和测试无需导入图像原生库 props；首次构建需先准备本地原生包，见 [NuGet](NuGet.md#本地打包)。
Android OpenAL 由 Engine 的 Android 目标引用 `SCNET.Native.OpenAL.Android`，
与图像库一样通过标准 RID 原生资产自动进入 APK；启动器无需文件引用或 ABI 映射。
ContentTool 将完整运行输出打入工具目录，独立运行时不依赖模组项目的资产。
许可声明统一维护在根目录的 `ThirdPartyNotices.txt`；桌面应用发布时携带，Android 加入 APK assets。

原生库版本、许可证和重编译方法见 [Native.ImageCodecs](../Native.ImageCodecs/README.md)。
仓库已包含稳定、验证可用的预编译原生库，日常开发、首次构建和发布均直接复用；
准备本地 NuGet 包只是打包这些现有文件，不需要重新编译。只有升级版本或明确需要修复、
补充原生产物时，才执行原生库构建脚本，并在替换前验证对应平台或 ABI。
该项目的 `Scripts/` 包含 Android 图像库及桌面 PNG/zlib 构建脚本：在临时目录中编译，
全部成功后复制到项目的 `runtimes` 并更新校验值。成功后清理临时目录，失败时保留日志和中间文件。
这些项目专用脚本不发布应用，也不注册到解决方案级脚本分组。
