# 内容管理迁移临时实施计划

> 状态：临时执行计划。用于约束 ModServer 迁移、统一内容安装以及 FilePicker 落地期间的实现边界。
> 全部验收项完成、正式文档更新并经人工验收后，删除本文档及 README 中的入口。

## 目标

将远程模组仓库收口到 ContentServer，并统一本地文件导入、ContentServer 下载、内部安装、使用和导出流程：

```text
FilePicker 本地导入 ─┐
                     ├─> 校验 ─> ContentPackageCache ─> 按类型安装 ─> 游戏逻辑决定如何使用
ContentServer 下载 ──┘

ContentPackageCache ─> 原包导出 ─> FilePicker 指定的保存目标
游戏资产/素材 ─> 显式制造 ─> 临时 .scpkg ─> FilePicker 保存
保存的 .scpkg ─> 用户显式导入 ─> ContentPackageCache ─> 按类型安装
```

本计划只描述迁移期间的目标和实施顺序。当前受支持行为仍以现有正式文档和代码为准。

## 已确认的设计约束

以下内容视为实施不变量，除非先修改本计划并说明原因：

1. 项目尚未发布，不保留 ModServer API、旧序列化字段、旧配置、旧目录扫描或旧类型名称的兼容层。
2. ContentServer 是最终唯一的远程内容服务；其中模组继承 ModServer 的仓库核心语义：包清单驱动、精确版本查询、不可变版本、匿名下载。
3. 所有类型共用一个按 PackageHash 寻址的 `ContentPackageCache`；原有 ModCache 收敛为该缓存的 Mod 查询视图，不保留独立的模组包存储协议或目录。
4. GUI 与 Headless 使用同一套 ModProfile 解析流程：始终先查询 ContentPackageCache 中的 Mod，仅在缓存缺失时查询 ContentServer 并把下载结果写入统一缓存。
5. 模组进入 ContentPackageCache 不代表启用。只有 ModProfile 决定启用哪些精确的 `ModId + Version`；导入和下载均不得修改 Profile。
6. 删除启动时扫描 `GamePaths.Mods` 并自动导入缓存的行为，不保留目录同步、监听或导入状态文件。
7. Headless 不增加专用导入流程。离线运行和调试继续通过预置/注册目标 Storage、ContentPackageCache 或本地 ContentServer 完成。
8. 所有内容的导入和下载只负责校验、缓存并按请求安装到正确的内部位置；如何选择、启用、进入或使用由对应游戏逻辑决定。
9. 所有交互式导入和导出通过平台注册的 FilePicker 完成。游戏逻辑只使用文件名和流，不依赖系统路径、Android URI 或平台对话框类型。
10. Headless 不注册 FilePicker，也不提供交互式导入导出；FilePicker 不参与启动、Profile 解析或 ContentServer 下载。
11. 导出只写入用户通过 FilePicker 选择的目标，不修改缓存、安装或使用状态。
12. 所有可导入、导出和发布的内容使用同一个内容包协议、manifest 和 PackageHash 算法；内容类型差异只存在于 payload 校验、内部安装位置和使用逻辑。
13. 离线和在线包使用同一组 `Identifier + Version + PackageHash` 识别内容；`ContentId` 只是 ContentServer 生成的数据库记录 ID，不参与本地包识别。非 Mod 资产安装后不再参与任何包身份或版本匹配。
14. 所有 Identifier 全局唯一；Mod 的 Identifier 就是全局唯一 ModId，其他类型由创作工具生成 UUID 形式 Identifier。
15. World、BlocksTexture、CharacterSkin 和 FurniturePack 安装后均成为与来源包无关的游戏资产；包身份只保留在 ContentPackageCache，不写入或绑定安装资产。
16. ContentServer 包文件存入本地内容寻址文件目录，SQLite 只保存元数据、状态、关系和审核记录，不保存包或草稿 BLOB。
17. ContentPackageCache 只保存以 `PackageHash` 寻址的原始 `.scpkg`；GameModRuntime 直接从其中读取 Mod payload，其他类型安装时从中派生游戏资产。缓存不解包到目录、不转换为旧格式，也不维护第二份派生内容包。
18. “制造并保存新包”与“导出缓存包”是两个操作：制造从游戏资产或 FilePicker 素材在临时区生成并校验新 `.scpkg`，再由 FilePicker 保存，不自动写入缓存或安装；缓存包导出只复制已经导入/下载的缓存制品。用户要使用制造结果时必须显式重新导入。
19. 非 Mod 安装成功是内容包职责的终点；内容包管理 Screen 不持有安装关系、不展示已安装状态，也不负责重命名、使用或删除派生资产。后续生命周期完全由现有对应游戏资产管理 Screen 负责。

## 立项基线与已知缺口（实施前）

- `Engine.FileStorage` 已定义 `IFilePicker`、`FilePicker`、请求和流式结果模型。
- Windows、Linux、Android 当前均未实现或注册 `IFilePicker`，`FilePicker.IsAvailable` 实际始终为 `false`。
- GUI 和 Headless 启动当前都会调用 `LocalModsImportManager.ImportInstalledMods` 扫描 `GamePaths.Mods`。
- `LocalModsImportManager` 使用系统路径、文件大小和修改时间维护 `LocalModsImportState.xml`，不适合作为 FilePicker 流式导入模型。
- ModProfile 解析已经采用 ModCache 优先、远程仓库补缺的策略，该优先级必须保留。
- ContentServer 已提供 `/api/v1/mods`、精确版本查询和包下载，但模组提交仍由表单提供身份与版本，尚未以 `.scpak` 的 `manifest.json` 为权威来源。
- ModServer/游戏端使用规范包内容 hash，ContentServer 当前使用上传文件原始字节 hash，迁移前必须明确并统一协议含义。
- 当前内容交换格式并不统一：Mod 是带 manifest 的 `.scpak` ZIP，World 是 `.scworld` ZIP，BlocksTexture 可接收 `.scbtex`/`.png` 图像，CharacterSkin 是 `.scskin` 图像，FurniturePack 是含 XML 的 `.scfpack` ZIP。迁移目标是用统一内容包封装这些类型，不让 ContentServer、FilePicker 和内容管理 UI 分别理解五套顶层协议。

## 目标职责边界

### 平台 FilePicker

- Windows、Linux、Android Starter 分别实现并注册 `IFilePicker`。
- 打开操作返回显示名称、可选媒体类型和延迟创建的读取流。
- 保存操作返回显示名称和延迟创建的写入流。
- 用户取消返回空结果，不作为错误。
- picker 负责平台资源生命周期和 URI 权限，调用方不得保存平台路径或 URI。
- Headless 保持未注册状态。

### 统一内容包缓存与安装层

