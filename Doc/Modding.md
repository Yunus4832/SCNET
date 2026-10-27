# 模组开发

本文档面向模组开发者，说明如何创建项目并打包统一内容包 `.scpkg`。

如果只是配置、下载或启用模组，见 [Mods.md](./Mods.md)。

## 创建项目

当前第一方包暂通过本地源准备。在仓库根目录运行 `./Scripts/pre-build.sh` 后，从本地包目录安装模板：

```bash
dotnet new install ./Publish/NuGet/SCNET.ModTemplates.<模板版本>.nupkg
```

将 `<模板版本>` 替换为 `SharedProperties/Versions.props` 的 `ModTemplatesVersion`。
在仓库外创建模组时，还需将本地 `Publish/NuGet` 的绝对路径加入该项目的 NuGet 源；
详细配置与依赖边界见 [NuGet 包](NuGet.md)。

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

模组可以在 `payload/assets/<mod-id>/lang/<语言代码>.json` 中补充翻译。可选语言及其显示名称只由游戏内置语言文件定义；
模组提供的其他语言代码不会新增语言选项，也不会在运行时加载。

模板项目引用 `SCNET.Survivalcraft` 作为编译期 API，并私有引用 `SCNET.ContentTool`。
工具包携带的构建目标会在构建后创建和验证 `.scpkg`，宿主运行时程序集不会复制进包。

仓库内的 `TerritoryStoneMod` 也只通过相同的 NuGet 包引用构建，不使用项目引用或
相对路径导入 Target。本地开发时先将当前版本包生成到仓库的 `Publish/NuGet` 文件源。

模板资源位于 `Survivalcraft.ModTemplates/Survivalcraft.Mod/`。解决方案里唯一与模板打包直接相关的项目是 `Survivalcraft.ModTemplates/Survivalcraft.ModTemplates.csproj`，它会把这些资源打包成可发布的 `dotnet new` 模板包。

## 方块注册与内容上下文

模组按稳定的 `ResourceId` 注册方块，不指定数字索引：

```csharp
context.Extensions.RegisterBlock<MyBlock>(
    new ResourceId(context.Manifest.ModId, "my_block"));
```

`BlockIndex` 初始为 `-1`，由注册目录在属性加载和 `Initialize()` 之前赋值，模组不能写入。
方块生成自身的创造物、放置值或配方产物时使用 `BlockIndex`，通过 `Catalog` 查询同一目录的其他方块；
不要定义静态数字 `Index`，也不要在模组
`Configure` 或 `Start` 中缓存尚未分配的编号。构造工厂每次必须返回新实例。方块可覆盖 `Dispose()` 清理自身订阅或资源；
清理必须允许初始化尚未完成，不得释放内容系统共享的纹理和模型。
内置方块的固定编号集中维护在游戏的静态注册表中。

GUI 运行时分别提供 `PreviewCatalog` 和 `WorldCatalog`：前者服务帮助、图鉴和创建世界预览，
后者服务实际游戏。游戏内打开帮助仍使用预览目录。两者共享资源定义、模型与纹理，
方块实例、数字编号及解析后的配方分别持有；不存在隐式的当前目录或预览回退。
世界目录不存在时，`RequireWorldCatalog()` 会报告生命周期错误。Headless 不建立预览目录。

`BlocksData.txt` 以分号分隔，首列 `Id` 为完整资源 ID。字段值 `#game:granite` 引用目标
目录中花岗岩的编号，`#` 引用自身，不包含可写的 `BlockIndex` 列。
配方 XML 的 `Result` / `Remains` 同样使用资源 ID，可附加 `@data`，如 `game:seeds@7`；
材料仍使用已有 `CraftingId[:data]` 语义。动物掉落定义的产物使用同一资源 ID 格式。
XML 源定义可以共享，解析后的 `ResultValue` / `RemainsValue` 不能跨目录传递。

