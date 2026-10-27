# NuGet 包

SCNET 使用显式的包白名单。只有在项目文件中将 `IsPackable` 设置为 `true`，
项目才允许打包。每个包使用其项目目录内的 `README.md`，描述该包的职责、消费方式和依赖边界，
不使用仓库首页替代包说明。

## 运行时包

| 包 | 项目 | 用途 |
| --- | --- | --- |
| `SCNET.Native.SDL` | `Native.SDL` | SDL2 及 Android 平台适配和启动桥接；由 Engine 依赖，替换 Silk 自带原生资产。 |
| `SCNET.Native.OpenAL` | `Native.OpenAL` | Windows、Linux 和 Android 四种 ABI 的 OpenAL，由 Engine 统一依赖。 |
| `SCNET.Native.ImageCodecs` | `Native.ImageCodecs` | PNG、WebP 的标准 RID 原生资产；由 Media 依赖。 |
| `SCNET.Engine.Core` | `Engine.Core` | 数学、集合、文本编码、身份地址、日志和运行支持；命名空间为 Engine.Core 下的 Mathematics、Collections、Text、Security、Logging、Runtime，不依赖窗口或图形层。 |
| `SCNET.Engine.Media` | `Engine.Media` | 图像、音频、glTF 2.0 场景数据与编解码；公共命名空间按 Images、Audio、Models 划分于 Engine.Media 下，无窗口、GPU、音频设备及文件存储依赖。 |
| `SCNET.Engine.Serialization` | `Engine.Serialization` | 基于 Engine.Core 构建的序列化支持。 |
| `SCNET.Rendezvous.Protocol` | `Rendezvous.Protocol` | 邀请信令契约及认证中继报文编码。 |
| `SCNET.Engine.Network` | `Engine.Network` | 独立传输宿主、连接生命周期与邀请路径；依赖 Engine.Core 的共享身份地址编码，内部使用 LiteNetLib。 |
| `SCNET.Engine` | `Engine` | 跨平台图形、音频、输入、存储和窗口运行时。 |
| `SCNET.EntitySystem` | `EntitySystem` | 实体、组件、子系统和模板数据库运行时。 |
| `SCNET.Content.Packaging` | `Content.Packaging` | 统一内容包协议、读写和验证。 |
| `SCNET.ServerSource.Protocol` | `ServerSource.Protocol` | 服务器源 HTTP 契约、验证和标准客户端。 |
| `SCNET.Authentication.Protocol` | `Authentication.Protocol` | 独立外部认证契约、ES256 持钥请求、连接批准验证与有界 HTTPS 客户端；只依赖 Engine.Core。 |
| `SCNET.Game.Protocol` | `Game.Protocol` | 游戏通信契约、协议兼容整数与二进制编解码；独立于游戏运行时和网络传输实现。 |
| `SCNET.Survivalcraft` | `Survivalcraft` | 游戏运行时和 Mod 编译契约。 |

这些包遵循项目的依赖边界。使用方通常只需引用所需的最高层级包。Mod 应引用
`SCNET.Survivalcraft`，不应再单独列出各个引擎包。

## 构建工具包

| 包 | 项目 | 用途 |
| --- | --- | --- |
| `SCNET.ContentTool` | `ContentTool` | 内容包 CLI 和自动生成 `.scpkg` 的 Mod 构建 Target。 |

## 模板包

`SCNET.ModTemplates` 由 `Survivalcraft.ModTemplates/Survivalcraft.ModTemplates.csproj`
构建。它包含 `Survivalcraft.ModTemplates/Survivalcraft.Mod/` 中的模板资源，生成的
项目会引用打包时确定的 `SCNET.Survivalcraft` 和 `SCNET.ContentTool` 版本。模板、游戏 SDK 和
工具独立维护版本；模板项目从集中版本属性生成引用，不要求三者版本号相同。

`SCNET.ContentTool` 是私有构建工具包，携带 ContentTool CLI、协议依赖和自动生成
`.scpkg` 的 MSBuild Target。生成的 Mod 项目显式引用该包，但不会把它作为运行时
依赖传递。`SCNET.Survivalcraft` 不依赖或携带构建工具。