- 本地导入与 ContentServer 下载复用同一套流式缓存入口：写临时文件、校验、按 PackageHash 原子提交到 `ContentPackageCache/<PackageHash>.scpkg`。
- 缓存保存所有类型的原始不可变 `.scpkg`，统一索引 manifest；按类型、Identifier、Version 和 PackageHash 查询，不另设 ModCache 物理存储。
- 对所有类型，`Identifier + Version` 只能对应一个 PackageHash，Identifier 首次出现后 ContentType 不得改变；同版本同 hash 幂等，同版本不同 hash 或同 Identifier 不同类型必须在本地导入和 ContentServer 提交时一致地拒绝。
- 包导出只打开缓存文件并复制到 FilePicker 输出流，不调用安装器，也不从安装后的游戏资产重建包。
- 本地导入与 ContentServer 下载在缓存成功后复用同一套类型安装处理器；调用方也可以从缓存重新执行安装。
- 缓存提交与安装是两个事务边界：缓存成功后安装失败应保留已验证包并返回安装错误，允许用户稍后重试；不得因安装回滚删除原包。
- 第一阶段不做自动过期或容量驱逐。缓存删除是独立的显式操作；非 Mod 包删除不影响已派生资产，被任一 ModProfile 引用或当前 Runtime 使用的 Mod 包必须拒绝删除并说明引用方。
- 相同 PackageHash 重复写入时幂等命中已有缓存制品；即使 ZIP 压缩参数等物理字节不同，也不建立第二个逻辑包。导出复制当前缓存实际保存的那份制品。
- 缓存文件不可原地修改。安装、ModRuntime 首次加载和原包导出在消费时必须依据实际文件重新验证结构与 PackageHash，不能只信文件名或索引；发现损坏时隔离缓存项、报告错误并允许从 ContentServer 重新获取。
- 安装入口接受流，不要求系统路径，也不要求先完整加载为 `byte[]`。
- 安装位置只由对应类型处理器知道，UI 和远程客户端不直接拼接内部路径。
- 非 Mod 安装器调用对应资产 Manager 创建或显式替换资产。创建模式由 Manager 生成独立稳定的本地 `AssetKey`；替换模式保留用户所选同类型目标的 AssetKey。该键不等于包 Identifier、ContentId 或 PackageHash，显示名称可变且不能充当持久引用键。
- 安装结果只返回 `ContentType + AssetKey + DisplayName` 等本地句柄，不持有来源包身份，也不产生“启用”“当前选中”或“立即加载”的副作用。

内容包和游戏资产具有两个独立生命周期：

```text
包生命周期：外部 .scpkg -> 导入/下载 -> ContentPackageCache -> 原包导出/显式移除
资产生命周期：缓存包 -> 安装 -> 独立游戏资产 -> 游戏使用/显式删除
```

- UI 不使用含混的“卸载内容包”：内容包管理 Screen 对缓存对象只提供“从缓存移除”；现有游戏资产管理 Screen 对派生对象提供“删除本地资产”。二者不得级联或持有反向关系。
- Mod 是例外：它不产生派生资产，运行时直接依赖缓存包；Mod Screen 提供停用/从 Profile 移除以及从缓存移除，而不是普通资产卸载。
- World 的删除继续属于世界管理界面，并采用现有世界删除确认与生命周期；内容包管理 Screen 不把它显示为来源包的已安装项。
- 现有皮肤/材质资产管理 Screen 提供删除本地资产；若仍被任何 World、玩家配置或当前会话引用则拒绝直接删除。资产 Screen 应列出引用，并允许用户先选择替代资源后再执行删除，不能留下悬空引用或静默回退。
- 现有家具资产管理 Screen 提供删除本地资产；若未来存在持久引用，同样使用“阻止删除或先替换引用”的规则，不根据来源 PackageHash 判断。
- World、BlocksTexture、CharacterSkin 和 FurniturePack 安装均默认创建新资产，也允许用户明确选择同类型本地资产进行替换。替换必须显示引用/影响范围并二次确认，不能依据包 Identifier、版本、名称或历史来源自动选择目标。
- 所有替换先写入并验证暂存数据，再以可恢复的原子操作提交；失败时保留原资产。目标正被运行时使用且对应 Manager 无法安全热替换时应拒绝或延迟到退出当前会话，不能产生半更新状态。
- 内置皮肤、默认材质等使用稳定的保留 AssetKey，并标记为只读；它们可以被选择使用，但不能成为安装替换或删除目标。内容包安装只能创建或替换用户资产。

类型与目标位置：

| 类型 | 内部安装目标 | 使用方 |
|---|---|---|
| Mod | 不派生安装资产；直接保留在统一包缓存 | ModProfile / GameModRuntime |
| World | 世界存储 | 世界选择与加载逻辑 |
| BlocksTexture | 材质包存储 | 材质选择逻辑 |
| CharacterSkin | 皮肤存储 | 角色外观逻辑 |
| FurniturePack | 家具包存储 | 家具导入与使用逻辑 |

### 统一内容包、身份与 hash

所有内容使用一个统一的仓库交换包，公共扩展名固定为 `.scpkg`。包是 ZIP 容器，
至少包含根目录 `manifest.json` 和 `payload/`；ContentServer、FilePicker 导入、导出和本地安装只先理解这一种外层协议。

manifest 的最低公共字段：

```text
formatVersion   统一内容包协议版本
type            Mod / World / BlocksTexture / CharacterSkin / FurniturePack
identifier      全局唯一、跨版本稳定的逻辑内容标识
name            显示名称
version         SemVer 2.0 不可变发布版本
payload         入口、媒体类型及类型专属格式标识
metadata        类型专属结构化元数据
```

统一身份规则：

- `Identifier`：位于 manifest，跨版本稳定并全局唯一；离线包缓存、ContentServer 发布关系和包版本查询使用同一值，不用于匹配安装后的非 Mod 游戏资产。
- `Version`：位于 manifest，采用 SemVer 2.0；与 Identifier 共同定位逻辑版本。
- `PackageHash`：由完整逻辑包计算，精确标识该版本的不可变制品。
- `ContentId`：只存在于 ContentServer 数据库和 API 响应，由服务端生成，用于内部关系与管理路由，不写入包且不参与本地识别。

身份与信任不是同一概念：PackageHash 证明包内容完整性，Identifier 表达逻辑身份，但二者本身不证明离线文件的 Publisher。首个协议明确不实现 Publisher 签名、密钥管理或可离线验证的签名回执，以免扩大本次迁移范围：

- ContentServer 下载只能标记为“来自该仓库并通过审核”，依据账户归属、审核状态和安全连接，不能声称包具有密码学发布者签名。
- FilePicker 导入包标记为“本地未验证来源”；尤其 Mod 导入必须明确提示其包含可执行代码。
- 未来签名采用与 `.scpkg` 分离、以 PackageHash 为目标的 attestation/receipt，不修改包、Identifier、Version 或 PackageHash，也不把签名字段塞入 manifest 造成递归 hash 或重打包。
- ContentServer 从现在开始保留不可变发布审计事实，使未来可以对历史 Published PackageHash 补发服务端证明；签名实现、密钥轮换、吊销和离线信任链留给独立方案，不在本阶段预留半成品验证分支。

Mod 的 `Identifier` 就是 ModId，Mod metadata 不再定义第二个可能不一致的 ID。其他类型首次创建时由游戏内容包制造器、ContentWebUI
或 ContentTool 生成 UUID 字符串并写入 manifest；更新版本必须继承 Identifier。ContentServer 首次接收某个 Identifier 时生成
ContentId 并绑定发布者，后续提交必须命中同一发布者拥有的记录。服务端身份分配不重写已接受的包。若同一 PackageHash 已有物理制品，则保留首次接受的文件作为代表，后续逻辑相同但 ZIP 物理字节不同的提交幂等引用该制品。

公共字段由统一解析器完成结构和安全校验；类型处理器只解释 `payload/` 与对应 metadata：

| 类型 | 统一包内的 payload | 安装后的性质 |
|---|---|---|
| Mod | manifest、assemblies、data、assets 等模组运行内容 | 不可变包进入 ContentPackageCache，由 ModProfile 决定启用 |
| World | 世界快照文件集合 | 解包后创建与来源包无关的新世界实例，不保留 ContentId、版本或 PackageHash 关联 |
| BlocksTexture | 规范支持的图像文件 | 安装为材质资源，由材质选择逻辑使用 |
| CharacterSkin | 规范支持的图像文件 | 安装为皮肤资源，由角色外观逻辑使用 |
| FurniturePack | 家具设计数据 | 安装为家具包，由家具逻辑使用 |

