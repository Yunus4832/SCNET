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

HTTP 服务尚未实现；已预留的协议契约使用统一的 `POST /commands` 入口，不为每条命令建立路径。请求通过 identity 分发：

```json
{
  "identity": "example.mod:echo",
  "arguments": {
    "text": "hello"
  }
}
```

只有注册了同 identity `HttpCommandBinding` 的命令才会暴露。未来 HTTP 宿主负责认证并创建可信的 `CommandPrincipal`，同时将调用入口记录为 `CommandInvocationChannel.HttpApi`；命令仍会执行正常的命令域、权限和宿主环境校验。