`SCNET.Engine.Media` 依赖 `SCNET.Native.ImageCodecs`，图像原生库通过标准
`runtimes/<rid>/native` 资产传递；应用只引用 Media 或上层库即可。
`SCNET.ContentTool` 统一将完整运行输出装入 `tools/net10.0/any`，包含托管依赖、
`.deps.json`、原生资产和许可声明，支持从 NuGet 工具目录独立执行；这些文件不传递到模组输出。

## 不作为 NuGet 包的项目

- 平台启动项目（`Survivalcraft.Windows`、`Survivalcraft.Linux` 和 Android 项目）
  属于应用程序，应以对应平台的产物形式分发。
- `RendezvousServer` 是独立信令服务，`NetworkProbe` 是网络测试驱动器，两者通过应用发布脚本分发。
- 测试项目仅用于验证实现。
- `TerritoryStoneMod` 是实际模组，产物为 `.scpkg`，而不是 NuGet 包。
- `Survivalcraft.ModTemplates/Survivalcraft.Mod/` 是模板源代码；只有对应的模板打包
  项目会生成 NuGet 包。

## 版本归属

版本集中于 `SharedProperties/Versions.props`，各项目显式选择 `Version`，包版本默认跟随该项目。
不能再用游戏版本推断引擎、原生依赖或工具版本。`pre-build.sh/.ps1` 分别读取白名单项目的
`PackageVersion`，检查相应文件并精确刷新缓存。修改某组件不要求无关包升版，但发布的依赖
元数据或内嵌文件变化时，消费者产物也需要新版本，不能重用已发布包的身份。

模板打包时将游戏 SDK、ContentTool 和新模组初始版本写入生成的模板，不把仓库属性引用泄漏
到用户项目。领地石的 `manifest.template.json` 在构建时用集中定义的模组版本生成有效清单。

## 本地打包

仅需准备或更新原生包时，可选择三个原生资产项目：

```bash
./Scripts/pack-nuget.sh --project Native.ImageCodecs --project Native.OpenAL --project Native.SDL
```

这三个项目只将现有原生文件打包，不编译 C/C++，位于解决方案的 `01 NativeLibraries` 逻辑文件夹。

首次开发构建使用 `./Scripts/pre-build.sh`（PowerShell 为 `./Scripts/pre-build.ps1`），自动准备缺失的
本地包并还原解决方案。SDK 源码或原生资产改变后使用 `--refresh`（PowerShell 为 `-Refresh`），
清除这些包当前版本的缓存并重新打包。它只负责依赖准备，完成后照常使用 `dotnet build`。

从仓库根目录运行统一打包脚本。脚本先生成图像、OpenAL 和 SDL 原生资产包，再按依赖顺序逐个执行可打包项目，任一项目失败时立即停止：

```bash
./Scripts/pack-nuget.sh
```

PowerShell：

```powershell
./Scripts/pack-nuget.ps1
```

默认打包全部项目；可按项目名称选择一个或多个项目，执行顺序仍按脚本中的依赖顺序：

```bash
./Scripts/pack-nuget.sh --project Native.ImageCodecs --project Native.OpenAL --project Native.SDL
./Scripts/pack-nuget.sh --project ContentTool -- --no-restore
```

```powershell
./Scripts/pack-nuget.ps1 -Project Native.ImageCodecs,Native.OpenAL,Native.SDL
```

选择参数使用项目名称（例如 `Survivalcraft.ModTemplates`），不是 NuGet 包名。
额外参数传给每个选中项目的 `dotnet pack`。仅打包所选项目，不自动补打包依赖；
依赖包的新版本需事先准备好。省略项目选择时保留原有全量行为。

包会输出到 `Publish/NuGet`。仓库根目录的 `NuGet.Config` 已将该目录注册为
`SCNET Local` 文件源，因此 `TerritoryStoneMod` 和仓库内生成的 Mod 项目与外部
开发者一样，只使用 `PackageReference`，不回退到项目引用或相对路径 Target。

如需在干净环境中验证使用方流程，请从该目录安装模板，在仓库外创建项目，并将
`Publish/NuGet` 作为包源执行还原。最终生成的 `.scpkg` 应包含 Mod 程序集及其自身
内容，不应包含 Survivalcraft 或引擎运行时程序集。

不要通过解决方案级 `dotnet pack SCNET.slnx` 生成发行包。包集合采用显式白名单，并包含多目标项目和构建工具之间的顺序依赖；新增或移除可发布包时，应同时更新两个 `pack-nuget` 脚本、本页包清单及相关模板版本约束。