统一包采用“单层容器、展开 payload”规则：

- 不把 `.scpak`、`.scworld` 或 `.scfpack` 原 ZIP 整体作为一个文件再次放进统一 ZIP。
- 现有 ZIP 格式迁移时解包为 `payload/` 下的逻辑条目；图像等单文件内容直接作为一个 payload 条目。
- 安装时类型处理器直接从统一包条目流写入新的游戏资产存储 API；同步重构仍要求旧归档流的 Manager 接口，不在安装边界重建 `.scworld`、`.scfpack` 或其他旧格式。
- 显式制造时直接从创作输入或游戏资产生成统一包条目，不先生成旧包再套一层统一包；制造结果先位于临时区并通过 FilePicker 保存。普通缓存包导出不进入制造流程，只复制缓存原包。
- 已压缩或压缩收益低的 payload（PNG、JPEG、已有压缩数据等）使用 ZIP Store；文本、XML 和普通数据按条目使用 Deflate。压缩策略不参与 PackageHash。

复杂度按四个边界隔离，不实现一个包含所有类型分支的巨型处理器：

- `ContentPackageReader/Writer`：唯一负责 ZIP、manifest、路径规则、大小限制、流生命周期和 PackageHash。
- `IContentPayloadCodec`：按 `ContentType` 注册，负责类型 metadata、payload 布局、校验以及创作输入到 payload 的转换，不依赖游戏内部存储。
- `IContentInstaller`：位于游戏内容层，只负责把缓存中已验证的 payload 安装到类型专属内部位置；不负责包导出或维持来源关系。
- `IContentPackageCache`：负责所有类型 `.scpkg` 的流式写入、原子提交、索引、打开、删除和原包导出，是本地包身份的唯一事实来源。
- `IContentCreationSource`：位于游戏内容层，只把选定游戏资产或 FilePicker 素材投影为 codec 所需的只读制造输入；不向 `Content.Packaging` 暴露具体 Manager、Screen、Storage 路径或运行对象。

ContentServer、FilePicker 导入和下载先调用共享 reader，再根据 manifest 的 `type` 选择 codec；只有游戏端继续调用 installer。
新增内容类型需要新增 codec、schema 和游戏 installer，不允许复制一套顶层上传、下载或 hash 流程。

### 玩家与创作者制作流程

统一内容包是机器交换格式，不要求玩家手工创建 ZIP、编写 manifest 或计算 PackageHash。不同类型可以保留不同的制作入口，
但所有入口最终调用同一个 `ContentPackageWriter`：

| 类型 | 推荐制作入口 | 创作者需要提供 |
|---|---|---|
| Mod | 模组项目 MSBuild target / 内容打包 CLI | 稳定 ModId、名称、版本和模组 manifest 信息 |
| World | 游戏内内容包制造器 | 选择本地世界、显示名称和版本；可创建新 Identifier 或基于已有 World 包制造新版本 |
| BlocksTexture | 游戏内内容包制造器 / ContentWebUI / CLI | 通过平台或浏览器 FilePicker 选择图像，并提供名称和版本 |
| CharacterSkin | 游戏内内容包制造器 / ContentWebUI / CLI | 通过平台或浏览器 FilePicker 选择图像，并提供名称和版本 |
| FurniturePack | 游戏内内容包制造器 | 选择游戏内家具包、显示名称和版本 |

创作者工具至少提供以下能力：

- `pack`：从类型专属输入生成统一内容包。
- `inspect`：显示公共 manifest、类型 metadata、payload 清单和 PackageHash。
- `verify`：执行与 ContentServer 相同的结构、安全和类型校验。
- 游戏内内容包制造器：统一填写类型、名称、版本和 metadata；World/FurniturePack 从游戏资产读取素材，BlocksTexture/CharacterSkin 通过平台注册的 FilePicker 读取原始图像。完成类型校验和预览后在临时区生成新包，再通过 FilePicker 保存；保存成功不自动导入、安装或改变缓存。它只做素材选择、预览和打包，不提供图片编辑。
- WebUI 简单内容制造与提交：作为有限的补充入口，仅为 BlocksTexture/CharacterSkin 提供浏览器文件上传、预览、元数据、生成包和提交审核；不承担 World、FurniturePack 或 Mod 打包。
- ContentWebUI 完整包提交：发布者可以选择游戏、CLI 或 MSBuild 已生成的 `.scpkg`，预览权威校验结果后提交审核，不需要重新制造。
- ContentServer 的仓库提交接口始终接收完整 `.scpkg`，不要求调用者使用服务端制造能力。第一阶段由 ContentWebUI 承担 Publisher 登录和完整包提交；游戏不实现 Publisher 凭据或审核工作台。

PackageHash 始终由 writer 生成，创作者不得手工填写。所有包使用 SemVer 2.0；创作入口可为首次创建提供 `1.0.0`
默认值，但必须允许创作者修改。Identifier 由创作入口生成或由 Mod 作者声明，更新时继承；ContentId 只由 ContentServer
在首次提交时生成。任何类型制造新内容时生成新 Identifier；制造已有内容的新版本时显式继承所选基线包的 Identifier。

创作身份与安装资产严格分离：

- “创建新内容”生成新的 Identifier。
- World、BlocksTexture、CharacterSkin 和 FurniturePack 的“创建新版本”必须由创作者显式选择一个同类型已有 `.scpkg` 作为版本基线，只继承其 Identifier，并提供新的 SemVer 和素材；读取基线包不导入、不安装，也不修改缓存。
- ContentServer 最终验证该 Identifier 是否属于当前 Publisher；选择他人的包作为基线不能绕过所有权检查。
- World 与其他内容一样允许创建新 Identifier 或继承基线包 Identifier 制造新版本；该创作选择不建立安装世界与基线包之间的关系。
- Mod 的稳定 Identifier 来自模组项目配置，新版本由构建系统沿用，不从已安装 Mod 或 Runtime 反向生成。

内容制作能力由一个共享 SDK 和三个调用入口组成：

```text
Content.Packaging（共享 SDK）
├── 模组 MSBuild target
├── ContentTool CLI
├── 游戏内容包制造器
└── ContentServer 后端（有限的简单内容制造能力）
      └── ContentWebUI 简单内容界面
```

- `Content.Packaging`：拥有统一 manifest、reader/writer、PackageHash、schema 和 payload codec，不依赖 UI、ContentServer 或游戏运行时；所有协议相关公共依赖均归集到独立的解决方案 Protocol 文件夹。
- 模组 MSBuild target：从模组项目配置和构建输出自动生成可发布内容包，不要求作者额外运行打包命令。
- `ContentTool` CLI：服务 CI、批量制作、格式检查和高级创作者，调用同一 SDK，不重新实现协议。
- 游戏内容包制造器：游戏内完整的非 Mod 制造入口；使用同一 SDK 从游戏资产或 FilePicker 素材生成临时包并保存到用户选择的位置，不隐式写入统一缓存。
- ContentServer 后端：核心仓库能力只接收完整包；有限的简单制造接口调用共享 SDK 完成皮肤/材质源文件的权威校验、manifest 生成、打包和 PackageHash，不扩展到 World、FurniturePack 或 Mod。
- ContentWebUI：面向不编写代码的玩家，提供皮肤、材质等简单内容的素材上传、预览、元数据编辑、包下载和发布入口。

