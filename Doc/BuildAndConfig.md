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
dotnet restore SCNETWORK.slnx

# Debug 构建
dotnet build SCNETWORK.slnx

# Release 构建
dotnet build SCNETWORK.slnx --configuration Release
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

### 打包行为

仓库使用 `Directory.Build.targets` 统一处理资源和发布结果：

- 构建前会把 `Content/` 打成 `Content.zip`
- 桌面发布会将 `$(PublishDir)` 生成的发布输出压缩到仓库根目录下的 `Publish/` 目录
- Android 构建会生成并重命名 APK 到 `Publish/`

这也是为什么不同平台项目里会看到 `UsePackResourceTarget`、`UsePackOutputTarget` 和 `UseRenameApkTarget` 之类的属性。这些属性用于集中处理资源和发布产物。
