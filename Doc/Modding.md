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
dotnet build ExampleMod/ExampleMod.slnx
```

包输出位置：

```text
ExampleMod/ExampleMod.Client/bin/<Configuration>/<TargetFramework>/packages/<mod-id>.scpkg
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

方块显示名称和说明在客户端表现层按当前语言解析。带变种的方块先提取箭种、材质等语义编号，
再查询 `Blocks.<类型名>:<变种>.DisplayName` 等资源；模型节点名和配方材料标识用于资源及规则关联。
颜色、季节组合名称使用 `BlockNames.Colored` 和 `BlockNames.Seasonal` 格式模板，分别接收修饰名称和基础名称两个参数。
跨网络的提示、拒绝与断开原因携带 `LocalizedText` 资源和参数，由接收端解析语言。


图像资产使用普通直通 Alpha，默认采用无损 WebP，也支持 PNG。默认内容查找优先选择 WebP。
`Image` 保留原始像素；`Texture2D.Load` 在上传 GPU 前由 CPU 预乘，不修改输入图片，资源作者无需预乘。
GUI 图集也输出直通 Alpha 的 WebP，约定见 [图集工具](../GuiAtlasTool/README.md)。
自定义着色器应使用预乘纹理的混合约定；对纹理数据的底层指针或数组上传仍由图形调用方保证 GPU 像素表示。

模板的规则项目引用 `SCNET.Survivalcraft.Core` 和 `SCNET.Survivalcraft.Hosting`。客户端项目引用规则项目、`SCNET.Survivalcraft.Client`，并私有引用 `SCNET.ContentTool`。
工具包携带的构建目标会在构建后创建和验证 `.scpkg`，宿主运行时程序集不会复制进包。

仓库内的信标和领地石模组已将方块规则与客户端表现分到两个项目。规则项目引用
`SCNET.Survivalcraft.Core` 与 `SCNET.Survivalcraft.Hosting` 编译契约；`.Client` 项目引用 `SCNET.Survivalcraft.Client`，通过项目引用带入规则程序集，并私有引用
`SCNET.ContentTool`，由它生成包含两个程序集的同一个 `.scpkg`：

```bash
dotnet build Mods/Beacon/Beacon.slnx
dotnet build Mods/TerritoryStone/TerritoryStone.slnx
```

产物分别位于对应 `.Client/bin/<Configuration>/<TargetFramework>/packages/`。
只构建 `BeaconMod` 或 `TerritoryStoneMod` 规则项目不再产包。本地开发前仍需准备
`Publish/NuGet` 中的当前 SDK 与工具包；没有改成相对路径导入构建 Target。
模板默认生成同样的双项目结构及客户端内容入口。

模板资源位于 `Survivalcraft.ModTemplate/Survivalcraft.Mod/`。解决方案里唯一与模板打包直接相关的项目是 `Survivalcraft.ModTemplate/Survivalcraft.ModTemplate.csproj`，它会把这些资源打包成可发布的 `dotnet new` 模板包。

## 模组定义与世界状态

代码包通过 `metadata.entrypoint` 指定共享规则入口，纯数据包填写 `null`；不提供按 GUI 与独立服务器
选择的分侧入口。`Configure` / `Start` 只注册共享定义，不在其中启动世界计时器、修改地形或创建表现资源。
同进程中的模组包、程序集以及 `Configure` / `Start` 注册只加载一次；单机和 GUI 开服仍有
独立的 Authority 与 Client 世界。注册回调不能把当前世界、计时器、玩家缓存或待同步数据保存到
模组入口字段、共享闭包或静态变量中。世界业务通过回调的 Project 取得 `WorldRuntimeContext`，
以 `IsAuthority` 判断模拟职责，以 `HasPresentation` 判断能否创建 UI、声音与渲染资源。
`Gameplay` 与 `BlockBehaviors` 上下文统一实现 `IWorldHookContext`，其 `Project` 明确指向所属世界；构造上下文时组件和子系统必须已经绑定世界。分发器在每次调用处理器前检查该世界，关闭或释放后不再开始后续处理器。更新调度器同样在延时回调、世界钩子与组件之间检查运行时状态；关闭会中断本轮剩余工作，游戏时间不再推进，也不接收新的游戏时间回调。同步业务调用者在钩子返回后同样检查世界是否仍可用；世界关闭与业务 `Cancel` 相互独立，关闭后不再开始地形写入、扣物、伤害等后续操作。
附近交互动作的查询和执行上下文也提供所属 `Project`。查询期间关闭世界会停止后续 provider；解析得到的动作仅能在原世界仍活跃、提供它的注册仍存在时执行，不能跨世界复用或在模组卸载后执行。

