# SCNET 统一内容包协议（`.scpkg` v1）

> 状态：**已批准；实现进行中。** 本文将
> [内容管理迁移临时实施计划](../ContentManagementMigrationPlan.md) 中的阶段 0 约束细化为
> `Content.Packaging`、ContentServer 和游戏端的共同契约。Mod 构建与 Runtime 已迁移到 `.scpkg`；尚未替代的
> `.scpkg` 安装器直接读取展开后的 payload。旧交换格式不属于本协议，其剩余入口将在阶段 6 清理。

## 1. 范围和术语

`.scpkg` 是唯一的外部内容交换格式。它是 ZIP 文件，文件名扩展名必须为小写 `.scpkg`，推荐媒体类型为
`application/vnd.scnet.content-package+zip`。ZIP 只是运输容器；逻辑内容身份由下文的 `PackageHash` 定义，
不取决于 ZIP 的压缩方法、条目顺序或时间戳。

包身份、仓库记录和安装后的游戏资产是不同概念：

| 名称 | 含义 | 生命周期 |
|---|---|---|
| `Identifier` | 跨版本稳定的逻辑内容标识 | 包、缓存和 ContentServer |
| `Version` | 该 Identifier 的不可变发布版本 | 包、缓存和 ContentServer |
| `PackageHash` | 该逻辑包制品的 SHA-256 | 包、缓存和 ContentServer |
| `ContentId` | ContentServer 内部记录 ID | 仅服务端 |
| `AssetKey` | 安装后本地资产的稳定 ID | 仅游戏资产管理器 |

`ContentId` 与 `AssetKey` 均不得写入包。World、BlocksTexture、CharacterSkin 和 FurniturePack 安装完成后不得保存
`Identifier`、`Version`、`PackageHash` 或 `ContentId`，也不得以它们推断覆盖目标。

首版不定义发布者签名或离线信任链。ContentServer 下载只能表达“来自该仓库且经过其审核”；FilePicker 导入只能表达“本地未验证来源”。本地导入 Mod 必须显示其包含可执行代码的警告。

未来 attestation/receipt 必须是与 `.scpkg` 分离、以 `PackageHash` 为目标的独立制品；它不进入 manifest，不改变
PackageHash，也不成为包缓存的事实来源。v1 不预留或实现证明解析、签名验证、密钥轮换、吊销或离线信任状态。

## 2. ZIP 容器和通用安全规则

ZIP 内只能有下列文件：

```text
manifest.json
payload/**
```

不得有根目录以外的文件、空目录、符号链接、加密条目、ZIP 注释承载的数据或嵌套归档。`payload/` 本身可以不作为独立 ZIP 目录条目出现。

读取器在解释 manifest 或 payload 前必须验证以下限制；Writer 也必须产生满足这些限制的包：

| 限制 | v1 值 |
|---|---:|
| manifest.json 解压后大小 | 64 KiB |
| 文件条目数（含 manifest） | 10,000 |
| 单一 payload 条目解压后大小 | 128 MiB |
| 所有条目解压后总大小 | 200 MiB |
| ZIP 压缩数据与解压数据的最大比率 | 200:1 |
| 路径 UTF-8 字节长度 | 1–240 |

每个文件路径必须使用 `/`、UTF-8 NFC、相对路径，并满足：没有空段、`.`、`..`、反斜杠、NUL、控制字符、绝对路径或驱动器前缀。路径比较使用 ordinal；同时拒绝 ordinal-ignore-case 相同的两个路径。`manifest.json` 只能出现一次且必须使用精确的小写名称；所有其他文件必须以 `payload/` 开头。

读取器不得仅相信中央目录中的长度：复制、解压或 hash 时都要计数并在超限时终止。未知的 ZIP 容器元数据可以忽略，但未知文件和未声明的 payload 文件必须拒绝。

## 3. manifest.json

