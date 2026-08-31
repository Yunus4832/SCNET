# SCNET 内容制造与发布契约 v1

> 状态：**正式协议；实现待后续阶段完成。** 本文只固定内容制造、浏览器草稿、提交和元数据权限边界；不要求阶段 0 提前实现游戏 UI、ContentWebUI 或 ContentServer 路由。

## 1. 权限与事实来源

`.scpkg` 一经 Writer 生成即不可被 ContentServer 重写。各字段只有一个权威写入方：

| 数据 | 初始写入方 | ContentServer 行为 | 是否进入 PackageHash |
|---|---|---|---|
| `formatVersion`、`type`、`payload`、类型 metadata | 对应官方制造入口 | 共享 Reader/codec 校验；不得修改 | 是 |
| `identifier` | Mod 项目声明；其他类型由制造入口生成或从显式基线包继承 | 校验格式、类型稳定和 Publisher 归属；不得重新分配或修改 | 是 |
| `name`、`version` | 创作者在制造时确认 | 作为不可变包事实保存；不得用仓库展示编辑覆盖 | 是 |
| payload 字节 | 创作源通过 codec 产生 | 权威校验；不得转码、修复或重打包 | 是 |
| `PackageHash` | Writer/Reader 计算 | 重算并作为公开制品身份；不接受客户端声明值 | 不作为输入字段 |
| `ContentId`、`PublisherId`、提交时间 | ContentServer | 服务端生成并保存 | 否 |
| Pending/Published/Rejected、审核人、审核时间、理由 | ContentServer 审核流程 | 只追加不可变审核事实；当前状态由事实派生 | 否 |
| 仓库展示摘要、标签、截图、上下架状态 | Publisher 提交，ContentServer 审核/管理 | 独立仓库元数据，可编辑但不能伪装成包内事实 | 否 |
| `BlobHash` | ContentServer 可选计算 | 仅诊断物理 ZIP，不向客户端充当身份 | 否 |

审核员可以批准、拒绝、下架或恢复仓库记录，不能修改包内字段、payload、Publisher 归属或已发生审核记录。若包错误，Publisher 必须使用新 SemVer 重新制造和提交。

## 2. 共享制造边界

所有入口最终调用同一个 `ContentPackageWriter`，输入模型等价于：经解析的 manifest、有限且可重开的 payload 条目流及每项已知长度。Writer 不接收游戏 Manager、浏览器对象、系统路径或 Android URI。

| 入口 | 创作源 | Identifier 行为 | 输出与副作用 |
|---|---|---|---|
| Mod MSBuild target | 项目 manifest、程序集、Data、Assets | 使用项目声明的稳定 ModId | 输出并 verify `.scpkg`；不发布、不安装 |
| ContentTool `pack` | manifest 文件与 payload 目录 | 使用 manifest 中已确认的值 | 输出并 verify `.scpkg`；适用于 CI/高级创作者 |
| 游戏制造器 | World/Furniture Manager 快照或 FilePicker 图片流 | 新内容生成 UUID；新版本只从用户选择的同类型基线包继承 | 临时生成并 verify，交给 FilePicker 保存；不缓存、不安装、不发布 |
| WebUI 简单制造 | 浏览器选择的 PNG 与草稿字段 | 新草稿生成 UUID；导入同类型草稿/基线时显式继承 | 后端无状态生成下载包，或同次请求生成后提交 Pending |

`IContentCreationSource` 位于游戏内容层，只投影 manifest 输入和可重开的条目流。World/Furniture 源必须在读取期间获得一致快照；图片源只暴露选中的一个 PNG。取消令牌贯穿源读取、Writer、临时文件刷新和 FilePicker 复制，取消后清理临时制品。

## 3. 创建新内容与创建新版本