地形环境查询使用所属世界的 `SubsystemTerrain.Environment`，不为客户端构造区块生成器。内容可用、高度与光照完成、业务初始化完成和 GPU 可绘制是独立能力，不以一个综合就绪值替代；权威初始规范化产生的改块并入首份内容基线。

生成完成钩子、区块初始化及卸载业务回调在所属世界的更新线程、地形访问门外执行；回调中改块仍通过正常地形写入入口获取短访问租约，不直接接管后台线程。卸载会先预留保存容量，回调后重新验证分配身份并捕获最终脏数据。未被既有钩子框架隔离的初始化或卸载异常会关闭世界，不重试已经产生副作用的通知；这不改变模组分发器原有的异常隔离规则。详细边界见[地形线程所有权](TerrainDistribution.md#线程所有权)。

`metadata.clientContentEntrypoint` 可选，指向实现 `IClientContentModule` 的客户端内容入口，
与 `metadata.entrypoint` 一样使用带程序集名的类型名称。客户端应用通过该包的
`IModEntrypointLoader` 创建模块，以其 `BlockPresentations` 显式注册方块表现工厂；
预览和各客户端世界分别持有表现目录，规则目录不反查客户端资源。
共享规则入口的创建不会解析客户端入口类型或加载其程序集，独立服务器不装配该内容入口。
业务仍只有一个共享 `IMod` 入口，不因 GUI、独立服务器、本地或远程客户端另建业务实现。

模板生成一个 `.slnx`、共享规则项目、客户端项目和同一份包清单。客户端项目引用规则项目并负责将两份程序集打入一个 `.scpkg`。
内置示例各自拥有独立解决方案，见[信标](../Mods/Beacon/README.md)与[领地石](../Mods/TerritoryStone/README.md)。
领地石模组的边界绘制、着色器、设置面板和审批弹窗由 `TerritoryStoneMod.Client` 所有，
按 `Presentation` 子系统模板装配。共享规则子系统保存领地与请求数据，处理权威权限和同步，
通过事件交付本地交互与效果；客户端订阅这些事件并在世界释放时解绑。独立服务器不解析或实例化该表现子系统。

这是当前方块表现的装配契约，不代表其余模组 UI、声音与世界回调已经全部分离，
也不代表独立服务端产物已完成。

子系统的可变状态直接放在子系统实例中；需要在多个回调间共享，或在 Bootstrap 尚未创建 Project
时保存的临时状态，可在 `Configure` 中注册世界状态工厂：

```csharp
var states = context.Worlds.Register(
    new ResourceId(context.Manifest.ModId, "world_state"),
    runtime => new MyWorldState(runtime));

context.Gameplay.OnWorldUpdating(update =>
{
    var runtime = WorldRuntimeContext.For(update.Project);
    states.Get(runtime).Update(update.DeltaTime);
});
```

`MyWorldState` 实现 `IDisposable`。工厂在某个运行时第一次 `Get` 时调用；同一注册在同一运行时
返回同一实例，在不同运行时必须返回不同实例，不能返回单例。工厂可能在 Project 加载前调用，
不得假定子系统已经存在。按世界更新线程访问此状态；异步工作使用 `runtime.Lifetime` 取消，
结果通过所属世界的消息或更新流程应用，不直接从后台线程修改世界。

运行时关闭立即取消 `Lifetime` 并拒绝新的状态访问；已绑定 Project 的状态等到该 Project
完成实体和子系统清理后才逆序释放，未绑定 Project 的状态则直接释放。单个清理失败不会阻止
其他状态释放。释放客户端世界不会清理 Authority 状态；仍有状态实例时禁止卸载模组宿主。
该状态注册不提供持久化，存档数据仍使用下文的世界扩展数据或所属子系统的保存逻辑。
与世界相关的实时时间延迟回调使用 `runtime.QueueTimeDelayedExecution(deadline, action)`，
截止时间采用 `Time.FrameStartTime` 的时间基准。回调在所属世界更新时执行，同一轮回调中新排入的工作
留到下一轮；世界关闭会立即清空待执行回调。不要把捕获 Project、组件或子系统的回调放入
进程级 `Time.QueueTimeDelayedExecution`，否则保存退出或重载后仍可能访问已经释放的世界。

### 生物玩法姿态

生物模板组合 `CreaturePose`，通过 `Kind` 声明 Human、FourLegged、Bird、FlightlessBird、Fish 或 Generic。
眼位、朝向和攻击/进食指令使用 `ComponentCreature.ComponentCreaturePose`；不从 `ComponentCreatureModel` 读取玩法状态。
模型只在表现侧更新，攻击阶段由 Pose 驱动；自定义模型与 AI 应分别消费/写入同一 Pose。
身体快照携带攻击、进食、阶段、计数及鱼类弯曲/钻泥状态，迟到基线使用 Pose 的保存值。

### 开场与规则提示

`Intro` 同时组合 `IntroPresentation`，内置角色基模板已配置。
`ComponentIntro` 只保存一次性开场指令及共享出生点几何查询，不再运行镜头状态机。
首次出生不依据宿主的本机标志分叉；客户端收到角色并报告就绪后，权威侧消费该指令并保存，重连不重播。
本机表现组件读取初始指令，负责镜头、提示与客户端临时 `IntroShip`；其他玩家副本和 独立服务器不创建场景船。
演出及提示使用自身起点计时，移除或释放后取消旧回调、清理临时船；临时船不进入权威存档。
合成与熔炉的配方提示通过既有消息链路投递给规则选中的角色，只显示 Toast，不创建额外表现组件。
这不改变现有参与角色选择或库存访问授权；方块配方当前仍沿用既有的附近角色选择规则。

### 角色局部反馈与物件损伤

`Character` 同时组合 `CharacterPresentation`，内置角色基模板已配置。
角色协调器通过 `SteedSpeedFeedback` 和 `CreativeBlockPicked` 提供反馈候选事件；
客户端表现负责本机骑乘呼喊、选块提示和点击音效，实体移除或释放时解除订阅。
`AimHintRequested` 仅表示瞄准提示候选；表现组件合并待播请求，延迟三秒后检查仍在瞄准且未潜行，
每角色生命周期只提示一次，移除、释放后的旧回调不会在重新加入时生效。
`ComponentDamage` 只在权威侧推进模拟；客户端应用损伤状态并发布 `Attacked`、`Destroyed` 事件，
表现代码读取当前 `HitPoints` 或监听事件，不依赖已移除的 `HitPointsChange` 临时变化量。
`ComponentGlowingEyes` 本身就是表现组件，不再拆类；它只在客户端加载依赖、分配发光点，并在移除或释放时清理。

### 船、移动与发射器

`Boat`、`Locomotion` 分别组合 `BoatPresentation`、`LocomotionPresentation`，内置基模板已配置。
两侧保留现有运动计算；船的浮力密度根据 `ComponentDamage.HitPoints` 推导，浸没损伤和强制下船只在权威侧执行。
客户端沉船音效独立比较生命值阈值，初始低生命值和重新加入不会重放历史受损提示。
`ComponentLocomotion` 通过 `FootstepRequested`、`FootwearHintRequested` 发出本地表现候选事件；
表现组件负责材质音效、鞋子建议与显示节流，移除或释放时解除订阅，玩法噪声保留在权威侧。
`ComponentDispenser.Dispense` 和 `DispenseItem` 只允许活动的权威世界调用；库存消耗、射出判定及失败后的普通投放只计算一次。
实际执行结果通过独立的 `DispenserSoundPackage` 向附近客户端发送，由声音接收器播放；原 `DispenserPackage` 继续只表示配置请求。

### 服装规则与皮肤表现

使用 `Clothing` 的角色同时组合 `ClothingPresentation`，内置角色基模板已配置。
`ComponentClothing` 保存服装库存、密度、保暖与骑乘速度属性，穿着等级检查、磨损、护甲损耗与进食、掉落只在权威侧执行。
库存同步通过 `SetClothes` 更新两侧状态，并通过 `ClothesChanged` 通知表现失效。
`ComponentClothingPresentation` 持有内外层合成贴图、皮肤、模型引用及设备重置订阅；界面从它读取当前贴图。
移除或释放时解除订阅、清除模型上的自有贴图并释放渲染目标和自有皮肤；共享内容纹理由内容系统管理。
客户端缺失皮肤仍使用现有皮肤请求链路，不由服务端创建纹理或发起图形更新。
服装命中、破损和等级限制反馈通过 `ClothingEffectPackage` 权威事件发送给实体观察者；客户端负责音效和碎屑，提示只显示给本机玩家。

### 等级与经验表现

使用 `Level` 的实体同时组合 `LevelPresentation`，内置角色基模板已配置。
规则只在权威侧调用 `ComponentLevel.AddExperience`；经验消息携带精确的最终等级与权威计算的跨级次数，
同步给实体观察者，客户端使用 `ApplyExperienceState` 应用结果，不重算经验增长。
`ComponentLevelPresentation` 负责本机等级标签、经验收集音效和延迟升级反馈；其他玩家的副本只更新状态。
移除、释放后，旧一轮实体生命周期中排队的反馈不会在重新加入时播放。最高等级统计随状态更新维护，
属性因子仍由两侧读取当前状态计算，不依赖表现组件或音频子系统。

### 挖掘与攻击表现

使用 `Miner` 的实体同时组合 `MinerPresentation`，内置模板已配置。`ComponentMiner` 保留地形预测、
工具可用性、交互规则与挖掘阶段；提示、材质音效、碎屑及挥击音效通过事件交给客户端表现组件。
表现组件在实体移除或释放时解除订阅，工具建议的显示节流独立于规则计算。
客户端调用或回放 `Hit` 只产生挥击反馈，随机命中、伤害、工具损耗、击退和近战统计由权威侧处理。
`AttackBody` 只允许活动的权威世界；投射物等调用方也遵循这一限制。
`CharacterHitEffectPackage` 的浮字与身体命中音效可分别存在，接收端不再根据浮字重复播放挥击声。
这些变化不替代攻击请求的距离、视线及目标合法性校验；移动和交互输入的完整校验仍需独立完善。

### 身体流体表现

使用 `Body` 的实体同时组合 `BodyPresentation`，内置角色、生物及船基模板已配置。
`ComponentBody.ImmersionUpdated` 在身体更新完成浸没采样后触发，并携带本次物理步的起始位置；
客户端表现组件负责入水声音和水花、岩浆飞溅，移除或释放时解除订阅。
物理碰撞、浸没状态和浮力仍由 `ComponentBody` 计算，不受播放结果影响。

### 生物声音

使用 `CreatureSounds` 的实体同时组合 `CreatureSoundsPresentation`，内置基模板已配置。
AI 等权威规则调用 `ComponentCreatureSounds.Play*Sound` 发布声音事件；客户端表现调用
`ComponentCreatureSoundsPresentation.Play*Sound` 或 `PlayFootstepSound`，不会把播放结果上报为规则结果。
音频路径、距离与自定义脚步材质仍配置在 `CreatureSounds` 模板，由表现组件读取共享定义；权威组件不依赖音频子系统。
AI 进食摩擦声使用权威 `PlayFeedingSound` 事件，客户端再采样脚步材质。
嚎叫、振翅和产卵声分别使用权威 `PlayHowlSound`、`PlayWingsSound`、`PlayEggLaidSound` 事件；
嚎叫音频路径仍配置在 `HowlBehavior.HowlSoundName`，由声音表现组件读取，行为不绑定音频子系统。
鲸类实体同时组合 `CetaceanBreatheBehavior` 与 `CetaceanBreathePresentation`，内置基模板已配置。
前者决定呼吸与喷气起止，后者独立调度消息驱动的喷气粒子和声音，不依赖鱼类模型调用；
移除、释放或收到停止事件时停止发射并清除待播事件。
实体移除、组件释放或世界关闭后拒绝本地播放。已有 Gameplay 噪声逻辑保持权威，不能用“播放成功”决定 AI 感知。

### 世界更新执行侧

实现 `IUpdateable` 时必须显式声明 `UpdateSide`：`Authority` 用于权威规则，`Client` 用于客户端状态，
`Presentation` 用于需要图形界面的客户端工作，`Both` 用于两侧各自执行的共享计算。
`Disabled` 可用于当前实例不参与更新的条件；声明不合法会在注册时失败。
同一类型在两个世界中的实例与状态独立，`Both` 不代表共享实例或自动复制字段。

自定义实体使用燃烧、变身、生命或耐久组件时，应同时组合对应的 `OnFirePresentation`、
`ShapeshifterPresentation`、`HealthPresentation`、`DamagePresentation` 模板；内置实体已由基模板继承这些组合。
表现模板在状态模板之后加载，Authority 实例不会绑定它们的表现资源。
耐久组件的 `DebrisTextureSlot`、`DebrisStrength`、`DebrisScale`、`DestructionSoundName` 配置归属 `DamagePresentation`。
伤害使用权威组件的 `Injure` / `Damage`；客户端只应用权威状态，不通过伤害规则重放统计、掉落或实体删除。
使用 `Sickness` 模板的实体同时组合 `SicknessPresentation`；疾病和呕吐只能通过权威组件发起，客户端只重放带序号的效果状态。
`Flu`、`Sleep`、`VitalStats` 分别组合对应的 `FluPresentation`、`SleepPresentation`、`VitalStatsPresentation`。
进食通过权威库存处理调用 `Eat`，客户端不再有 `NetEat` 规则入口；强制睡眠仅由权威 `Sleep(false)` 发起，玩家请求不能指定唤醒策略。



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
规则目录统一初始化碰撞盒、配方、发光强度等模拟数据，不通过表现能力分支加载图形资源。
GUI 预览和客户端世界分别创建 `BlockPresentationCatalog`，由其中的表现对象初始化模型和纹理；
Authority 不创建表现目录，即使它与 GUI 客户端位于同一进程。
方块表现统一在客户端地形协调线程生成网格，GPU 上传仍由图形线程处理。
内置方块的固定编号集中维护在游戏的静态注册表中。

GUI 的 `PreviewCatalog` 服务帮助、图鉴和创建世界预览，游戏内打开帮助仍使用预览目录。
实际世界的目录由独立的内容所有者提供，通过 `WorldContentBinding.For(project).Catalog` 取得。一个模组环境可以创建多份世界目录，
各自持有方块实例、可变缓存及解析后的配方；资源定义、模型与纹理可以共享。
两端使用相同编号映射，不代表可以共用方块实例或跨目录传递解析后的配方。
`CurrentModRuntime` 表示已加载的模组环境，不提供全局“当前世界目录”；世界相关代码必须使用
所属运行时或显式接收目录。独立服务器不建立预览目录。

`FurnitureDesign` 的构造参数显式指定目录和可选地形；指定地形时，目录必须属于同一世界。
家具包校验、网络快照等纯数据解析可以明确传入空目录，但不能对这些对象执行染色或生成绘制几何。
游戏内导入家具使用世界目录，内容管理界面使用预览目录；克隆保留原目录归属。

关闭运行时会停止消息处理；已经绑定 Project 的目录保留到该 Project 的资源释放完成，
随后释放目录。未绑定 Project 的目录随运行时关闭释放。卸载模组环境之前必须先释放其所有世界。

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
`Send<T>(runtime, messageType, payload, audience)` 通过协议统一配置编码，处理上下文使用 `Read<T>()` 解码，
`Reply<T>(payload)` 回复类型化载荷。传输前采集数据，不直接序列化运行时子系统或可变世界对象。
原始 `byte[]` 重载只传递模组自行定义的 opaque 载荷，游戏不解析其内部结构。
发送时必须提供所属 `WorldRuntimeContext`，不能依赖全局当前网络节点。
消息处理上下文提供 `Runtime` 和 `Project`，`Reply` 与上下文的 `Send` 自动使用原接收运行时；
原始字节发送会捕获独立副本，调用后修改原缓冲不会改变待投递内容。
世界相关状态应保存在所属 Project 的 Subsystem 或运行时对象中，不能缓存单个全局当前世界。
`Game.Components.Behavior.ComponentBehavior` 的自动更新属于权威模拟；客户端表现应由普通组件按所属世界职责更新。行为选择器、寻路和导航控制不在客户端推进；本机坐骑的输入预测独立于 AI 状态机。
`ComponentSpawn.Despawned` 是权威退场通知；客户端资源清理使用组件的 `OnEntityRemoved`，不要在客户端据退场计时生成实体或掉落物。
死亡时的库存 `DropAllItems` 调用也只在权威世界发生，包括自定义库存覆盖实现；客户端生命值重放仍更新死亡时间和视觉反馈，但不提交库存掉落。

`context.Network.Send(runtime, messageType, payload, audience)` 与消息处理上下文的 `Send`
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
- `OnCreatureInjuring`：权威运行时应用伤害前，可以取消或调整伤害；客户端重放权威伤害结果不重新触发该钩子。
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
using Game.Commands;
using Game.Commands.Http;
using Game.Commands.Text;
using Game.Localization;
using Game.Modding;
using Game.Protocol;
using Game.Protocol.Localization;
using MessagePack;

[MessagePackObject]
public sealed record EchoCommand([property: Key(0)] string Text) : IGameCommand;

public sealed class EchoMod : IMod
{
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
                supportsRemoteInvocation: true));

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
                arguments => new EchoCommand(arguments.Get<string>("text")),
                new HttpCommandArgumentDefinition("text", "string")));
    }

    public void Start(IModContext context) { }

    public void Stop() { }
}
```

HTTP、文本和 UI 入口均先构造命令对象，远程执行共用 `ProtocolPayloadCodec` 的 MessagePack 编解码；服务端不会重新解释 HTTP 或文本参数。需要远程调用的命令须设置 `supportsRemoteInvocation: true`，并为命令数据声明 `[MessagePackObject]` 与连续的数字 `[Key]`；默认不支持远程调用，只能在权威进程本地执行。客户端构造和编码不代表授权，服务端仍按实际连接玩家的权限执行。

文本适配器、命令补全与可调用性查询由调用方显式传入所属 Project；应用级查询可传空值。
`CommandSuggestionContext.Project` 固定为该目标世界，不回退到全局当前世界；尚无角色对象的已准入玩家也使用会话所属世界查询。

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

命令说明使用通用的 `Game.Protocol.Localization.LocalizedText`，注册时不会读取当前语言。候选菜单和帮助信息在展示时解析资源，因此初始化语言或运行时切换语言都不需要重新注册命令。模组应在自己的语言资源中提供对应 section 和 key；只有 GUID 等运行时数据才应显式使用 `LocalizedText.Literal(...)`。
联机世界命令候选由服务端使用当前连接玩家的主体、权限和 `SuggestionProvider` 生成，客户端只合并本地 Application 候选。响应保留说明的 section/key/fallback，由客户端解析语言，不同步提供器使用的完整业务数据。提供器应只返回该玩家可见的信息，不应改变世界状态；输入补全不会执行命令。已输入的字面子命令优先于同位置的通用参数路线，避免 `player` 等子命令被当作玩家名称继续提示坐标。
规则文本使用同一个结构化 `LocalizedText` 值：原文用 `Literal`，普通资源用 `Resource`，数据库名称用 `Database`，格式化文本用 `Format` 及嵌套文本参数。伤害原因、死亡历史与消息内容保存这些值，在客户端或控制台显示时才解析语言；不要先翻译后把结果写回规则数据。聊天入口只接受原文，以便权威侧直接执行内容过滤。协议与存储读取校验文本树的深度、节点数及总字节数，不读取旧的死亡历史字符串拼接格式。

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

`GET /commands` 只返回当前 HTTP 主体在当前运行模式下可能执行的 binding，并提供 identity、本地化说明及参数的 `name`、`valueType`、`required`。只有注册了同 identity `HttpCommandBinding` 的命令才会暴露。HTTP 宿主通过 Bearer Token 认证，GUI 与 独立服务器都使用可信的 `LocalOperator` 身份；调用入口记录为 `CommandInvocationChannel.HttpApi`，命令仍会执行正常的命令域、权限和宿主环境校验。宿主只监听 loopback，且仅在有效 session 或实例 `Settings.xml` 启用时启动；实例默认配置、启动参数和可保存的 session 覆盖见 [Server.md](./Server.md)。

GUI 的 `GET/POST /player/commands` 使用当前真实玩家，而不是 LocalOperator。它只开放 World/Server binding；联机客户端的查询与调用发送给权威服务器，按服务器保存的玩家权限处理。命令由模组注册不改变此规则，玩家不能通过 HTTP 自行声明主体或权限。独立服务器不提供玩家入口。两组入口的请求格式相同，入口之间不自动回退。

GUI 自动化的操作合同见 [游戏自动化](Automation.md)，Agent 工作流程见项目的
`scnet-debugging` 技能。Mod 适合准备确定性场景和补充观测，不应通过直接改位置、库存
或调用交互回调代替被测操作。

材质包与皮肤包也接受静态 PNG/WebP，推荐无损 WebP；创建、上传和安装都保留原始图片编码及字节，不自动转码，资源作者无需预乘。

## 测试

模组的测试项目使用普通单元测试框架，检查自身的计算、权限数据、存储转换、颜色与电压映射等逻辑。
规则和客户端项目通过正常 SDK 编译，测试项目引用需要验证的模组项目。

游戏集成验证使用构建后的游戏程序和 `.scpkg` 成品：

1. 分别构建游戏和模组解决方案。
2. 在专用测试实例中安装包并启用对应 profile，记录包的精确 PackageHash。
3. 通过正常游戏入口进入新世界，检查模组内容和界面，执行交互、保存并重新进入。
4. 需要自动化时使用现有游戏命令和自动化接口，见 [游戏自动化](Automation.md)。
5. 验证完成后退出实例，保留结果记录并清理测试数据。

信标重点检查电路供电、颜色切换、光柱、掉落和保存后的状态。领地石重点检查领地创建、
玩家申请、审批、权限限制、边界显示和保存恢复。联机场景让服务器与客户端使用相同的包 hash。

游戏内部测试夹具由游戏测试工程维护。发布模组的开发和单元测试以公开 SDK 与模组源码为输入，
真实实例验证以各解决方案的产物为输入。