`manifest.json` 使用 UTF-8（无 BOM），顶层必须是对象。未知顶层字段、未知 `payload` 字段和未知类型 metadata 字段一律拒绝，避免不同实现对同一包产生不同解释。

公共 schema：

```json
{
  "formatVersion": 1,
  "type": "blocksTexture",
  "identifier": "a5a3f8d1-8b7c-4fe0-9c9f-1acf7efbf0a1",
  "name": "Granite Terrain",
  "version": "1.0.0",
  "payload": {
    "format": "scnet.blocks-texture.png-v1",
    "entry": "payload/texture.png",
    "mediaType": "image/png"
  },
  "metadata": {
    "width": 256,
    "height": 256
  }
}
```

所有公共字段均为必填，不接受 `null`：

| 字段 | 规则 |
|---|---|
| `formatVersion` | JSON number，必须是整数 `1`。新协议破坏性变更使用新整数，不做猜测或 fallback。 |
| `type` | 精确为 `mod`、`world`、`blocksTexture`、`characterSkin` 或 `furniturePack`。 |
| `identifier` | Mod 是规范 ModId；其余类型是小写 RFC 4122 UUID（`8-4-4-4-12` 十六进制形式）。全局、大小写不敏感地唯一。 |
| `name` | NFC 文本，去除首尾 Unicode 空白后长度 1–120 个 Unicode 标量值；不得含控制字符。它只是显示名称。 |
| `version` | 严格 SemVer 2.0.0，必须含 major/minor/patch，禁止前导零和 build metadata（`+...`）。保留 prerelease。原始字符串是版本键，比较使用 SemVer。 |
| `payload` | 下节定义的精确对象。 |
| `metadata` | 对应 `type` 的精确对象；不得放入展示摘要、发布者、审核状态、ContentId 或本地资产 ID。 |

ModId 继续使用现有规范：小写 ASCII 段以 `.` 分隔，段由字母或数字开头，可包含 `-` 和 `_`；最大 120 字符。SDK 必须提供单一验证器，游戏端和服务端不能各自实现正则。

Writer 必须用 UTF-8 无 BOM、两空格缩进、LF 换行和上表字段顺序写 manifest；每个 metadata 的字段顺序见后文。此规则使受官方 Writer 创建的包稳定可复现。Reader 不重新格式化 manifest，接受任何满足 JSON/schema 的原始 UTF-8 字节。

## 4. PackageHash

`PackageHash` 是 32 字节 SHA-256 的小写十六进制字符串。其输入不是 ZIP 原始字节，而是逻辑条目序列：精确原始 `manifest.json` 字节，及所有 payload 文件的精确未压缩字节。

1. 收集 `manifest.json` 与全部 payload 文件；目录不参与。
2. 先使用上节路径规则规范化路径，按 UTF-8 字节的 ordinal 升序排序。
3. 对每个条目依次写入：路径 UTF-8 字节长度的 4 字节无符号大端整数、路径 UTF-8 字节、内容长度的 8 字节无符号大端整数、内容原始字节。
4. 对完整序列计算 SHA-256，输出小写十六进制。

公开的逐字节测试向量位于 [PackageHashVectors.json](./PackageHashVectors.json)，其他语言实现必须得到其中记录的
`packageHash`，不能仅以同一实现内 Writer/Reader 结果相等作为互操作证据。
可由 Writer 直接打包的最小 Mod 黄金源位于 `Content.Packaging.Test/Assets/GoldenMod/`，其固定 PackageHash 为
`3f6d65a916b78a55ab6bab6a2c246888b4f7aa41913eff3c0d3c882bd6263a9a`。测试程序集还固定了五类最小逻辑包的 manifest、payload
字节和预期 PackageHash，任何协议实现变更都必须显式更新并审查这些值，不能由测试在运行时自行接受新 hash。