ContentWebUI 使用浏览器文件输入选择创作源文件，不使用 `Engine.FileStorage.FilePicker`。浏览器预览只改善交互，ContentServer
必须重新执行所有权威校验；不能信任前端提交的尺寸、类型、ID、PackageHash 或打包结果。游戏内制造器
只提供素材选择、预览、元数据和打包能力，不提供图片编辑等 Content Studio 能力。

无代码创作的最低流程：

```text
登录 ContentWebUI 发布者工作台
  -> 选择“创建皮肤/材质”并由浏览器选择图片
  -> 前端即时预览，后端返回权威尺寸/格式校验
  -> 填写名称、SemVer 版本和可选说明
  -> ContentServer 生成并 verify 统一内容包
  -> 下载生成的包，或直接提交为 Pending 版本
```

ContentServer 第一阶段不保存发布者草稿。ContentWebUI 将简单内容草稿保存在浏览器 IndexedDB 中，包括 Identifier、类型、名称、版本、
metadata 和创作源 Blob；不使用 `localStorage` 保存大文件。草稿只存在于当前浏览器配置，不提供跨设备同步或服务端恢复，
界面必须明确提示浏览器数据被清理后草稿会丢失。

建议的后端能力边界：

```text
接收源文件和创作元数据
权威验证并返回预览元数据
生成并返回统一内容包
将同一次请求生成的结果提交审核
```

匿名用户不使用后端打包能力，避免把 ContentServer 变成无认证的通用文件转换服务。第一阶段只允许 Active Publisher 调用
验证、打包和提交能力。后端接口无草稿状态：下载包时直接返回生成结果；提交时由同一次请求的输入生成不可变包并创建
Pending 版本。具体路由在实现设计时确定，不在临时计划中固化 HTTP 路径。

所有类型共享一个 `PackageHash` 算法。算法对包的逻辑条目计算 SHA-256：

- 包含未经重写的 `manifest.json` 字节以及所有 payload 文件内容；
- 条目路径规范化并按 ordinal 顺序参与计算；
- 每个路径和内容使用固定端序长度前缀编码，避免简单连接或分隔符造成歧义；
- 路径使用 UTF-8 规范编码，拒绝重复路径、大小写歧义、绝对路径和 `..`；
- 不包含 ZIP 条目顺序、压缩方式、时间戳和其他容器元数据；
- 因此相同逻辑包的重新压缩不改变 `PackageHash`，任何 manifest 或 payload 字节变化都会改变它。

ContentServer 可以内部额外保存原始上传 ZIP 字节的 `BlobHash` 用于物理 BLOB 去重或诊断，但该字段不属于公共内容身份，
不得替代 `PackageHash`。公开查询、下载地址、ContentPackageCache 和 ModProfile 运行时校验统一使用 `PackageHash`；派生游戏资产不保存该身份。

World 安装提供两种显式模式：

- “创建新世界”为默认模式：使用 manifest 名称作为默认值并允许用户指定本地名称；重名时自动追加 ` (1)`、` (2)` 等序号，生成新的本地 AssetKey。
- “覆盖现有世界”为高级模式：必须由用户明确选择本地目标并二次确认，不能依据包 Identifier、版本、名称或历史来源自动推断目标。目标世界不得正在运行；安装器先完整写入并验证暂存世界，再以可恢复的原子替换提交，失败时保留原世界。

两种模式完成后都不保存来源 Identifier、ContentId、Version 或 PackageHash。覆盖模式保留目标世界的本地 AssetKey，名称由用户在确认界面决定；后续修改、删除和运行完全由世界逻辑管理。
从 World 制造内容包时可以创建新 Identifier，也可以显式选择同类型基线包继承 Identifier 制造新版本；新包通过 FilePicker 保存，只有用户随后显式导入时才进入 ContentPackageCache。

各类型的缓存与安装语义：

- Mod：不同版本在 ContentPackageCache 共存；下载或导入不修改 Profile，启用/停用只由 Mod 管理界面修改 Profile。
- World、BlocksTexture、CharacterSkin、FurniturePack：默认创建独立游戏资产，也允许用户明确替换所选同类型本地资产；不记录来源 Identifier、Version、PackageHash 或 ContentId，绝不依据包身份自动覆盖。
- 创建模式使用资产 Manager 生成的稳定 `AssetKey` 和可变本地显示名称；游戏配置引用 AssetKey，显示名称冲突时追加序号。替换模式保留目标 AssetKey，重复安装同一个 PackageHash 时仍由用户逐次选择创建或替换。
- 删除或修改派生资产不修改缓存包；删除缓存包也不删除已经派生的资产。
- 非 Mod 资产删除只按本地资产身份执行，不尝试查找、删除或修改产生它的缓存包；同一包多次安装得到的资产可以分别删除。

ContentServer 包文件存储结构：

```text
Data/
├── content-server.db
├── packages/<packageHash>.scpkg
└── temp/
```

上传和后端生成先写入 `temp/`，完成大小、安全、schema 和 PackageHash 校验后原子移动到 `packages/`。数据库事务只在文件已就绪后
建立引用，失败时清理临时文件。服务启动或维护工具需要能够发现并清理无数据库引用的孤儿包，但不得删除仍被任何版本引用的文件。

### 统一内容包中的模组 payload

- 从统一 manifest 读取全局唯一 `identifier`（即 ModId）、`name`、`version`，从 Mod metadata/payload 读取 `side`、依赖和入口点。
- 本地导入、ContentServer 提交、ContentServer 下载和 ContentPackageCache 索引共用统一包解析器及 Mod payload 验证器。
- Mod 的权威制品与其他类型一致，固定为 `ContentPackageCache/<PackageHash>.scpkg`；缓存文件就是经验证的导入包或 ContentServer 下载包，不生成 `.scpak`、展开目录或其他运行时副本。
- GameModRuntime 通过共享 `ContentPackageReader` 直接打开 `.scpkg`，确认 `type = Mod`、Identifier、Version、PackageHash、Side 和依赖后，由 Mod payload codec 按需读取程序集、数据和资源条目。
- 加载使用 `FileStream`、`ZipArchive` 和条目流；只允许在程序集加载等既有 API 必须使用字节数组时物化单个条目，不得把完整 `.scpkg` 读入内存。
- ContentPackageCache 可以维护可重建的统一索引以加速查询，但索引不是事实来源；索引缺失或损坏时必须能从 `.scpkg` 重建，不能据此引入第二套包身份或加载格式。
- 导出未修改的缓存 Mod 时直接将同一 `.scpkg` 流复制到 FilePicker 目标，保持 Identifier、Version、PackageHash 和包字节不变，不重新打包。
- `ModId + Version` 标识逻辑版本；同版本不同规范包 hash 必须冲突，不允许覆盖。
- 相同版本、相同规范包 hash 的重复导入或提交应幂等。
- 模组和其他内容使用统一内容包 `PackageHash`；ContentServer 原始 ZIP BLOB hash 如需保留只能作为内部 `BlobHash`。

### ContentServer

- 新 Identifier 首次提交时由服务端生成全局唯一 ContentId 并绑定发布者；后续版本按 Identifier 命中并验证归属。
- 发布者提交模组时只上传包和可选仓库展示信息，ModId（即 Identifier）、名称与版本由 manifest 决定。
- 新提交进入 `Pending`，管理员审核后成为 `Published`；匿名接口只暴露允许公开访问的版本。
- 保留按模组列举、按 `ModId + Version` 精确解析以及按 hash 下载的公开能力。
- 已发布版本不可覆盖；修复内容必须使用新版本。

