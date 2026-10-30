# 构建与共享配置

## 共享配置

仓库将许多跨项目约定集中到几个共享文件里，避免每个工程重复配置。对于联机项目而言，这有助于降低跨平台配置不一致的风险。

### `Directory.Build.props`

这是全局构建属性入口，主要定义了：

- `GlobalTargetFramework`
- `AndroidTargetFramework`
- `MinAndroidApiVersion`
- 通过 `SharedProperties/Versions.props` 集中导入发布版本
- `LangVersion`
- `Nullable`
- `WarningsAsErrors`
- `ImplicitUsings`
- 解决方案级别的 `Configurations`

这些属性会被大多数项目自动继承，所以单个项目里通常只需要写差异部分。

项目自身的目标框架引用 `GlobalTargetFramework` 或 `AndroidTargetFramework`；Windows 启动器
在通用框架后追加 `-windows`。受已知 MSBuild 缺陷影响，`ProjectReference.SetTargetFramework`
仍须硬编码，平台引用的 RID 也按现有约定固定，不应将这些缺陷规避改为属性引用。
框架升级时需同步检查这些引用、独立模组模板及 NuGet 包内工具路径。

### 应用图标与 Android 资源

Android、Linux 和 Windows Starter 共用 `SharedResources/AppIcon/icon.png`，
桌面 Starter 另外共用同目录下的 `icon.ico`，
平台项目直接引用共享文件，不各自保存副本：

- Android 将 PNG 链接为 `Resources/mipmap/icon.png`，继续使用 `@mipmap/icon`。
- 桌面 PNG 嵌入 Starter，资源名为 `Starter.Resources.icon.png`，用于引擎窗口图标；
  Linux 还将它安装到用户图标目录，供 `.desktop` 入口使用。
- ICO 通过 `ApplicationIcon` 嵌入两个平台的托管 DLL，Windows 还将资源带入 EXE，
  支持文件管理器预览。Linux ELF 启动程序不因此获得文件图标。

