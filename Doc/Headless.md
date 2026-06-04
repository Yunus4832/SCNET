# Headless 模式

Headless 模式在不打开游戏窗口的情况下仅运行服务端逻辑，适用于联机仓库的核心场景：

- 作为独立联机服务器部署
- 在 Linux 服务器、Windows 机器或容器环境中长期运行
- 只需要世界模拟和网络服务，不需要图形界面
- 作为稳定的联机服务器对外提供服务

## 启动方式

### Windows

Windows 启动器支持以下参数：

- `-d`
- `--server`

示例：

```bash
SurvivalcraftStarter.exe --server
```

或者：

```bash
SurvivalcraftStarter.exe -d
```

### Linux

Linux 启动器同样支持 `-d` 和 `--server`。

示例：

```bash
./SurvivalcraftStarter --server
```

开发环境也可以直接运行对应项目：

```bash
dotnet run --project Survivalcraft.Linux/Survivalcraft.Linux.csproj -- --server
```

### Android

Android 端无法通过命令行切换 Headless，因此采用更直接的方式——直接读取运行配置文件：

- 配置文件：`config:RunningSetting.xml`
- 将 `RunMode` 设为 `HeadlessServer`

首次启动时，如果配置文件不存在，程序会自动创建默认配置。
Android 进入 Headless 模式后，界面会切换到日志视图，服务端在后台持续运行。

## 常用参数

Headless 模式下，除了 `-d` / `--server`，还支持：

- `--world <名称>`: 指定世界目录名或世界名
- `--seed <种子>`: 新建世界时使用的种子

示例：

```bash
./SurvivalcraftStarter --server --world World --seed 123456
```

## 配置文件

`config:RunningSetting.xml` 用于保存启动设置，常见内容包括：

- `RunMode`
- `World`
- `Seed`

示例：

```xml
<RunningSetting RunMode="HeadlessServer" World="World" Seed="123456" />
```

行为说明：

- `RunMode=HeadlessServer` 时，启动器会直接进入无头服务端模式
- 如果 `World` 对应的世界不存在，程序会自动创建
- 如果提供了 `--seed`，新建世界时会优先使用该种子
- 如果世界已经存在，`--seed` 会被忽略

## Headless 运行时行为

Headless 启动后会：

- 初始化配置、内容、模组、数据库和世界系统
- 自动装载语言资源，优先使用 `zh-CN`，否则回退到 `en-US`
- 启动网络服务，默认监听游戏端口和广播端口
- 运行一个固定节奏的主循环，默认 20 TPS
- 退出时保存世界和设置

可以通过 `Ctrl+C` 终止进程。