### 游戏 UI 职责

- Mod 管理 Screen 只展示 ContentPackageCache 中可用的 Mod、Profile 启用/停用以及 Mod 包 FilePicker 导入和原包导出，不承担其他内容类型管理。
- 内容包管理 Screen 负责非 Mod 目录浏览、包缓存、ContentServer 下载、从缓存发起安装以及对应 FilePicker 包导入和原包导出；安装完成后不展示派生资产、不记录“已安装/可卸载”状态。
- World 下载后的安装由用户选择创建新世界或覆盖明确选定的本地世界；完成后不在内容包管理 Screen 保留任何安装关系，本地世界统一由世界管理界面处理。
- 皮肤、材质和家具安装完成后同样退出内容包流程；查看、选择、重命名、引用检查和删除由现有对应游戏资产管理 Screen 负责。
- 在线内容目录可以展示 Mod 并把包下载到 ContentPackageCache，但后续启用/停用必须进入 Mod 管理 Screen。
- 两个 Screen 复用统一包 reader、下载客户端、安装结果和错误模型，不复制协议与存储逻辑。

### ModProfile 与启动

- GUI 和 Headless 调用同一个解析/准备组件。
- 解析顺序固定为 ContentPackageCache 的 Mod 视图优先、ContentServer 补缺、回到本地包启动 Runtime。
- 两端只允许 `ModSide` 和错误呈现方式不同，不允许维护两套缓存或下载规则。
- 删除自动目录导入后，正常启动不新增替代扫描、同步或首次启动分支。

## 分阶段实施

阶段按依赖关系组织，遵守以下规则：

- 每个交付项只归属一个阶段；早期阶段可以定义后续契约，但不提前要求后续 UI、平台或运行时实现完成。
- 阶段门禁只能依赖当前阶段及更早阶段的产物，不能引用尚未实施的后续阶段。
- 公共协议先于共享 SDK，共享 SDK 先于服务端和游戏基础设施，基础设施先于平台 UI，最后统一删除旧实现。
- 跨层端到端验证归入最后一个必需组件所在的阶段；早期阶段只执行当时可闭合的组件级或纵向验证。

### 阶段 0：固化统一内容包协议

- [x] 将 `.scpkg` 定为唯一公共扩展名，并确认媒体类型、ZIP 目录布局和 `formatVersion` 规则。
- [x] 将单层容器、展开 payload 和禁止公共嵌套归档写入格式规范；逐类根据当前游戏数据语义定义全新的规范 payload，不建立旧扩展名输入映射。
- [x] 定义公共 manifest schema、公共字段约束以及各类型 metadata schema。
- [x] 明确发布者、包内 manifest 和服务端分别拥有哪些元数据的写入权与审核权。
- [x] 将首阶段“无签名”写入协议和 UI 信任词汇：ContentServer 来源只表示仓库审核，本地 FilePicker 来源未验证，可执行 Mod 必须警告；不得出现“发布者签名已验证”状态。
- [x] 为未来独立 attestation 方案记录兼容原则：证明按 PackageHash 关联并与 `.scpkg` 分离，不进入 manifest、PackageHash 或包缓存事实来源；本阶段不实现解析、验证、密钥或吊销代码。
- [x] 定义统一 `PackageHash` 的逐字节算法、路径规范化、安全限制和公开 API 含义。
- [x] 明确 PackageHash 与可选内部 BlobHash；BlobHash 如保留不得进入客户端内容身份。
- [x] 设计五种内容的规范 payload 以及安装后的游戏资产布局；旧格式只用于理解当前数据语义，不作为新 reader、writer 或内部安装格式保留。
- [x] 固化 World 解包即脱离来源、创建新世界/覆盖指定世界、默认/自定义名称、重名追加序号、原子替换和制造新内容/新版本的规则。
- [x] 固化 Identifier 全局唯一、ModId 等于 Identifier、其他类型使用 UUID Identifier、ContentId 仅由服务端生成及 SemVer 2.0 规则。
- [x] 新协议不定义 `.scpak`、`.scworld`、`.scbtex`、`.scskin`、`.scfpack` 输入，不设计兼容解析或双格式入口；实际代码删除在替代 reader、writer、installer 全部就绪后的阶段 6 执行。
- [x] 定义模组构建、打包 CLI、游戏内非 Mod 制造和 WebUI 简单内容制造/提交的输入输出与职责边界；普通玩家路径不得要求手写 manifest 或操作 ZIP。各入口的实现和端到端验证归入对应后续阶段。
- [x] 定义浏览器 IndexedDB 草稿 schema、容量失败提示、单条/全部清理和数据清理警告；ContentServer 不保存草稿，第一阶段不定义草稿交换格式。
- [x] 定义皮肤和材质无代码上传、预览、权威校验、浏览器草稿、打包和提交审核的交互与 API 契约；不设计图片编辑能力，实际实现归入阶段 3。
- [x] 将最终协议写入正式文档，再允许修改 ContentServer 数据模型和共享安装接口。

门禁：阶段 0 未完成前，不修改 ContentServer 初始数据库结构，不实现五套临时上传协议，也不让新内容管理层直接接收旧格式作为最终公共契约。

### 阶段 1：建立统一包 SDK 与测试向量

- [x] 实现不依赖游戏运行时的统一 manifest 解析、ZIP 安全校验和 PackageHash 组件。
- [x] 在共享 SDK 中实现五种 payload codec 和验证器，不依赖游戏运行时或 ContentServer。
- [x] 为皮肤和材质选择可同时用于 Content.Packaging、ContentServer 与游戏端的确定性图像读取/校验实现，避免共享 SDK 反向依赖完整 Engine、Graphics 或游戏运行时。
- [x] 建立 `Content.Packaging.Test`，固化五类黄金包、恶意包和跨 writer 一致性测试。
- [x] 提供最小 `pack`、`inspect`、`verify` ContentTool，用于开发、CI 和诊断。
- [x] 修改模组 MSBuild target 和模板，使构建直接输出通过 verify 的 `.scpkg`。
- [x] 修改游戏 Mod 包解析器和 GameModRuntime，使其从 `.scpkg` 条目流直接校验并加载 Mod payload，不保留 `.scpak` parser 或完整包 `byte[]` 路径。

门禁：五类黄金包必须固定 SDK 的逻辑 PackageHash；Mod 黄金包必须进一步证明 SDK、MSBuild 构建和游戏解析得到相同的 PackageHash。阶段 1 不要求 ContentServer 或非 Mod 游戏安装器已经接入。

### 阶段 2：重构 ContentServer 存储与统一发布

