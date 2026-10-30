# SCNET 内容制造与发布契约 v1

本文描述内容制造、浏览器草稿、提交和元数据权限边界。格式校验以
[内容包协议](ContentPackageProtocol.md)及共享 Reader 为准；下文单独列明当前实现限制。

## 1. 权限与事实来源

`.scpkg` 一经 Writer 生成即不可被 ContentServer 重写。各字段只有一个权威写入方：

| 数据 | 初始写入方 | ContentServer 行为 | 是否进入 PackageHash |
|---|---|---|---|
| `formatVersion`、`type`、`payload`、类型 metadata | 对应官方制造入口 | 共享 Reader/codec 校验；不得修改 | 是 |
| `identifier` | Mod 项目声明；其他类型由制造入口生成或从显式基线包继承 | 校验格式、类型稳定和 Publisher 归属；不得重新分配或修改 | 是 |
| `name`、`summary`、`description`、`version` | 创作者在制造时确认 | 作为不可变包事实保存；不得用仓库展示编辑覆盖 | 是 |
| payload 字节 | 创作源通过 codec 产生 | 权威校验；不得转码、修复或重打包 | 是 |
| `PackageHash` | Writer/Reader 计算 | 重算并作为公开制品身份；不接受客户端声明值 | 不作为输入字段 |
| `ContentId`、`PublisherId`、提交时间 | ContentServer | 服务端生成并保存 | 否 |
| Pending/Published/Rejected、审核人、审核时间、理由 | ContentServer 审核流程 | 保存当前状态，并由领域事件追加审核记录 | 否 |
| 上下架状态 | Publisher 或 Administrator | 独立仓库状态，不修改包内事实；当前不定义内容截图或标签编辑接口 | 否 |
| `BlobHash` | ContentServer 可选计算 | 仅诊断物理 ZIP，不向客户端充当身份 | 否 |

审核员可以批准、拒绝、下架或恢复仓库记录，不能修改包内字段、payload、Publisher 归属或已发生审核记录。若包错误，Publisher 必须使用新 SemVer 重新制造和提交。

## 2. 共享制造边界

所有入口最终调用同一个 `ContentPackageWriter`，输入模型等价于：经解析的 manifest、有限且可重开的 payload 条目流及每项已知长度。Writer 不接收游戏 Manager、浏览器对象、系统路径或 Android URI。

| 入口 | 创作源 | Identifier 行为 | 输出与副作用 |
|---|---|---|---|
| Mod MSBuild target | 项目 manifest、程序集、Data、Assets | 使用项目声明的稳定 ModId | 输出并 verify `.scpkg`；不发布、不安装 |
| ContentTool `pack` | manifest 文件与 payload 目录 | 使用 manifest 中已确认的值 | 输出并 verify `.scpkg`；适用于 CI/高级创作者 |
| 游戏制造器 | 未运行的 World/Furniture 本地资产或 FilePicker 图片流 | GUI 新建生成 UUID；底层 API 可显式传入同类型基线包继承 | 临时生成并 verify，交给 FilePicker 保存；不缓存、不安装、不发布 |
| WebUI 简单制造 | 浏览器选择的 PNG/WebP 与草稿字段 | 新草稿生成 UUID；更新目标使用发布者明确选择的既有内容标识 | 后端无状态生成下载包，或同次请求生成后提交 Pending |

游戏制造逻辑位于 `ContentPackageCreationManager`，使用 `ContentPackageWriteEntry` 提供条目流。
World 从未运行的世界目录读取，Furniture 从本地资产读取，图片入口先复制并验证选中的 PNG/WebP。
制造 API 同步执行，GUI 通过后台任务调用，不支持取消已开始的编码；
临时制品由调用方释放，HTTP 制造入口另使用请求取消令牌。

## 3. 创建新内容与创建新版本

- Mod Identifier 来自项目配置；游戏 GUI 新建非 Mod 内容时生成小写 UUID。
- 游戏底层 `ContentCreationIdentity.BaselinePackage` 可继承同类型基线包的 Identifier，
  但当前游戏 GUI 没有选择基线包或“创建新版本”的入口。
- WebUI 更新已有内容时使用发布者明确选择的目标 Identifier，不从安装资产、显示名称或
  本地历史推断。完整包投稿仍以包内 Identifier 为准，并在预览时核对更新目标。
- 版本必须为合法 SemVer；服务端裁决发布者所有权和版本冲突。游戏底层基线 API 目前
  不额外拒绝与基线相同的版本，不能把“生成成功”当作“可以作为新版本投稿”。
- 基线读取不自动导入缓存、不安装；制造结果保存成功也不自动导入。

## 4. 浏览器 IndexedDB 草稿 schema

ContentWebUI 使用数据库 `scnet-content-drafts`，schemaVersion `1`，对象仓库如下：