长度前缀和排序消除了连接歧义；ZIP 条目顺序、压缩等级、压缩方式、时间戳、extra fields 和 ZIP 注释不参与。manifest 的空白或字段顺序变化会改变 `PackageHash`，因为它改变了实际 manifest 字节。ContentServer 可另存原始 ZIP 的 `BlobHash`，但它不是公开包身份，不能替代 `PackageHash`。

每一个 Reader、缓存消费者、ContentServer 下载校验和 ModRuntime 首次加载都要以实际文件重算此值。Writer 先写入所有条目、再以同一算法验证产生的临时 ZIP，不能相信内存中的预期 hash。

## 5. payload 的公共规则

`payload` 固定包含 `format`、`entry` 和 `mediaType`，字段顺序也固定为此顺序。`entry` 是下列类型定义的主入口，必须以 `payload/` 开头，且必须指向一个实际文件。每一种类型都精确规定其允许的 payload 文件集合；不允许附带 README、缩略图、原始创作素材、旧格式包或任意 ZIP。

压缩策略不是身份的一部分：PNG 条目必须 `Store`；XML、JSON、文本和二进制世界区块使用 Deflate。实现可以选择不同 Deflate 等级。

## 6. 类型规范

### 6.1 Mod

```json
"payload": {
  "format": "scnet.mod-v1",
  "entry": "payload/mod.json",
  "mediaType": "application/json"
},
"metadata": {
  "side": "common",
  "entrypoints": { "common": "Example.ModEntry, Example" },
  "dependencies": [
    { "identifier": "example.core", "minimumVersion": "1.2.0", "optional": false }
  ]
}
```

`payload/mod.json` 是 `{ "formatVersion": 1 }`，用于让 payload 具有明确入口；它没有可由 manifest 重复的业务字段。允许的其他文件为：

```text
payload/assemblies/*.dll
payload/data/**
payload/assets/<identifier>/**
```

程序集只允许文件名为 `*.dll` 的直接子项；data 与 assets 中只允许文件。`assets/<identifier>/` 的 Identifier 必须与 manifest 精确一致。所有 Mod 贡献的语义、程序集隔离加载和资源命名空间保持现有 Mod 契约，但其权威字段迁移到统一 manifest 和 metadata；不再读取旧 `manifest.json`。

metadata 字段顺序为 `side`、`entrypoints`、`dependencies`。`side` 为 `common`、`client` 或 `server`。`entrypoints` 仅可含 `common`、`client`、`server`，值是非空程序集限定类型名；至少存在适用于声明 side 的入口，或包至少含一项 data/assets 贡献。dependencies 按 identifier ordinal 升序写出；每项字段顺序为 `identifier`、`minimumVersion`、`optional`，不得依赖自身、不得重复，minimumVersion 使用上述 SemVer 规则。

### 6.2 World

```json
"payload": {
  "format": "scnet.world-v1",
  "entry": "payload/world/Project.xml",
  "mediaType": "application/xml"
},
"metadata": {
  "projectFormat": "scnet-project-xml-v1",
  "regionsDirectory": "payload/world/Regions"
}
```

World 只能包含 `payload/world/Project.xml`、`payload/world/Regions/*.dat` 以及由项目格式明确引用的
`payload/world/` 下文件。不得包含 `EmbeddedContent/`、`.snapshot`、`backup/` 或任何独立内容包。所有引用路径必须保持在 `payload/world/` 内。

`scnet-project-xml-v1` 是独立 XML 文件，不是旧 `.scworld` 容器。它使用 UTF-8、无 DTD、无 XML namespace，根节点精确为
`Project`，并具有 `Version="SCNET-1"`、`Guid="9e9a67f8-79df-4d05-8cfa-61bd8095661e"`、
`Name="GameProject"`；根节点必须各包含一个 `Subsystems` 和 `Entities` 直接子节点，不能出现其他直接子节点。其内部
`Values`/`Value` 数据沿用 SCNET-1 项目数据语义，但不得包含指向包内路径、旧内容包、备份或 snapshot 的引用。

