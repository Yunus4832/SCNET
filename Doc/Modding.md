# 模组开发

本文档面向模组开发者，说明如何创建项目并打包 `.scpak`。

如果只是配置、下载或启用模组，见 [Mods.md](./Mods.md)。

## 创建项目

Install the published template package:

```bash
dotnet new install SCNET.ModTemplates
```

创建并构建模组：

```bash
dotnet new scpakmod -n ExampleMod --modId example.mod
dotnet build ExampleMod/ExampleMod.csproj
```

包输出位置：

```text
bin/<Configuration>/<TargetFramework>/packages/<mod-id>.scpak
```

## 包结构

```text
manifest.json
assemblies/*.dll
data/**
assets/<mod-id>/**
```

模板项目引用 `SCNET.Survivalcraft`。它的构建目标会加入匹配的编译期 API，并在构建后创建 `.scpak`。宿主运行时程序集不会复制进包。

在本仓库内部，模板和验证模组直接使用 `Survivalcraft/Modding/Survivalcraft.Mod.targets`，这样核心代码和模组代码可以一起开发，不需要先发布中间 NuGet 包。

模板资源位于 `Survivalcraft.ModTemplates/Survivalcraft.Mod/`。解决方案里唯一与模板打包直接相关的项目是 `Survivalcraft.ModTemplates/Survivalcraft.ModTemplates.csproj`，它会把这些资源打包成可发布的 `dotnet new` 模板包。

## 示例模组

`VerificationBlockMod/` 是当前端到端验证模组，展示了：

- 代码、数据和 assets 打进同一个 `.scpak`
- 注册自定义方块
- 生命周期日志
- 玩家伤害拦截
- 方块挖掘、放置和世界更新回调

构建：

```bash
dotnet build VerificationBlockMod/VerificationBlockMod.csproj -c Debug
```

## 命令与前端适配器

模组在 `Configure` 阶段分别注册类型化命令和需要支持的前端绑定：

```csharp
using Game.Localization;

public sealed record EchoCommand(string Text) : IGameCommand;

public void Configure(IModContext context)
{
    var identity = new ResourceId(context.Manifest.ModId, "echo");
    var permission = new ResourceId(
        context.Manifest.ModId,
        "world.echo");
    context.Commands.Permissions.Register(
        permission,
        new CommandPermissionDefinition(
            CommandDomain.World,
            PermissionGrantPolicy.Standard));

    context.Commands.Register(
        identity,
        new CommandDefinition<EchoCommand>(
            (_, command) => CommandResult.Ok(command.Text),
            CommandDomain.World,
            requiredPermission: permission));

    context.Commands.Adapters.Register(
        new ResourceId(context.Manifest.ModId, "text/echo"),
        new TextCommand(
            "echo",
            new LocalizedText(
                "Commands",
                "ExampleEcho_Description",
                "输出文本"),
            [
                new CommandRoute(
                    [new CommandArgument("text")],
                    typeof(EchoCommand),
                    arguments => new EchoCommand(arguments.Get<string>("text")))
            ]));

    context.Commands.Adapters.Register(
        identity,
        HttpCommandBinding.Create<EchoCommand>(
            arguments => new EchoCommand(arguments.Get<string>("text"))));
}
```

命令定义拥有执行逻辑、命令域、权限和必要的宿主环境约束。绑定只负责把某个前端的参数转换为命令，不能创建身份或绕过 `CommandDispatcher`。模组也可以实现自己的
`ICommandAdapterBinding`，通过 `context.Commands.Adapters` 注册、查询，并由对应前端消费。

没有注册对应绑定的命令仍可由游戏 UI 直接以类型化方式执行，但不会自动暴露到文本或 HTTP。文本绑定本身不限制身份；同一个绑定可以由游戏命令面板或服务器 stdin 使用，最终是否允许执行由命令域、主体和权限注册表统一判断。

命令域只有三种：

- `CommandDomain.Application`：修改当前应用或设备，例如语言和 UI 设置，始终在本进程执行。
- `CommandDomain.World`：修改已加载世界；离线和 GUI 服务端直接在权威世界执行，联机客户端自动发送到服务器。
- `CommandDomain.Server`：管理服务器进程，只能在服务器权威端执行。

权限必须通过 `context.Commands.Permissions` 显式注册，并使用模组自己的 `ResourceId`
命名空间。命令只能引用已注册且命令域一致的权限。权限授权策略包括：

- `Standard`：拥有再授权能力的玩家可以授予。
- `OperatorManaged`：只能由服务器操作员授予，玩家获得后只能使用。
- `OperatorOnly`：不能授予玩家。

命令入口记录为 `CommandInvocationChannel`，只用于日志和展示，不参与授权。
如果操作在业务上必须绑定玩家实体，可以通过 `allowedPrincipals:
CommandPrincipalKind.Player` 声明主体要求；这与命令来自消息面板、HTTP 或 stdin 无关。

命令说明使用通用的 `Game.Localization.LocalizedText`，注册时不会读取当前语言。候选菜单和帮助信息在展示时解析资源，因此初始化语言或运行时切换语言都不需要重新注册命令。模组应在自己的语言资源中提供对应 section 和 key；只有 GUID 等运行时数据才应显式使用 `LocalizedText.Literal(...)`。

HTTP 命令宿主使用统一的 `POST /commands` 入口，不为每条命令建立路径。请求通过 identity 分发：

```json
{
  "identity": "example.mod:echo",
  "arguments": {
    "text": "hello"
  }
}
```

每个 HTTP binding 必须显式声明参数契约，使认证后的 `GET /commands` 能让客户端发现可调用命令及其格式：

```csharp
commands.Adapters.Register(
    new ResourceId(owner, "example/echo"),
    HttpCommandBinding.Create(
        arguments => new EchoCommand(arguments.Get<string>("text")),
        new HttpCommandArgumentDefinition("text", "string")));
```

`GET /commands` 只返回当前 HTTP 主体在当前运行模式下可能执行的 binding，并提供 identity、本地化说明及参数的 `name`、`valueType`、`required`。只有注册了同 identity `HttpCommandBinding` 的命令才会暴露。HTTP 宿主通过 Bearer Token 认证，并按当前宿主创建可信的 `ApplicationUser` 或 `ServerOperator`；调用入口记录为 `CommandInvocationChannel.HttpApi`，命令仍会执行正常的命令域、权限和宿主环境校验。宿主只监听 loopback，且仅在有效 session 或实例 `Settings.xml` 启用时启动；实例默认配置、启动参数和可保存的 session 覆盖见 [Headless.md](./Headless.md)。

内置 GUI 自动化命令位于 `game:automation/ui/*`，业务实现集中在 `Game.Automation`，命令层只负责参数校验和结果封装。`context/get` 返回目标支持的 `actions`；`tap`、`scroll`（鼠标滚轮）和 `swipe`（跨帧触摸轨迹）都必须通过 `Engine.Input.InputSimulation` 注入，不应直接修改 Widget 状态或调用 Screen 回调。`swipe` 的 `deltaX/deltaY` 表示手指从目标中心移动的方向和距离，例如向上滑动使用负的 `deltaY`。

相对鼠标输入使用 `game:automation/input/mouse/move`，其 `deltaX/deltaY` 会与同一帧的物理鼠标位移合并，可用于游戏内视角控制。它与用于 UI 命中定位的绝对鼠标坐标是两种不同语义。
