# 模组开发

本文档面向模组开发者，说明如何创建项目并打包统一内容包 `.scpkg`。

如果只是配置、下载或启用模组，见 [Mods.md](./Mods.md)。

## 网络消息受众

`context.Network.Send(messageType, payload, audience)` 与消息处理上下文的 `Send`
要求显式传入 `Game.Network.PackageAudience`，没有默认广播或可选 `to/except` 参数。
全局消息使用 `PackageAudience.Global`，定向消息使用 `PackageAudience.To(client)`，
空间消息将统一兴趣查询得到的观察者集合传入 `PackageAudience.To(observers)`。
集合会在创建受众时复制，空集合不会退回全局广播；连接按对象身份匹配。
客户端上行消息应显式指定当前服务端连接，不把上行请求描述成全局广播。
`Reply` 只回复当前接收上下文的原发送者；没有发送者时不发送。

## 创建项目

Install the published template package:

```bash
dotnet new install SCNET.ModTemplates
```

创建并构建模组：

```bash
dotnet new scpkgmod -n ExampleMod --modId example.mod
dotnet build ExampleMod/ExampleMod.csproj
```

包输出位置：

```text
bin/<Configuration>/<TargetFramework>/packages/<mod-id>.scpkg
```

## 包结构

```text
manifest.json
payload/mod.json
payload/assemblies/*.dll
payload/data/**
payload/assets/<mod-id>/**
```

模板项目引用 `SCNET.Survivalcraft` 作为编译期 API，并私有引用 `SCNET.ContentTool`。
工具包携带的构建目标会在构建后创建和验证 `.scpkg`，宿主运行时程序集不会复制进包。

仓库内的 `TerritoryStoneMod` 也只通过相同的 NuGet 包引用构建，不使用项目引用或
相对路径导入 Target。本地开发时先将当前版本包生成到仓库的 `Publish/NuGet` 文件源。

模板资源位于 `Survivalcraft.ModTemplates/Survivalcraft.Mod/`。解决方案里唯一与模板打包直接相关的项目是 `Survivalcraft.ModTemplates/Survivalcraft.ModTemplates.csproj`，它会把这些资源打包成可发布的 `dotnet new` 模板包。

## 世界操作扩展点

`Gameplay.OnTerrainCellChanging` 在地形写入前运行；通过 `DestroyCell` 破坏方块时，也在掉落物、粒子和收获回调之前运行，取消后不会产生这些副作用。破坏过程只调用一次该钩子。`BlockBehaviors.OnBlockPlaced` 名称虽然是过去式，但实际在放置写入之前调用，提供最终落点，可以取消放置；不要在此回调中假定方块已经写入地形。

`Gameplay` 还提供以下局部扩展点，沿用注册优先级及模组停止时清理的规则：

- `OnCellIgniting`：点火前，可以取消，包含目标坐标和发起者。
- `OnExplosionPointProcessing`：爆炸传播点处理前，可以取消该点，包含目标坐标和发起玩家。
- `OnTerrainCollisionBoxes`：身体地形碰撞查询时，可向本次查询的缓冲区贡献碰撞盒；不要保留或在回调后修改该缓冲区。自定义碰撞盒可设置 `Collided` 回调，仅在该碰撞盒实际阻挡身体移动时调用。
- `OnMounting`：开始骑乘前，可以取消；客户端请求和服务端执行经过该入口，客户端应用权威骑乘结果不重新授权。
- `OnPistonBlockMoving`：活塞推拉扫描前，可以取消该扫描位置，包含活塞位置、目标位置和推拉方向。
- `OnMovingBlockSetTerrainCollision`：移动方块集地形碰撞查询时，可调用方块集的 `Stop()`；范围是最小坐标包含、最大坐标不包含的格子范围。

这些钩子不是自动的领地或权限系统，模组需要自行定义业务规则和服务端身份检查。直接应用权威网络结果、生成地形或底层地形写入不等于玩家操作，不应据此假定所有写入都经过这些入口。

## 世界业务数据

模组可以把不属于实体、组件或子系统结构的业务数据保存到当前世界的扩展节点：

```csharp
using Game.Modding;

// 在能够取得当前 Project 的世界回调或业务操作中使用。
var data = context.GetWorldData(project, "territories");
data.SetValue("enabled", true);
```

`GetWorldData` 自动使用 `context.Manifest.Id` 作为所有者，第二个参数是模组内稳定的数据标识。返回的 `ValuesDictionary` 属于该世界，修改会随下一次世界保存写入 `Project.xml` 的 `ExtensionData`；不需要单独调用保存，也没有额外的模组卸载生命周期。不要在 `Configure` 或 `Start` 阶段假定世界已加载，不要把字典缓存到跨世界的静态状态中。

载荷复用现有 `ValuesDictionary` 序列化，使用嵌套字典、字符串、数值及已有可序列化值类型，不直接存储模组自定义 CLR 对象。玩家相关业务数据可使用玩家 GUID 作为字典键；不会自动解析实体引用或级联清理数据。扩展数据默认不发送给联机客户端，需要同步时使用模组自己的网络消息。

所有者分组使维护工具能够识别和整组清理业务数据，但不是权限沙箱，也不提供模组实体、方块、物品的来源追踪或完整卸载能力。真正属于实体、组件或子系统的数据仍可使用原有序列化机制，不要求全部搬到扩展节点。

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

命令结果通过带有 flags 语义的 `CommandResultPresentation` 声明展示范围。默认结果同时进入
`History` 消息记录并显示在 `Overlay` HUD；较长的帮助和列表结果应仅使用 `History`；短暂的状态通知可以仅使用 `Toast`；无需向玩家展示时使用 `Silent`。展示范围与
`CommandResultAudience` 相互独立，不应通过受众范围推断 UI 展示方式。

命令说明使用通用的 `Game.Localization.LocalizedText`，注册时不会读取当前语言。候选菜单和帮助信息在展示时解析资源，因此初始化语言或运行时切换语言都不需要重新注册命令。模组应在自己的语言资源中提供对应 section 和 key；只有 GUID 等运行时数据才应显式使用 `LocalizedText.Literal(...)`。
联机世界命令候选由服务端使用当前连接玩家的主体、权限和 `SuggestionProvider` 生成，客户端只合并本地 Application 候选。响应保留说明的 section/key/fallback，由客户端解析语言，不同步提供器使用的完整业务数据。提供器应只返回该玩家可见的信息，不应改变世界状态；输入补全不会执行命令。已输入的字面子命令优先于同位置的通用参数路线，避免 `player` 等子命令被当作玩家名称继续提示坐标。
`CommandLiteral` 可提供独立的 `LocalizedText` 说明，用于主体分组等中间层候选；未提供时使用整条路线说明。
`CommandDefinition<TCommand>` 的 `panelBehavior` 声明消息面板的执行后行为，默认 `CommandPanelBehavior.ReturnToMessage`。需要成功后回到游戏的命令可注册 `CloseOnSuccess`。行为随命令结果返回，只作用于提交该请求的面板；失败和 Pending 不关闭，异步命令须在最终结果中携带该注册行为。用户开始新输入、重新打开或切换面板后，旧请求不会改变当前面板。

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

普通游玩和交互验证优先使用 [游戏自动化](Automation.md) 的组合输入、库存拖放、角色观测和步行导航。
Mod 适合准备确定性场景和补充观测，不应通过直接改位置、库存或调用交互回调代替被测操作。