方块行为的 `HandledBlocks` 声明 `ResourceId[]`，世界加载时解析为整数分发表；也可继续
使用方块属性中的 `Behaviors` 名称绑定，同一行为不会因两种声明重复执行。
世界映射保存于 `Project.xml` 的 `BlockRegistry` 节点；已有编号保持稳定，新资源使用
未占用编号。联机在可靠有序的 Bootstrap 中传递服务器映射，建立世界目录后才解析
Project 并确认完成，不把完整映射放入连接前的服务器发现数据报。

## 网络消息受众

模组消息使用带 `[MessagePackObject]` 和数字 `[Key]` 的独立数据契约。
`Send<T>(messageType, payload, audience)` 通过协议统一配置编码，处理上下文使用 `Read<T>()` 解码，
`Reply<T>(payload)` 回复类型化载荷。传输前采集数据，不直接序列化运行时子系统或可变世界对象。
原始 `byte[]` 重载只传递模组自行定义的 opaque 载荷，游戏不解析其内部结构。

`context.Network.Send(messageType, payload, audience)` 与消息处理上下文的 `Send`
要求显式传入 `Game.Network.PackageAudience`，没有默认广播或可选 `to/except` 参数。
全局消息使用 `PackageAudience.Global`，定向消息使用 `PackageAudience.To(client)`，
空间消息将统一兴趣查询得到的观察者集合传入 `PackageAudience.To(observers)`。
集合会在创建受众时复制，空集合不会退回全局广播；连接按对象身份匹配。
客户端上行消息应显式指定当前服务端连接，不把上行请求描述成全局广播。
`Reply` 只回复当前接收上下文的原发送者；没有发送者时不发送。

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
using Game.Protocol.Localization;
using Game.Protocol.Commands;

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
            requiredPermission: permission,
            write: static (writer, command) => writer.Write(command.Text),
            read: static reader => new EchoCommand(reader.ReadString())));

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

HTTP、文本和 UI 入口均先构造命令对象，远程执行共用命令定义的 `write`／`read` 编解码；服务端不会重新解释 HTTP 或文本参数。需要远程调用的命令必须同时提供这两个委托；未提供时仅能在权威进程本地执行。客户端构造和编码不代表授权，服务端仍按实际连接玩家的权限执行。

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
- `NonGrantable`：不能授予玩家。

命令入口记录为 `CommandInvocationChannel`，只用于日志和展示，不参与授权。
如果操作在业务上必须绑定玩家实体，可以通过 `allowedPrincipals:
CommandPrincipalKinds.Player` 声明主体要求；这与命令来自消息面板、HTTP 或 stdin 无关。

`Game.Commands.CommandResult` 是游戏业务结果，包含本地发布与展示策略。联网时由游戏转换为精简的 `CommandResponse`，不传输 `Audience`、`Sensitive` 或业务工厂逻辑。

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

完整的身份、路由和授权规则见 [命令权限模型](CommandPermissions.md)。

`GET /commands` 只返回当前 HTTP 主体在当前运行模式下可能执行的 binding，并提供 identity、本地化说明及参数的 `name`、`valueType`、`required`。只有注册了同 identity `HttpCommandBinding` 的命令才会暴露。HTTP 宿主通过 Bearer Token 认证，GUI 与 Headless 都使用可信的 `LocalOperator` 身份；调用入口记录为 `CommandInvocationChannel.HttpApi`，命令仍会执行正常的命令域、权限和宿主环境校验。宿主只监听 loopback，且仅在有效 session 或实例 `Settings.xml` 启用时启动；实例默认配置、启动参数和可保存的 session 覆盖见 [Headless.md](./Headless.md)。

GUI 的 `GET/POST /player/commands` 使用当前真实玩家，而不是 LocalOperator。它只开放 World/Server binding；联机客户端的查询与调用发送给权威服务器，按服务器保存的玩家权限处理。命令由模组注册不改变此规则，玩家不能通过 HTTP 自行声明主体或权限。Headless 不提供玩家入口。两组入口的请求格式相同，入口之间不自动回退。

GUI 自动化的操作合同见 [游戏自动化](Automation.md)，Agent 工作流程见项目的
`scnet-debugging` 技能。Mod 适合准备确定性场景和补充观测，不应通过直接改位置、库存
或调用交互回调代替被测操作。