- [x] 让 ContentServer 引用 `Content.Packaging`，所有上传和下载校验复用共享 Reader、五种 payload codec 与 PackageHash；删除服务端重复的包、manifest 和 hash 解析代码。
- [x] 将 PackageBlob 的 SQLite `byte[]` 存储改为 `Data/packages/<PackageHash>.scpkg` 内容寻址文件。
- [x] 实现 `Data/temp` 流式写入、校验后原子移动、失败清理和孤儿包审计/清理。
- [x] ContentServer 所有提交改为统一内容包 manifest 驱动，新 Identifier 由服务端生成 ContentId 并绑定发布者，更新验证 Identifier 归属。
- [x] 将 PublisherId、ContentType、Identifier、Version、PackageHash、可选 BlobHash、提交时间以及完整审核状态变更记录作为不可变审计事实保存；不得因改名、下架或账号状态变化重写历史归属，以支持未来按 PackageHash 补发证明。
- [x] 数据库对规范化 Identifier 建立全局唯一约束；上传只建立服务端关系，不重写包，因此上传前后 PackageHash 完全一致。
- [x] 对所有 ContentType 实现 Identifier 类型稳定、SemVer 版本唯一、同版本同包幂等、同版本不同包冲突，不提供 replace；规则与本地 ContentPackageCache 完全一致。
- [x] 明确 PackageHash 对应单一物理代表制品：首次接受的 ZIP 文件保留，后续逻辑 hash 相同但容器字节不同的提交幂等引用已有文件，API 不承诺返回后一次提交的物理字节。
- [x] 更新当前初始迁移和数据库快照，SQLite 不再包含包或草稿 BLOB。
- [x] 实现流式匿名下载和管理员审核下载，保持 Pending/Published/Rejected 与内容上下架门禁。
- [x] 扩展 API 集成测试覆盖文件提交、事务失败、临时文件清理、孤儿审计和并发同包上传。

门禁：Mod 纵向链路必须完成提交、审核、精确查询、流式下载、PackageHash 校验和游戏解析；大包路径不得完整物化为 `byte[]`。

### 阶段 3：实现有限的 WebUI 简单内容制造

- [x] 保持 ContentServer 核心提交 API 只接收完整 `.scpkg`；为皮肤和材质额外实现边界隔离的无状态源文件验证、打包下载和直接提交接口，不支持 World、FurniturePack 或 Mod 制造。
- [x] ContentWebUI 提供完整 `.scpkg` 的选择、权威校验预览和提交审核入口，覆盖游戏、CLI 与 MSBuild 产生的包；不要求重新打包。
- [x] ContentWebUI 使用 IndexedDB 保存浏览器草稿，提供容量失败、清理风险以及单条/全部草稿清理。
- [x] UI 只提供图片上传、预览、元数据、权威校验结果、打包和提交，不实现图片编辑。
- [x] 增加 WebUI 组件、IndexedDB 与 API 集成测试并验证生产构建；第一阶段不要求严格的 ContentServer 浏览器冒烟，浏览器自动化细节可后续调整。
- [x] 验证未认证或非 Active Publisher 不能调用后端打包与提交能力。

门禁：IndexedDB 测试覆盖草稿持久化与清理；服务器没有草稿记录或源 BLOB；同一次提交生成的包与 Pending 版本 PackageHash 一致。

### 阶段 4：建立统一包缓存、游戏安装和导出内核

- [x] 建立 `IContentPackageCache`，将所有类型的唯一缓存布局定为 `ContentPackageCache/<PackageHash>.scpkg`，提供流式写入、原子提交、统一索引、打开、删除和重建索引。
- [x] 实现缓存消费校验与损坏隔离：安装、Runtime 首次加载和导出以实际文件重算 PackageHash；索引缺失、过期或伪造不能绕过验证，远程包可以显式重新下载修复。
- [x] 实现缓存生命周期规则：第一阶段不自动驱逐；显式删除非 Mod 包不级联删除资产；拒绝删除被 Profile 或当前 Runtime 引用的 Mod 包。
- [x] 将 `LocalModRepository` 收敛为统一缓存的 Mod 查询适配边界或直接删除，由 ModProfile 和 Runtime 查询 `type = Mod` 的缓存包。
- [x] 让所有类型的导入和下载在临时文件完成统一校验后原子移动到 ContentPackageCache，不解包、不转换格式、不生成派生内容包。
- [x] 让所有包导出直接复制缓存中的原 `.scpkg` 到 FilePicker 输出流，不重新封装或改变 PackageHash。
- [x] 建立来源无关的内容安装入口和各内容类型处理器。
- [x] 让 ContentServer 下载与本地导入调用相同安装入口。
- [x] 明确缓存包目录与已安装游戏资产目录是两个生命周期；内容系统不建立非 Mod 安装记录，现有资产 Manager 生成稳定 AssetKey 并只持久化自身身份、名称和游戏所需数据，游戏配置不再用可变显示名称作引用键。
- [x] 为内置皮肤、默认材质等定义稳定的只读保留 AssetKey；资产选择逻辑继续支持它们，但安装替换和删除入口必须排除内置资产。
- [x] 实现独立删除语义：缓存移除不删除资产，资产删除不删除缓存；同一包多次安装的资产能够分别删除。
- [x] 在现有游戏资产管理 Screen 中为材质和皮肤实现引用检查与替换后删除，禁止删除后留下 World、玩家配置或当前会话的悬空引用。
- [x] 实现 World 创建新世界与覆盖指定世界两种安装模式；覆盖要求显式选目标、二次确认、运行状态检查、暂存校验和可恢复的原子替换，完成后仍脱离来源。
- [x] 实现材质、皮肤、家具包默认创建独立本地资产，也可显式替换所选同类型资产；替换保留 AssetKey、显示引用影响、二次确认并使用暂存校验与原子提交，不按包身份自动匹配。
- [x] 建立游戏内容包制造内核：World/FurniturePack 从游戏资产取材，BlocksTexture/CharacterSkin 接受 FilePicker 素材流；创建临时 `.scpkg`，校验后交给 FilePicker 保存，不写入缓存或安装。
- [x] 为 World、皮肤、材质和家具制造器提供显式“创建新内容/创建新版本”模式；新版本从用户选择的同类型基线 `.scpkg` 只继承 Identifier，重新填写 SemVer 并使用当前素材制造。Mod 继续由项目构建流程维护版本身份。
- [x] 验证 Mod 版本共存且导入/下载不修改任何 Profile。
- [x] 将 ContentServer 客户端下载和安装改为流式，不使用 `GetByteArrayAsync`。
- [x] 使用可控的非平台测试流对大型 World 执行制造、缓存导入和安装压力测试，验证峰值内存、包大小限制、取消和失败清理；不得为计算 hash、制造或安装而把完整包读入 `byte[]`。真实 FilePicker 保存与取消验证归入阶段 5。

门禁：等价的本地输入测试流与 ContentServer 下载流对同一包产生相同安装结果；五类安装失败均不留下半安装资产，缓存已提交的有效包仍可重试安装；大型 World 压力测试满足内存和取消指标。本阶段不依赖任何平台 FilePicker 实现。

### 阶段 5：实现 FilePicker 并迁移游戏 UI

- [x] 为 Windows 实现并在 GUI Starter 初始化阶段注册 `IFilePicker`。
- [x] 为 Linux 实现并注册 `IFilePicker`，明确桌面 portal 不可用时的行为。
- [x] 为 Android 实现并注册 `IFilePicker`，使用系统文档选择/创建能力并管理 URI 流生命周期。
- [x] 验证打开单文件、多文件、保存目标、取消、异常和重复调用；Headless 保持不注册。
- [x] Mod 管理 Screen 通过 FilePicker 多选导入统一 Mod 内容包到 ContentPackageCache，并只负责 Profile 启用/停用和 Mod 包管理。
- [x] 模组导出通过 FilePicker 原样复制缓存包，不重新生成内容包，也不再写入 `GamePaths.Mods`。
- [x] 内容包管理 Screen 承担其他类型的缓存包导入/导出和从缓存发起安装，不展示或删除安装后的本地资产，并与 Mod Screen 复用底层包服务。
- [x] 内容包管理 Screen 提供非 Mod 内容包制造入口：选择类型和素材、填写名称与版本、预览和校验，通过 FilePicker 保存；另行提供显式导入和缓存包导出。第一阶段不在游戏中实现 Publisher 登录或提交审核。
- [x] FilePicker 不可用时隐藏或禁用交互式导入导出，并提供明确说明。
- [x] 包导入完成后只刷新缓存包列表；安装完成后只刷新对应游戏资产 Screen 的数据。启用、选择或进入内容必须是独立的显式操作。
- [x] 删除对外部源路径的持久化和展示依赖。
- [x] 更新 `Doc/Architecture.md`、`Doc/FileStorage.md` 和 `Doc/StartupSessions.md` 中的平台支持现状。