桌面 PNG 保留标准格式：[Linux 图标主题规范](https://specifications.freedesktop.org/icon-theme/latest/)
推荐 PNG，没有将 WebP 列为桌面图标格式。游戏纹理默认 WebP 的约定不适用于这类系统集成资源。
桌面图标由 Starter 加载，不经过游戏 `Content/Assets` 资源系统。

Android 专属的 `AndroidManifest.xml`、`values/colors.xml`、`values/styles.xml` 和 `xml/update_paths.xml`
集中在 `SharedResources/Android`。四个 Android 架构项目都由 `AndroidApplication.props`
引用同一份平台资源和应用图标；Manifest 通过专用 `AndroidManifest` 属性引用，
不作为普通 `AndroidResource` 编译。`AndroidVariant.props` 仅为非主架构项目复用源码。

### `SharedProperties/Versions.props`

所有自主发布单元从 `0.1.0` 开始，使用 `MAJOR.MINOR.PATCH[-prerelease]`。
游戏、引擎、原生库、内容工具、协议包和服务各自选择独立版本属性；数字只在此文件维护。
`Version` 用于程序集和 NuGet 默认包版本，消费方通过对应生产者属性引用精确依赖。
游戏及 Starter 共用 `GameVersion`，`Engine.*` 与 `EntitySystem` 共用 `EngineVersion`；
其他发行单元使用同名版本属性，开发工具与测试默认使用 `ToolsVersion`。
[UI 预览工具](../UiPreviewTool/README.md) 使用独立的 `UiPreviewToolVersion`，从 `0.1.0` 开始。
ContentWebUI 随 ContentServer 发布，私有 npm 清单不重复定义产品版本。
开发、构建、测试、提交和推送均保持现有版本号，不重置现有版本。
由用户明确新发行版的发布时机，Agent 再按该批次累积变更评估受影响的发行单元及依赖，统一提升一次。
重新打包或部署已确定的版本不重复升版；“打包或部署最新代码”等请求若未明确新发行意图，先确认再改版本。
新发行单元保留约定的初始版本。本地未发布的 NuGet 包允许同版本刷新，使用 `pre-build --refresh`
重打包并清理对应缓存；上传远程源的包版本不可覆盖。
本地模组测试构建使用完整包哈希区分，不为了每轮测试递增产品版本。

游戏从自身程序集的 `AssemblyInformationalVersion` 读取显示版本，去掉 `+` 后的构建信息；
完整标识保留在 `VersionsManager.BuildVersion`。CLR 数值程序集版本由 SDK 生成，不作为产品版本。
`GameProtocolPackageVersion` 是游戏协议库的独立 SemVer 发布版本。`GameProtocolVersion` 是独立的正整数兼容代际（当前为 `1`），由协议程序集持有元数据并在运行时读取为整数；游戏仅引用该唯一来源。握手与服务器发现均使用 Int32 编码；协议代际必须严格相等才能连接。未发布阶段固定为 `1`，不随开发迭代提升，也不保留旧测试协议兼容。建立正式发布的互操作边界后再约定兼容代际的提升规则；它始终不随产品或协议库的 SemVer 版本自动变化。
发行版本验证和更新排序使用同一语义化比较规则，不接受旧四段式产品版本。

Android 共享配置把 `GameVersion` 写入版本名称，把 `AndroidReleaseCode` 写入系统升级计数。
升级计数独立递增，同一发行批次各 ABI 共用该计数，签名密钥保持不变。应用标签固定为 SCNET。
Android 安装计数仅在准备新的 Android 发行批次时提升一次，普通构建不提升。
当前 `0.x` 开发期不提供兼容性保证，新发行批次通常递增补丁版本，明确进入新开发阶段时再提升次版本。
同一发行批次已调整的版本不重复提升，普通提交不自动升版。

升版时同步维护根目录 `CHANGELOG.md`（首次升版时创建），按发行批次和日期列出各发行单元及准确版本，
汇总各单元自上一发行版以来的功能、修复、不兼容变化及必要的使用或部署操作。基于历史和差异汇总，
不只记录最后一次提交。普通提交与同版本本地重打包不要求新增条目，也不要求回填历史发行日志。

涉及 `Versions.props` 变化的提交，正文必须记录以下可检索区块，属性名及前后值以该次暂存差异为准：

```text
Versions:
- TerritoryStoneVersion: 0.1.2 -> 0.1.3
```

新增属性使用 `initial -> 0.1.0`，删除属性使用 `旧值 -> removed`；多项变更逐行列出，
包括实际变化的 Android 安装计数和协议标识。未改版本的提交不添加该区块。
例如可用 `git log --all --grep='TerritoryStoneVersion: .* -> 0\.1\.3$'` 定位目标版本的变更提交，
再通过 `git show <commit>:SharedProperties/Versions.props` 核对完整版本集合。
正式发布时可另用带发行单元名称的标签定位源码，开发提交不自动打标签。


游戏帮助中的“关于 SCNET”从运行程序集读取产品与构建版本（页面中的提交 hash 显示前 8 位，完整值可通过 `VersionsManager.BuildVersion` 读取），并展示游戏联机、服务器源、P2P 信令与中继数据报及内容包格式的实际版本。这些协议/格式标识独立于相应 NuGet 包的发布版本，不应混用；页面无需联网。

### `Directory.Build.targets`

这是全局构建目标入口，主要负责：

- 在资源打包前根据独立 SVG 源文件增量生成 GUI 图集
- 把 `Content/` 打成 `Content.zip`
- 在 `Publish` 后打包桌面发布输出
- 在 Android 发布后复制并重命名 APK

通用资源与压缩包流程由此文件处理；容器镜像、服务发行物等专属行为由各项目的
`Directory.Build.targets` 和部署脚本负责。

### `SharedProperties/GlobalUsing.props`

这是全局 `using` 的集中入口。它为许多常用命名空间做了隐式导入，例如：

- `Engine.Core.Mathematics`
- `Engine.Core.Collections`
- `Engine.Core.Logging`
- `Engine.Core.Runtime`
- `Engine.Core.Text`
- `Engine.FileStorage`
- `Engine.Windowing`
- `Game.Managers`
- `Game.Screens`
- `Game.Subsystems`
- `Game.Components`
- `Game.Modding`

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
`Content.zip` 资产声明、共享图标与 Android 资源、共享 Manifest 和全局 `using`。每个架构项目仍显式声明自己的
`RuntimeIdentifier`、APK 名称，并在 `ProjectReference` 中硬编码相同 RID，以避免 Rider 设计时
构建和 MSBuild 项目引用求值产生架构偏差。

公共 Manifest 位于 `SharedResources/Android/AndroidManifest.xml`，所有 Android 启动器使用同一个模板。
应用通过限制到缓存 `updates/` 子目录的 FileProvider 把已校验 APK 交给 Android 系统安装器；各架构变体必须链接同一份 `update_paths.xml`。
架构项目不应保存独立副本；需要平台级声明时修改公共模板，需要真正的架构差异时再使用条件配置
或 Manifest Overlay。

### `SharedProperties/AndroidVariant.props`

Arm32、x64 和 x86 变体通过这份配置链接 `Survivalcraft.Android/` 中的公共启动器源码。
平台资源由四个架构共同导入的 `AndroidApplication.props` 引用，不再从主架构项目转引。
各变体继续保留独立的项目、`bin/obj`、`Content.zip` 和对应 ABI 的 OpenAL 库，使 Rider
可以直接选择匹配设备架构的项目运行，同时避免不同 RID 的中间产物互相污染。

## 构建

```bash
# 首次构建：安装 ContentServer 的前端依赖（需要 Node.js 与 npm）
npm --prefix ContentWebUI ci

# 准备本地 NuGet 包并还原依赖
./Scripts/pre-build.sh

# Debug 构建
dotnet build SCNET.slnx

# Release 构建
dotnet build SCNET.slnx --configuration Release

# Bash 主机：顺序生成应用和服务发行产物
./Scripts/publish.sh

# PowerShell：顺序生成应用和服务发行产物
./Scripts/publish.ps1
```

当前尚未配置第一方远程 NuGet 源，本地包准备是过渡流程。配置远程源并上传所需版本后，
普通开发可恢复 IDE / `dotnet build` 自动还原，本地准备仅用于开发未发布的包。

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

### 配置语义

- `Debug` 和 `Release` 是通用配置，会走完整解决方案编译流程
- `Engine` 中的 Android 和桌面平台源码由目标框架选择，不依赖解决方案配置或手写的条件编译常量
- `Debug` 更适合开发调试，`Release` 更适合发布打包
- 测试工程在 `Release` 下会被排除，部分 Android 变体也会在 `Release` 下被排除
- 当前代码库大量使用反射，因此不启用 `trim`，也不支持通过裁剪方式进行发布优化

`Scripts/publish.sh` 和 `Scripts/publish.ps1` 会依次发布各发行入口，并为每个入口启动独立、串行的 `dotnet publish`，避免多目标共享项目在并行还原和发布时竞争中间文件。当前包含 Linux、Windows、Android Arm64、Android Arm32、ContentServer、RendezvousServer、Authentication.Server 和 NetworkProbe。各项目使用独立命令，便于以后配置平台专属参数；任一项目失败时，脚本会立即停止并返回失败。

不指定项目时发布全部。指定项目时只发布选中的入口；多个项目仍按脚本中的固定顺序发布。项目名称为 `Survivalcraft.Linux`、`Survivalcraft.Windows`、`Survivalcraft.Android`（Arm64）、`Survivalcraft.Android.Arm32`、`ContentServer`、`RendezvousServer`、`Authentication.Server` 和 `NetworkProbe`。例如：

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

- `GuiAtlasTool/Assets/` 中任一 SVG、PNG 或 WebP 发生变化时，会先重新生成 GUI 图集
- 构建前会把 `Content/` 打成 `Content.zip`
- 桌面发布会将 `$(PublishDir)` 生成的发布输出压缩到仓库根目录下的 `Publish/` 目录
- Android 发布会生成并重命名 APK 到 `Publish/`

这也是为什么不同平台项目里会看到 `UsePackResourceTarget`、`UsePackOutputTarget` 和 `UseRenameApkTarget` 之类的属性。这些属性用于集中处理资源和发布产物。

### GUI 图集

`GuiAtlasTool/Assets/` 保存独立的 PNG、WebP 和 SVG，文件名就是图集条目名。
位图默认使用无损 WebP，也接受普通 PNG；两者都应使用直通 Alpha，不需要作者预乘。
SVG 使用整数像素 `width`、`height` 和对应 `viewBox`。同一条目只能提供一种格式。

工具按尺寸和名称稳定排序，将 SVG 和位图排版合成为无损 WebP 图集，生成以下忽略的构建产物：

- `Content/Assets/Atlases/AtlasTexture.webp`
- `Content/Assets/Atlases/Atlas.txt`

```bash
dotnet run --project GuiAtlasTool/GuiAtlasTool.csproj -- generate \
  GuiAtlasTool/Assets \
  Content/Assets/Atlases/AtlasTexture.webp \
  Content/Assets/Atlases/Atlas.txt
```

`prepare-image` 将 PNG/WebP 转为普通无损 WebP，`extract` 从当前图集中直接提取 WebP 条目；
仅历史预乘输入的一次性导入需要显式指定 `--premultiplied`。详见 [图集工具](../GuiAtlasTool/README.md)。

启用 `UsePackResourceTarget` 的项目在 `PackResource` 前生成图集。
MSBuild 将 SVG、PNG、WebP 和工具源码作为增量输入；没有变化时不会重复生成。
图集工具在构建主机上以 `net10.0` 运行，嵌套构建会移除应用的目标框架、RID、平台及输出路径参数，
不会将工具改为 Android 项目，也不需要为它还原 Android 目标。
IDE 安装阶段的 `NoBuild` 和 `DeployOnBuild` 也不会传给主机工具，避免已构建应用的安装流程阻止工具生成图集。

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

`Authentication.Server` 使用独立 `AuthenticationServerVersion`，新发行单元初始为 `0.1.0`；不跟随普通开发构建递增。
通过 `Scripts/publish.sh --project Authentication.Server` 或 PowerShell 同名入口生成
`authentication-server-<版本>-portable.zip`；Linux 另生成 `authentication-server-<版本>-linux-amd64.tar.gz` 容器 bundle。
便携服务依赖 ASP.NET Core 10 运行时。包内含运行脚本、配置示例及第三方许可；数据目录由运行时按配置创建，
不包含本机 appsettings、身份私钥、授权状态或账号运行配置；已有发布目录夹带这些文件时拒绝打包。
普通 `dotnet build` 不执行打包或容器组装。容器基础镜像默认只用本地缓存，和信令服务相同的
`CONTAINER_ENGINE`、`IMAGE_PULL_POLICY` 覆盖适用。配置与备份见 [认证服务部署](../Authentication.Server/README.md#linux-容器部署)。

### 桌面更新包

Windows/Linux 的便携 ZIP 包携带 `DesktopRelease.xml`，声明平台和由更新管理的根目录发行文件。
解决方案的 Desktop 分组包含共享库 `Desktop.UpdateTransactions` 和测试项目
`Desktop.UpdateTransactions.Test`。共享库使用命名空间 `Scnet.Desktop.Updates`，负责清单验证、暂存、进程交接、
文件更新事务、回滚和中断恢复，不依赖游戏或 Engine。Windows/Linux Starter 通过项目引用调用共享库，
平台文件替换和通知保留在各自项目中。游戏通过退出动作提交更新请求，由 Starter 在游戏运行结束后接管。类库随单文件发布合并，不额外发布更新器或依赖 shell/PowerShell。
当前 Linux 集合为 `SurvivalcraftStarter`、`Content.zip`、`libSDL2-2.0.so`、`libopenal.so`；Windows 集合为
`SurvivalcraftStarter.exe`、`Content.zip`、`SDL2.dll`、`soft_oal.dll`。两平台还包含各自的三个图像原生库和
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
图像原生库位于 `Native.ImageCodecs/runtimes/<rid>/native`，OpenAL 位于
`Native.OpenAL/runtimes/<rid>/native`。`Native.ImageCodecs.csproj` 将图像库打包为
`SCNET.Native.ImageCodecs`，Media 引用该包，由 SDK 按 RID 选择并复制或打包原生资产。
应用、服务和测试无需导入图像原生库 props；首次构建需先准备本地原生包，见 [NuGet](NuGet.md#本地打包)。
各平台 OpenAL 由 Engine 统一引用 `SCNET.Native.OpenAL`，
与图像库一样通过标准 RID 选择原生资产，复制到桌面输出或进入 APK；启动器无需文件引用或 ABI 映射。
ContentTool 将完整运行输出打入工具目录，独立运行时不依赖模组项目的资产。
许可声明统一维护在根目录的 `ThirdPartyNotices.txt`；桌面应用发布时携带，Android 加入 APK assets。

原生库版本、许可证和重编译方法见 [Native.ImageCodecs](../Native.ImageCodecs/README.md)、
[Native.OpenAL](../Native.OpenAL/README.md) 和 [Native.SDL](../Native.SDL/README.md)。
SDL 由 Engine 统一引用 `SCNET.Native.SDL`，资产位于 `Native.SDL/runtimes/<rid>/native`；
包内的传递构建 target 排除 `Ultz.Native.SDL` 的资产，Android 同时替换 Silk AAR 的 Java 类和原生库，统一使用 SDL 2.32.10。
仓库已包含稳定、验证可用的预编译原生库，日常开发、首次构建和发布均直接复用；
准备本地 NuGet 包只是打包这些现有文件，不需要重新编译。只有升级版本或明确需要修复、
补充原生产物时，才执行原生库构建脚本，并在替换前验证对应平台或 ABI。
`Native.ImageCodecs/Scripts/` 包含 Android 图像库及桌面 PNG/zlib 构建脚本：在临时目录中编译，
全部成功后复制到项目的 `runtimes` 并更新校验值。成功后清理临时目录，失败时保留日志和中间文件。
这些项目专用脚本不发布应用，也不注册到解决方案级脚本分组。

## C# 代码风格与排版

`.editorconfig` 集中保存三类规则：基础 EditorConfig 规则、Microsoft/Roslyn C# 规则、
JetBrains 专属格式规则。文件中按适用工具分区注明，`resharper_` 键仅由 Rider、ReSharper、
CleanupCode 执行，`dotnet format` 不检查这些键。JetBrains 是统一排版的执行与检查工具，
Roslyn 只检查语法、命名等风格诊断；不再并列执行两套互相冲突的空白排版检查。

方法体不压成一行，字段和属性声明之间各空一行。过长的二元表达式链每个操作数一行，运算符放在上一行末尾；布尔条件链的各个 `&&` / `||` 条件因此分别换行。方法及构造调用（包括 `new(...)`）过长时，每个实参独占一行，右括号另起一行。声明参数过长或已换行时，每个参数一行，右括号另起一行；
普通方法/构造方法、主构造方法及 record 参数分别配置。短声明保留紧凑形式，建议行宽为 120。

Rider 中执行 Reformat Code；命令行可安装免费的跨平台 JetBrains 工具后格式化指定文件：

```bash
dotnet tool install --global JetBrains.ReSharper.GlobalTools --version 2026.2.3.1
jb cleanupcode Game.Protocol/Blocks/EditableBlockPackage.cs Engine/Input/TextInputTypes.cs
```

CleanupCode 的文件模式仅执行排版；解决方案模式必须显式选择 `--profile="Built-in: Reformat Code"`，
避免默认 Full Cleanup 同时改写语法。Roslyn 专项修复（例如主构造方法转换）单独执行并审查。
格式化后仍需运行 `.agents/skills/scnet-code-style/scripts/validate_changed_csharp.sh`。
脚本先检查枚举显式赋值，再将变更文件及相应 `.editorconfig` 复制到临时目录，用 CleanupCode
验证排版，最后执行 Roslyn 语法/命名风格检查。它不修改工作区，缺少 JetBrains CLI 会明确失败。
CLI 不在 PATH 时可设置 `SCNET_JB` 为 `jb` 可执行文件路径；普通编译不依赖该工具。
IDE/MCP 返回成功不能代替检查实际结果。

枚举成员必须显式赋值，包括首项与零值；已有数值、别名和 Flags 组合保持不变。
这不是 `.editorconfig` 的内置排版规则：上述检查脚本会调用 skill 内的 .NET 10 单文件 Roslyn 检查器，
报告 `SCNETSTYLE001`，并覆盖条件编译分支。业务项目不增加分析器依赖，也不会因此出现 IDE 实时诊断。
首次运行需还原该检查器固定版本的 Roslyn 包；全仓检查可执行：

```bash
dotnet run --file .agents/skills/scnet-code-style/scripts/check_explicit_enum_values.cs -- --all
```