- “创建新内容”必须生成新的小写 UUID Identifier；Mod 例外，其 Identifier 来自项目配置。
- “创建新版本”必须由用户显式选择同类型 `.scpkg` 基线，只读取并继承 Identifier；不能从安装资产、名称、ContentId 或历史记录推断。
- 新版本必须输入不同的合法 SemVer；制造器只做格式和与基线版本不同的检查，Publisher 所有权及仓库版本冲突由 ContentServer 最终裁决。
- 基线包读取不自动导入缓存、不安装，也不建立资产来源关系。
- 制造结果保存成功仍不自动导入；用户要使用该包必须走显式导入流程。

## 4. 浏览器 IndexedDB 草稿 schema

ContentWebUI 使用数据库 `scnet-content-drafts`，schemaVersion `1`，对象仓库如下：

### `drafts`

主键 `draftId`（随机 UUID），记录字段：

```text
schemaVersion       固定 1
draftId             浏览器本地草稿 UUID
type                仅 blocksTexture / characterSkin
identifier          内容 UUID；创建草稿时生成，后续编辑保持不变
name                manifest 显示名
version             SemVer 字符串
description         可选仓库展示草稿，不进入 manifest
baselineHash        可选；显式导入基线包时仅用于向用户展示
sourceBlobId        指向 sources.blobId
sourceFileName      仅展示，不作为协议字段
sourceMediaType     必须为 image/png
createdAt           ISO 8601 UTC
updatedAt           ISO 8601 UTC
```

索引：`updatedAt`、`type`、`identifier`。`draftId`、`baselineHash` 和时间戳均不进入生成包。

### `sources`

主键 `blobId`（随机 UUID），字段为 `blobId`、PNG `Blob`、`byteLength`、`sha256`。草稿与源 Blob 在同一 IndexedDB 事务中创建、替换和删除，不把大文件或 base64 放入 `localStorage`。

容量写入失败必须保留旧草稿，提示浏览器存储不足并允许直接制造/下载而不保存草稿。界面始终提示：草稿仅存在当前浏览器配置，清理站点数据、隐私模式回收或浏览器策略可能永久删除它。

草稿导出是一个只供 WebUI 使用的 JSON + PNG ZIP，不是 `.scpkg`，扩展名固定 `.scdraft`；导入只恢复浏览器草稿，不能提交、缓存或安装。JSON 必须包含上述 schemaVersion 和字段，源文件固定为 `source.png`，未知版本拒绝。该格式不属于公共游戏内容协议。

## 5. 无代码图片制造与提交契约

只允许 Active Publisher 调用后端制造能力。浏览器预览不具权威性，服务端每次请求都必须重新解码 PNG、验证尺寸/动画/交错规则并调用共享 Writer。

逻辑操作固定为：

1. `validate-source`：接收 type、PNG 流，返回权威宽高、媒体类型、源字节 SHA-256 和错误；不保存服务器状态。
2. `build-package`：接收 type、identifier、name、version、可选 description 和 PNG 流；生成并 verify `.scpkg` 后流式返回下载，服务器不保存草稿或源 Blob。
3. `submit-generated`：接收与 build 相同的输入；在同一请求内生成一次包，把该制品提交为 Pending，并返回 ContentId、VersionId、PackageHash 和审核状态。不得先返回一个包再用第二次重打包结果提交。
4. `submit-package`：接收任意官方入口产生的完整 `.scpkg`，返回权威 manifest、PackageHash、冲突或 Pending 结果；不重新制造。

具体 HTTP 路径可在阶段 2/3 按 Controller 组织确定，但请求语义、认证、无服务器草稿和响应事实不得改变。请求取消或连接断开必须停止读取、删除临时文件且不创建 Pending 记录。description 等仓库展示字段与包内 name 分开显示，不能覆盖 manifest 预览。

## 6. 阶段验收归属

- 阶段 1 已验证 Mod MSBuild、ContentTool 和共享 Writer。
- 阶段 2 实现完整包提交、权限事实和 ContentServer 文件存储。
- 阶段 3 实现 IndexedDB、图片预览、无状态制造及浏览器端到端测试。
- 阶段 4 实现游戏制造源、非 Mod installer 和大型 World 非平台流式压力测试。
- 阶段 5 实现真实平台 FilePicker 保存、取消及 UI 流程。