共享 World codec 负责上述 XML 外形、路径集合和安全规则；涉及模板数据库、Subsystem、实体字段和当前游戏版本的完整语义预检由
World installer 在暂存区调用游戏项目加载器完成。语义预检失败不得创建或覆盖世界。`Regions` 是大小写精确的目录，region
文件只能是直接子项 `*.dat`；除 `Project.xml` 与 `Regions/*.dat` 外，v1 不允许其他 World 文件。

metadata 字段顺序为 `projectFormat`、`regionsDirectory`。v1 不携带世界嵌入的皮肤、材质、家具或 Mod；外部资产引用由安装后的世界和资产管理流程显式处理。

### 6.3 BlocksTexture

```json
"payload": {
  "format": "scnet.blocks-texture.png-v1",
  "entry": "payload/texture.png",
  "mediaType": "image/png"
},
"metadata": {
  "width": 256,
  "height": 256
}
```

仅允许 `payload/texture.png`，必须是非交错、无动画的 PNG。权威 PNG 解码得到的宽高必须与 metadata 相同，宽和高均为 1–8192 的 2 的幂。metadata 字段顺序为 `width`、`height`。游戏只从 PNG 解码后的图像创建独立材质资产，不保存包身份。

### 6.4 CharacterSkin

```json
"payload": {
  "format": "scnet.character-skin.png-v1",
  "entry": "payload/skin.png",
  "mediaType": "image/png"
},
"metadata": {
  "width": 64,
  "height": 64
}
```

仅允许 `payload/skin.png`，必须是非交错、无动画 PNG。权威解码宽高必须与 metadata 相同，宽和高均为 1–1024 的 2 的幂。metadata 字段顺序为 `width`、`height`。本提案刻意不以文件名推断男性/女性或皮肤类型；这类显示和选择信息属于安装后的本地资产。

### 6.5 FurniturePack

```json
"payload": {
  "format": "scnet.furniture-designs-xml-v1",
  "entry": "payload/furniture/FurnitureDesigns.xml",
  "mediaType": "application/xml"
},
"metadata": {
  "designCount": 12
}
```

仅允许 `payload/furniture/FurnitureDesigns.xml`。文件使用 UTF-8、无 DTD、无 XML namespace，根节点必须为
`FurnitureDesigns`。每个直接子节点必须是 `Values`，具有唯一、非负十进制整数 `Name`，该值是设计索引；不接受前导正号、
负数、重复索引或其他直接子节点。子节点内部沿用当前 FurnitureDesign ValuesDictionary 数据语义，但不把 `.scfpack` ZIP
嵌入其中。共享 codec 校验 XML 外形、索引和数量；installer 使用游戏家具加载器完成链接设计和方块值语义预检。
metadata 字段顺序为 `designCount`，其值必须等于直接 `Values` 子节点数且范围为 1–1,024。

## 7. 一致性、缓存与安装规则

缓存和 ContentServer 都执行以下冲突规则：同一规范化 `Identifier + Version` 只能有一个 `PackageHash`；同一 Identifier 首次出现后 type 不可改变；同 version 同 hash 是幂等；同 version 不同 hash 或同 Identifier 不同 type 必须失败。不同物理 ZIP 可有相同逻辑 hash，缓存和 ContentServer 均保留首次成功接受的物理文件作为代表制品。

缓存只保存 `<PackageHash>.scpkg` 原包。导出只复制它；导入、下载和缓存命中都不修改 ModProfile；非 Mod 安装创建或显式替换本地资产，但不建立安装来源记录。制造新包先在临时区验证，再由 FilePicker 保存；它不自动缓存、安装或发布。

非 Mod installer 的规范输出是 Manager 拥有的本地资产，而不是另一种交换包：

