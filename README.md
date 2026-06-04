# Survivalcraft 联机版

这是一个 生存战争（Survivalcraft）联机版仓库。

## 法律与版权声明

请先阅读免责声明：[DISCLAIMER.md](./DISCLAIMER.md)。

## 环境要求

- [.NET 10 SDK](https://dotnet.microsoft.com/zh-cn/download/dotnet/10.0)
- 桌面开发：Visual Studio 2022+ 或 Rider，安装 `桌面开发`、`MAUI` 工作负载
- Android：额外安装 Android SDK

## 构建

```bash
# 还原依赖
dotnet restore SCNETWORK.slnx

# Debug 构建
dotnet build SCNETWORK.slnx

# Release 构建
dotnet build SCNETWORK.slnx --configuration Release
```

可选构建配置：`Debug`、`Release`、`ANDROID`、`DESKTOP`。

桌面版启动器支持 `-d` 或 `--server` 参数，传入后会直接进入无头服务端模式。
启动配置文件为 `config:RunningSetting.xml`，缺失时会使用默认值，不影响正常启动。
将其中的 `RunMode` 设为 `HeadlessServer`，即可让启动器直接进入无头服务端模式。

## 项目结构

| 项目 | 说明 |
|------|------|
| `Engine.Core/` | 核心类型 |
| `Engine.Serialization/` | 序列化 |
| `Engine/` | 渲染/音频 |
| `EntitySystem/` | ECS 实体系统 |
| `Survivalcraft/` | 游戏逻辑库 |
| `Survivalcraft.Linux/` | Linux 启动器 |
| `Survivalcraft.Windows/` | Windows 启动器 |
| `Survivalcraft.Android/` | Android 启动器 |

## 测试

```bash
dotnet test Engine.Test/
dotnet test EntitySystem.Test/
dotnet test Survivalcraft.Test/
```

测试框架：xUnit + coverlet。测试仅在 `Debug` 配置下编译。

## 贡献

1. Fork 本仓库并克隆
2. 基于主分支创建功能分支
3. 提交代码并推送
4. 在 Gitee 上创建 Pull Request