### `drafts`

主键 `draftId`（随机 UUID），记录字段：

```text
schemaVersion       固定 1
draftId             浏览器本地草稿 UUID
type                仅 BlocksTexture / CharacterSkin（浏览器草稿类型，非 manifest 的小写枚举）
identifier          内容 UUID；创建草稿时生成，后续编辑保持不变
name                manifest 显示名
version             SemVer 字符串
summary             必填简短说明，进入 manifest
description         必填详细描述，进入 manifest
baselineHash        可选字段；当前 UI 不写入，也没有基线包导入入口
sourceBlobId        指向 sources.blobId
sourceFileName      仅展示，不作为协议字段
sourceMediaType     必须为 image/png 或 image/webp，与实际源编码一致
createdAt           ISO 8601 UTC
updatedAt           ISO 8601 UTC
```

索引：`updatedAt`、`type`、`identifier`。`draftId`、`baselineHash` 和时间戳均不进入生成包。

### `sources`

主键 `blobId`（随机 UUID），字段为 `blobId`、PNG/WebP `Blob`、`byteLength`、`sha256`。草稿与源 Blob 在同一 IndexedDB 事务中创建、替换和删除，不把大文件或 base64 放入 `localStorage`。

容量写入失败必须保留旧草稿，提示浏览器存储不足并允许直接制造/下载而不保存草稿。界面始终提示：草稿仅存在当前浏览器配置，清理站点数据、隐私模式回收或浏览器策略可能永久删除它。

当前不定义草稿交换格式，也不提供导入或导出。用户可以删除单条草稿或一次清理全部草稿及其源 Blob；清理必须在同一 IndexedDB 事务中覆盖 `drafts` 和 `sources`，不能留下无主源文件。

## 5. 无代码图片制造与提交契约

只允许 Active Publisher 调用后端制造能力。浏览器预览不具权威性，服务端每次请求都必须重新识别并解码 PNG/WebP、验证格式、尺寸及静态图约束并调用共享 Writer。

逻辑操作固定为：

1. `validate-source`：接收 type、PNG/WebP 流，返回权威宽高、媒体类型、源字节 SHA-256 和错误；不保存服务器状态。
2. `build-package`：接收 type、identifier、name、version、必填 summary、description 和 PNG/WebP 流；生成并 verify `.scpkg` 后流式返回下载，服务器不保存草稿或源 Blob。
3. `submit-generated`：`POST /api/v1/publisher/packages/image/submit`，接收与 build 相同的输入；在同一请求内生成一次包并提交，返回 `ContentVersionResponse`。不得先返回一个包再用第二次重打包结果提交。
4. `submit-package`：`POST /api/v1/publisher/submissions`，通过 multipart 的 `package` 文件字段接收任意官方入口产生的完整 `.scpkg`；检查制品后返回 `ContentVersionResponse`，不重新制造，也不返回完整 manifest。

图片操作由 `ContentServer.Endpoints.Publisher.Packages`、完整包投稿由 `ContentServer.Endpoints.Publisher.Contents` 下的 FastEndpoint 实现；Endpoint 负责读取请求和发送命令，制造与提交规则由应用层和领域层执行。HTTP 请求取消令牌用于中止读取和构建；临时文件应清理。已经提交的业务事务不因浏览器断开而撤销，
重试前应查询投稿状态，不能把连接中断等同于未创建 Pending 记录。新投稿成功返回 HTTP 201；已存在的同一制品返回 HTTP 200，冲突则拒绝。JSON 的 `data` 为 `ContentVersionResponse`，包含 `contentId`、`versionId`、`packageHash`、`contentStatus`、版本审核 `status` 及 `name`、`summary`、`description` 等字段，不等同于原始 manifest。新版本进入 Pending；重复提交返回既有版本的当前状态。summary 和 description 均来自包内 manifest，预览和提交响应必须原样展示。

## 6. 实现约束

Mod MSBuild、ContentTool、游戏制造器和 WebUI 制造入口共享同一 Writer、Reader、codec 与
PackageHash 定义。平台 FilePicker、浏览器 IndexedDB 和 ContentServer 文件存储只负责各自边界内的
保存与传输，不能建立第二套包格式、身份算法或隐式安装流程。

## 7. World 数据保留

World 制造器读取存档 `Project.xml`，共享 codec 接受当前根节点结构：必需的 `Subsystems`、
`Entities`，以及各至多一个可选的 `BlockRegistry`、`ExtensionData`。方块编号映射和世界扩展数据按原始字节
保留，安装时不重新分配编号，也不剥离模组数据。未知或重复的根节点仍被拒绝。

图片创建和上传保留源文件的原始编码与字节，不自动转码、重编码或预乘；安装后也原样保存。WebP 是推荐格式，PNG 同样受支持。