| 类型 | Manager 拥有的逻辑布局 | 安装提交条件 |
|---|---|---|
| World | `<AssetKey>/Project.xml` 与 `<AssetKey>/Regions/*.dat` | 完整项目可预加载；创建生成新 AssetKey，替换保留目标 AssetKey |
| BlocksTexture | AssetKey、可变显示名和一份已解码验证的 PNG | 暂存 PNG 可再次解码且目标不是内置只读资产 |
| CharacterSkin | AssetKey、可变显示名和一份已解码验证的 PNG | 暂存 PNG 可再次解码且目标不是内置只读资产 |
| FurniturePack | AssetKey、可变显示名和游戏可加载的家具设计集合 | 全部设计和链接可预加载且目标不是内置只读资产 |

物理目录名、索引文件和事务日志属于各 Manager 的内部存储协议，不属于 `.scpkg`。但 Manager 必须提供按 AssetKey 的创建、显式替换、
打开、列举、引用检查和删除边界；显示名不能充当持久键。所有安装先写入同一 Storage 内的暂存目标并完成语义预检，再原子提交。
创建时名称冲突按 `Name`、`Name (1)`、`Name (2)` 递增；替换只接受调用方明确给出的同类型 AssetKey，不按包身份或名称猜测。

World 安装默认创建新世界。高级替换必须确认目标未运行、展示引用和影响范围并二次确认；提交时保留目标 AssetKey，显示名采用用户在确认界面选择的值。失败恢复原目录，成功后不保存来源 Identifier、Version、PackageHash 或 ContentId。

协议字段、创作入口和服务端元数据的写入权限，以及浏览器草稿与无代码制造契约见
[ContentCreationProtocol.md](./ContentCreationProtocol.md)。

## 8. 必须随 SDK 固化的测试向量

`Content.Packaging.Test` 应在协议实现同一提交中提供五类最小黄金包，及以下反例：

- 同一逻辑条目的 ZIP 顺序、时间戳、Store/Deflate 改变，PackageHash 不变。
- manifest 字节、任一 payload 字节、路径、文件长度改变，PackageHash 改变。
- 缺失/重复 manifest、大小写冲突路径、`..`、压缩炸弹、未声明 payload、嵌套 ZIP 和各限额均被拒绝。
- 每种类型的 metadata 与权威 payload 不一致时被拒绝。
- Mod 的 namespace、side、依赖和入口规则，World 的非嵌入内容规则，以及图片/家具的专属限制均被拒绝或接受。
- Writer 对相同规范输入产生字节稳定的 manifest 和相同 PackageHash；Reader 对其重新压缩版本得到相同 PackageHash。

## 9. ContentTool

`ContentTool` 位于 `ContentTool/`，只引用 `Content.Packaging`，不依赖游戏运行时或 ContentServer。它是开发、CI 和
仓库诊断调用统一 Reader 的最小入口：

```bash
dotnet run --project ContentTool/ContentTool.csproj -- inspect path/to/package.scpkg
dotnet run --project ContentTool/ContentTool.csproj -- verify path/to/package.scpkg
dotnet run --project ContentTool/ContentTool.csproj -- pack manifest.json path/to/payload output.scpkg
```

`inspect` 输出公共 manifest 和 `PackageHash`；`verify` 只在完整 ZIP、manifest、payload 布局和逻辑 hash 验证通过时返回
0。`pack` 将指定目录中的相对文件写入包内 `payload/`，通过共享 Writer 在同目录临时文件中生成并复验包，成功后再原子替换输出目标。格式错误、不可读文件或非 `.scpkg` 输入必须返回非零状态。

## 10. 实施顺序

五类黄金包、公开 hash 向量、`Content.Packaging`、ContentTool、Mod MSBuild 目标和 Mod Runtime 读取路径均已建立。Mod 构建和游戏解析现在只接受 `.scpkg`；其余旧内容格式仍按迁移计划等待对应 installer 替代链路就绪后删除。下一条纵向实施链路是让 ContentServer 复用同一 Reader、codec 和 PackageHash，并把包制品迁移到内容寻址文件存储。
