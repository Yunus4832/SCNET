# 构建与共享配置

## 共享配置

仓库将许多跨项目约定集中到几个共享文件里，避免每个工程重复配置。对于联机项目而言，这有助于降低跨平台配置不一致的风险。

### `Directory.Build.props`

这是全局构建属性入口，主要定义了：

- `GlobalTargetFramework`
- `AndroidTargetFramework`
- `MinAndroidApiVersion`
- `LangVersion`
- `Nullable`
- `WarningsAsErrors`
- `ImplicitUsings`
- 解决方案级别的 `Configurations`

这些属性会被大多数项目自动继承，所以单个项目里通常只需要写差异部分。

### `Directory.Build.targets`

这是全局构建目标入口，主要负责：

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

## 构建

```bash
# 还原依赖
dotnet restore SCNET.slnx

# Debug 构建
dotnet build SCNET.slnx

# Release 构建
dotnet build SCNET.slnx --configuration Release

# Linux/macOS：顺序生成应用和服务发行产物
./Scripts/publish.sh

# PowerShell：顺序生成应用和服务发行产物
./Scripts/publish.ps1
```

### 可选构建配置

- `Debug`
- `Release`
- `ANDROID`
- `DESKTOP`

### 配置语义

- `Debug` 和 `Release` 是通用配置，会走完整解决方案编译流程
- `ANDROID` 和 `DESKTOP` 是平台配置，只编译对应平台相关的项目
- `Debug` 更适合开发调试，`Release` 更适合发布打包
- 测试工程在 `Release` 下会被排除，部分 Android 变体也会在 `Release` 下被排除
- 当前代码库大量使用反射，因此不启用 `trim`，也不支持通过裁剪方式进行发布优化

`Scripts/publish.sh` 和 `Scripts/publish.ps1` 会依次发布各发行入口，并为每个入口启动独立、串行的 `dotnet publish`，避免多目标共享项目在并行还原和发布时竞争中间文件。当前包含 Linux、Windows、Android Arm64、Android Arm32 和 ContentServer。各项目使用独立命令，便于以后配置平台专属参数；任一项目失败时，脚本会立即停止并返回失败。

NuGet 包是另一条发行流程，不由应用发布脚本生成。使用 `Scripts/pack-nuget.sh` 或 `Scripts/pack-nuget.ps1`，具体包边界和使用方式见 [NuGet 包](./NuGet.md)。不要使用解决方案级 `dotnet publish` 或 `dotnet pack` 代替这些脚本；多目标共享项目应由下游入口按顺序独立处理。

根目录 `Publish` 中的桌面压缩包、重命名后的 Android APK，以及 ContentServer 的 portable 和容器镜像包只在 `Publish` 阶段生成；普通 `Build` 不会更新这些发行产物。

### 打包行为

仓库使用 `Directory.Build.targets` 统一处理资源和发布结果：

- 构建前会把 `Content/` 打成 `Content.zip`
- 桌面发布会将 `$(PublishDir)` 生成的发布输出压缩到仓库根目录下的 `Publish/` 目录
- Android 发布会生成并重命名 APK 到 `Publish/`

这也是为什么不同平台项目里会看到 `UsePackResourceTarget`、`UsePackOutputTarget` 和 `UseRenameApkTarget` 之类的属性。这些属性用于集中处理资源和发布产物。
