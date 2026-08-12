# Headless 模式

Headless 模式在不打开游戏窗口的情况下运行服务端逻辑，适合部署独立联机服务器。

它复用同一套世界、网络和模组运行时，但跳过图形界面流程。启动目标和世界都通过启动会话系统解析，详见 [StartupSessions.md](./StartupSessions.md)。

## 启动方式

### Windows

```bash
SurvivalcraftStarter.exe --server
```

等价短参数：

```bash
SurvivalcraftStarter.exe -d
```

### Linux

```bash
./SurvivalcraftStarter --server
```

开发环境可以直接运行 Linux 项目：

```bash
dotnet run --project Survivalcraft.Linux/Survivalcraft.Linux.csproj -- --server
```

### Android

Android 不依赖命令行参数切换无头模式。它读取 `config:RunningSetting.xml`，当 `RunMode` 为 `HeadlessServer` 时进入无头服务端流程。

## 常用参数

- `--instance <实例名>`: 选择或创建 `Instances/<实例名>` 数据实例；省略时由 `Starter.xml` 选择
- `-d` / `--server`: 切换到 `HeadlessServer`
- `--session <名称>`: 选择或创建一个具名启动会话
- `--world <名称>`: 给 `--session` 指定世界名或世界目录名
- `--seed <种子>`: 给 `--session` 指定新建世界时使用的种子
- `--log-level <级别>`: `Debug`、`Verbose`、`Information`、`Warning`、`Error`
- `--save`: 将运行模式、日志级别和未消费参数写入 `RunningSetting.xml`

`--world` 和 `--seed` 只有在同时指定 `--session` 时才会生效。没有 `--session` 时它们会被忽略，避免一次临时命令覆盖默认启动状态。

示例：

```bash
./SurvivalcraftStarter --instance server --server --session survival --world World --seed 123456 --log-level Information --save
```

再次启动同一个服务器：

```bash
./SurvivalcraftStarter --instance server --server --session survival
```

## 数据实例

Starter 首先在程序基础目录注册 `starter:`，读取 `starter:Starter.xml`，再将选中实例的目录注册为游戏使用的 `external:`、`data:` 和 `config:`。实例目录位于：

```text
Instances/<实例名>/
```

因此不同实例拥有独立的设置、身份、世界、模组、缓存和日志。`--instance` 由 Starter 消费，不会写入 `RunningSetting.RemainingArgs`。不存在的命令行实例会自动创建。

## 配置文件

Headless 启动主要涉及三个配置文件。

### RunningSetting.xml

路径：`config:RunningSetting.xml`

`RunningSetting.xml` 只保存启动入口层面的状态，不保存世界名或种子。

示例：

```xml
<RunningSetting RunMode="HeadlessServer" LogLevel="Information" DefaultSessionId="" PendingSessionId="">
  <RemainingArgs />
</RunningSetting>
```

字段说明：

- `RunMode`: `Gui` 或 `HeadlessServer`
- `LogLevel`: 最低日志级别
- `DefaultSessionId`: 没有命令行指定 session 时使用的默认会话 id
- `PendingSessionId`: 重启恢复使用的临时会话 id
- `RemainingArgs`: 启动器未消费、需要保留的参数

### SessionInfo.xml

路径：`config:SessionInfo.xml`

这里保存具名或临时启动会话。Headless 会解析当前 active session，得到目标世界、种子和远程/本地目标。

Headless 使用的会话通常是：

```xml
<SessionInfo
  SessionId="..."
  Name="survival"
  Target="World"
  World="World"
  Seed="123456"
  ServerHost=""
  ServerPort="0"
  Password="" />
```

### ModProfile.xml

路径：

- 全局 profile：`config:ModProfile.xml`
- 世界 profile：`<world>/WorldModProfile.xml`
- 会话 profile：`config:SessionProfiles/<sessionId>.xml`

Headless 启动时会解析当前会话对应的有效模组 profile，并确保缺失的包已下载到本地缓存。解析结果会直接成为本进程的 `CurrentModRuntime.Value.EffectiveProfile`。Headless 不通过“请求重启”来准备模组。

## 世界解析

Headless 启动时：

- 如果 session 指向的世界存在，直接加载该世界
- 如果世界不存在，会按 session 中的 `World` 和 `Seed` 创建
- 如果世界已存在，`Seed` 不再影响该世界
- 如果世界没有开启 `RunServer`，Headless 会自动启用它并保存世界设置

## 模组仓库

模组下载地址按以下顺序解析：

1. 当前有效 `ModProfile.RepositoryUrl`
2. `Settings.DefaultModRepositoryUrl`

如果两者都为空，只能使用本地已经存在的模组包。

## 运行时行为

Headless 启动后会：

- 初始化设置、内容、包管理和本地模组导入
- 解析启动会话和有效模组 profile
- 下载缺失的 required mods
- 启动服务端侧模组 runtime
- 加载或创建世界
- 启动游戏端口和广播端口
- 运行 20 TPS 左右的主循环
- 退出时保存世界和设置

Headless 进程运行期间不会像 GUI 一样在进入 world 前弹窗请求重启；如果要切换模组组合，需要用新的 session/profile 重新启动进程。

可以通过 `Ctrl+C` 终止进程。