门禁：至少在 Windows、Linux、Android 各完成一次真实打开与保存 smoke test；Headless 构建和启动不得依赖 picker。

### 阶段 6：收口启动、远程仓库并删除旧实现

- [x] 从 GUI `LoadingScreen` 删除 `ImportInstalledMods`。
- [x] 从 Headless 初始化删除相同调用。
- [x] 删除 `LocalModsImportManager`、`LocalModsImportState.xml` 和相关 `GamePaths` 字段。
- [x] 删除自动扫描 `GamePaths.Mods` 的行为、导出后登记源文件的行为以及相应 UI 文案。
- [x] GUI 和 Headless 继续共用 ContentPackageCache 优先、远程补缺的解析器。
- [x] 游戏端远程仓库客户端和类型统一改用 ContentServer 命名。
- [x] Profile、联机服务器信息和界面中的仓库地址统一为 ContentServer 语义。
- [x] 删除 `ModServer/` 项目、部署脚本、解决方案项和测试。
- [x] 删除 `ModServerClient`、旧响应类型、旧配置名和旧语言文本，不保留适配器。
- [x] 删除 `.scpak`、`.scworld`、`.scbtex`、`.scskin`、`.scfpack` 的 reader、writer、扩展名分派、测试夹具和旧资产存储路径；不保留 fallback 或内部临时重建。
- [x] 删除 `Doc/ModServer.md`，将仍有效的仓库语义写入 `Doc/ContentServer.md` 和 `Doc/Mods.md`。
- [x] 更新 README、Headless、启动、文件存储和内容管理相关文档。
- [x] 全仓搜索 `ModServer`、`ModRepositoryUrl`、`GamePaths.Mods`、`LocalModsImportState` 等陈旧术语并逐项处理。

门禁：空缓存、有缓存、远程补缺、无远程缺包四种情形在 GUI 和 Headless 中结果一致；仓库中不存在旧扩展名 parser、ModServer 或自动导入路径。

## 必须覆盖的测试与验证

### FilePicker

- 打开单文件和多文件。
- 保存新文件和覆盖目标。
- 用户取消不产生错误或空文件。
- 输入、输出流在成功和异常路径上正确释放。
- Android URI 不被转换或持久化为系统路径。
- Headless 未注册时业务逻辑不会调用 picker。

### 内容安装与导出

- FilePicker 导入和 ContentServer 下载同一包得到相同内部结果。
- 缓存提交成功但安装失败时不留下半安装资产，已验证 `.scpkg` 仍保留在 ContentPackageCache 并可重试安装。
- 同一 `.scpkg` 在本地、上传后服务端存储、下载后均保持相同 Identifier、Version 和 PackageHash。
- 所有类型在本地缓存和 ContentServer 都拒绝同一 Identifier + Version 的不同 PackageHash，也拒绝同一 Identifier 改变 ContentType；同版本同 hash 重复操作保持幂等。
- 所有类型经 FilePicker 导入或 ContentServer 下载后，原始 `.scpkg` 都存在于统一 ContentPackageCache；相同 PackageHash 幂等命中同一缓存文件。
- 模组导入后存在于 ContentPackageCache，但所有 Profile 和当前 Runtime 均不改变。
- ContentPackageCache 中只存在以 PackageHash 寻址的 `.scpkg` 权威包，不存在旧格式、展开 payload 或派生内容包。
- 缓存文件被截断、篡改或与文件名 PackageHash 不符时，安装、ModRuntime 和导出均拒绝使用并隔离该项；删除索引后扫描也不会把损坏文件登记为有效包。
- 任意已经进入本地缓存的物理 `.scpkg` 经缓存命中和 FilePicker 导出后字节及 PackageHash 保持不变。若导入的另一 ZIP 具有相同逻辑 PackageHash 但物理字节不同，则幂等保留缓存中首次接受的代表制品。
- World 默认创建新世界，允许指定本地名称且重名自动追加序号；也允许用户二次确认后覆盖明确选定且未运行的本地世界。两种模式安装后都不保存来源 Identifier、ContentId、Version 或 PackageHash。
- 同一 World 包重复安装时可以创建多个互不关联的世界实例，也可以覆盖用户每次明确选择的目标；不得依据包身份或名称自动选择覆盖目标。
- World 覆盖安装失败或取消时原世界保持完整；成功时保留目标 AssetKey，并且暂存世界已经完成结构校验。
- 材质、皮肤和家具包默认创建不含包来源身份的独立本地资产并获得新 AssetKey，也允许用户明确替换所选同类型资产并保留目标 AssetKey。
- 同名非 Mod 资产在创建模式下追加本地名称序号；替换模式必须显式选择目标并确认影响，不依据 Identifier、PackageHash 或名称自动匹配。
- 非 Mod 资产替换失败或取消时原资产保持完整；目标正在被运行时使用且无法安全热替换时不会立即提交。
- 内置皮肤和默认材质等只读资产不能被替换或删除；用户资产不能占用保留 AssetKey。
- 重命名本地资产不改变 AssetKey，World、玩家配置和当前选择等持久引用仍指向同一资产。
- 模组、材质、皮肤和家具包安装后不自动启用或选择。
- 包导出只复制 ContentPackageCache 中已经存在的 `.scpkg`，不读取或推断已安装游戏资产，也不修改安装和使用状态。
- 删除或修改非 Mod 游戏资产不改变缓存包；删除缓存包不删除已派生资产。
- 同一非 Mod 包重复安装产生的多个本地资产可以单独删除，删除其中一个不会影响其他资产或缓存包。
- 材质或皮肤仍被世界、玩家配置或当前会话引用时不能直接删除；选择替代资源后引用更新与资产删除必须作为一个不会留下悬空引用的操作完成。
- 非 Mod 安装完成后内容包管理 Screen 不出现“已安装”“升级”或“卸载”状态；新资产只出现在对应游戏资产管理 Screen，后续操作不查询来源包。
- 删除被 ModProfile 或当前 Runtime 引用的 Mod 缓存包会被拒绝；未引用缓存包可以显式删除，且第一阶段不存在自动驱逐。
- 两个物理 ZIP 的逻辑 PackageHash 相同时只保留一个缓存制品，索引不产生重复项，导出内容等于缓存中实际保留的文件。
- 单个文件失败不回滚同一批次中已经成功的其他独立文件，并能汇总错误。
- 游戏内制造并保存的新包可被 `inspect`/`verify`、游戏导入流程和 ContentServer 直接接受，不需要重新封装。
- 从 World 或 FurniturePack 等游戏资产制造新内容时生成新 Identifier；制造新版本时只继承用户所选同类型基线包的 Identifier。两者都生成新 PackageHash，保存后不自动进入缓存，只有用户显式导入才会安装。

### 内容创作

- 模组模板构建后直接产出通过统一 `verify` 的内容包。
- CLI、MSBuild 和 ContentServer 后端对相同输入生成相同 manifest 语义和 PackageHash。
- 玩家不编写代码、不手写 JSON、不操作 ZIP 即可完成皮肤和材质内容包制作。
- 游戏内可以完成 World、FurniturePack、BlocksTexture 和 CharacterSkin 内容包制造；皮肤和材质素材通过平台注册的 FilePicker 读取，游戏不提供图片编辑。
- 游戏制造成功的包由 FilePicker 保存到用户目标，不自动进入 ContentPackageCache。后续本地使用走显式导入；分享则在 ContentWebUI 选择已经保存的 `.scpkg` 提交，不上传原始素材或重新打包。
- World、皮肤、材质或家具的“创建新版本”只从所选同类型基线 `.scpkg` 继承 Identifier；不会导入基线包、安装资产或修改缓存，新版本 PackageHash 必须由新 manifest 与 payload 重新计算。
- ContentServer 拒绝当前 Publisher 不拥有的继承 Identifier；制造器拒绝类型不一致的版本基线。
- 制造界面取消、FilePicker 取消或保存失败时清理临时包，不产生缓存项、安装资产或 ContentServer 记录。
- 制造并保存后，ContentPackageCache 和游戏资产列表保持不变；只有再次显式导入该文件后才新增缓存项，并按用户选择执行安装。
- 无效图片在导出前给出具体校验错误，不生成半成品包。
- 浏览器草稿只保存在 IndexedDB，ContentServer 数据库和文件目录中不存在草稿记录或源 BLOB。
- IndexedDB 草稿重新打包时使用用户确认的版本并产生新的 PackageHash；浏览器数据清理会丢失草稿且 UI 有明确提示。
- 未认证或非 Active Publisher 不能调用后端设计、打包和提交能力。
- ContentWebUI 可以直接提交游戏、CLI 或 MSBuild 生成且 verify 通过的完整 `.scpkg`；ContentServer 不重写新接受的制品。若 PackageHash 已存在但 ZIP 物理字节不同，下载返回服务器首次接受的代表制品，其逻辑条目、Identifier、Version 和 PackageHash 与提交包相同。
- 当前版本不存在“签名已验证”状态：ContentServer 包只显示仓库与审核来源，FilePicker 包显示本地未验证来源，Mod 本地导入显示可执行代码警告。
- 只有显式提交请求才产生 Pending ContentVersion；仅验证或下载打包结果不会产生服务端内容记录。

### 模组解析与启动

- GUI/Headless 均优先命中 ContentPackageCache 的 Mod 查询视图。
- GUI/Headless 均由 GameModRuntime 直接从缓存 `.scpkg` 的条目流加载，除必要的单个程序集条目外不物化完整包。
- ContentPackageCache 索引删除或损坏后可仅从所有 `.scpkg` 重建；Mod 条目得到相同的 ModId、Version、PackageHash、Side 和依赖信息。
- 缓存缺失且配置 ContentServer 时均下载并缓存。
- 缓存缺失且无 ContentServer 时均报告精确缺失列表。
- 同 `ModId + Version` 不同 hash 在导入和发布时均冲突。
- Profile 是唯一启用来源；缓存中存在额外包不改变运行时加载集合。
- Client/Server Side 校验使用共享规则，仅宿主 Side 参数不同。

### ContentServer

- 新 Identifier 对应的 ContentId 只能由服务端生成；更新版本必须验证 Identifier 已绑定当前 Publisher。
- 表单不能伪造 manifest 中的 Identifier、名称、版本或 Mod Side。
- 每种内容下载后必须通过统一包解析器重新计算并匹配响应中的 `PackageHash`。
- 同一 PackageHash 的不同物理 ZIP 提交不会创建第二个文件或改写首次代表制品；下载内容逻辑 hash 正确，API 与诊断信息能区分 PackageHash 和可选 BlobHash。
- 包文件不进入 SQLite；临时文件、原子提交、孤儿文件审计和仍被引用包保护均有测试。
- Pending、Published、Rejected 和内容上下架状态正确限制匿名查询与下载。
- 发布审计记录能从任意历史 Published PackageHash 追溯提交 Publisher、Identifier、Version、ContentType 和审核状态变化，且下架不会抹除历史事实。
- Mod 精确版本查询返回统一 `PackageHash`，下载完整性、ContentPackageCache 和运行时校验使用同一身份。
- 已发布版本不可覆盖或删除后以相同版本替换。

## 禁止的临时捷径

- 不保留 ModServer 路由或客户端作为兼容入口。
- 不为不同 ContentType 暴露不同顶层上传格式、下载格式或 hash 算法。
- 不让旧扩展名成为绕过统一 manifest 和 PackageHash 校验的第二套公共协议。
- 不把 `.scpkg` 转换回旧包、解包目录或其他派生格式作为包缓存，也不为 Mod 建立独立于 ContentPackageCache 的第二套权威制品。
- 不把内部 `BlobHash` 暴露或解释为公共 `PackageHash`。
- 不让 ContentServer 信任表单重复提交的模组 ID、版本或其他 manifest 权威字段。
- 不因 Headless 没有 FilePicker 而恢复启动目录扫描或增加专用加载流程。
- 不让 FilePicker、UI 或 ContentServer 客户端直接决定内部安装路径。
- 不把“从游戏资产制造并保存新包”伪装成缓存包导出；制造结果不自动缓存或安装，缓存包导出永远只复制已有缓存制品。
- 不把缓存包身份写入 World、皮肤、材质或家具等派生资产，也不依据包 Identifier 自动覆盖这些资产。
- 不把导入、下载或缓存命中解释为启用。
- 不让 FilePicker 与 ContentServer 使用不同的包身份规则；两者统一依赖 Identifier、Version 和 PackageHash。非 Mod 安装后不再引入“本地包升级”规则。
- 不把平台路径、Android URI 或导入源路径写入持久化内容记录。
- 不在迁移完成后保留废弃类型、fallback parser、双读写或版本探测。

## 最终验收与计划删除条件

本文档只有在以下条件全部满足并经人工确认后才能删除：

1. 统一内容包格式、公共/类型元数据、PackageHash 算法以及五类 payload 已形成正式协议和测试向量。
2. 三个 GUI 平台的 FilePicker 已实现、注册并完成真实 smoke test。
3. GUI 和 Headless 的 ModProfile 解析共享统一 ContentPackageCache，并通过缓存优先/远程补缺矩阵测试。
4. 自动 Mods 目录导入及其状态文件已彻底删除。
5. 本地导入与 ContentServer 下载复用同一包缓存入口和内容安装内核，ModCache 已收敛到统一 ContentPackageCache。
6. 所有支持的内容类型遵守“制造并保存、导入/下载、包缓存、派生安装、资产使用、缓存原包导出相互分离”的规则。
7. ContentServer 完整接管统一内容提交、审核、精确查询和匿名下载，包文件位于本地内容寻址目录而非 SQLite。
8. 游戏提供完整的非 Mod 内容包制造入口但不承载 Publisher 登录；ContentWebUI 提供完整包提交和有限的皮肤/材质制造，草稿仅存浏览器 IndexedDB，服务端不保存草稿。
9. Mod 与其他内容管理 Screen 职责分离且复用底层服务；World、Mod 和其他内容的安装语义符合本计划。
10. ModServer 项目、客户端、配置、部署和文档已删除，旧内容扩展名和兼容 parser 已彻底移除。
11. 正式架构、模组、ContentServer、Headless、启动和文件存储文档已更新为最终行为。
12. 全仓静态检查、相关构建、测试和必要的跨平台运行验证全部通过。
13. README 中删除本计划入口，然后删除本文档。
