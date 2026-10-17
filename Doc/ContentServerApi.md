# ContentServer HTTP API

服务职责和状态模型见 [ContentServer](ContentServer.md)，部署见
[ContentServer 部署](ContentServerDeployment.md)。本页列出当前端点；请求与响应类型定义在
[`ContentServer/Endpoints/`](../ContentServer/Endpoints/) 的各端点文件中。
下列 `{id}`、`{contentId}` 等为占位参数，带 GUID 路由约束的参数需提供合法 GUID。

## 认证与响应

公开查询、发布者申请、管理员申请及首次管理员初始化无需 Bearer Key。发布者工作台使用
发布者 Key，管理员工作台使用管理员 Key；申请中的身份可查询自身状态，业务操作仍要求 Active。
Key 无效或撤销返回 401，身份未激活或无权操作返回 403。服务只保存 Key hash，
新申请返回的 Key 明文只展示一次。

普通 JSON API 使用 `ResponseData<T>`：`success`、`message`、`code`、`errorData` 和 `data`。
例如默认成功的单项查询：

```json
{ "success": true, "message": "", "code": 0, "errorData": [], "data": {} }
```
无数据操作返回 `ResponseData`，不应要求存在 `data`；HTTP 状态与响应中的业务 code 应分别检查。
例外如下：

- 包下载及图片包构建成功时直接返回二进制流。
- `/api/v1/server-directory` 直接返回 [服务器源协议](Protocol/ServerSourceProtocol.md) 的
  `ServerSourcePage`；参数为 `limit/cursor`，不是页码。
- 公告及游戏版本列表的 `data` 为数组，不使用 `PagedData`。

采用页码分页的内容、模组、审核、来源和管理列表使用 `pageIndex`（从 1 开始）与
`pageSize`（默认 10，最大 100）；适用端点另支持 `query`、`status` 或类型筛选。
分页响应包含 `items`、`total`、`pageIndex`、`pageSize`。具体可选参数以端点声明为准。

## 首次初始化

空数据库通过 `GET /api/v1/administrators/initialization` 查询 `required` 及 Key 约束，
然后提交以下 JSON 到 `POST /api/v1/administrators/initialize`：

```json
{
  "name": "Administrator",
  "apiKey": "replace-with-a-strong-secret"
}
```

Key 为 16–128 个 ASCII 字母、数字或 `. _ ~ -`。创建成功返回 201，不回显 Key；
已有管理员时返回 409，不能借此补发或轮换凭据。首次管理员是唯一超级管理员，
只有它可撤销或恢复其他管理员的 Key；其自身 Key 不提供撤销入口。

## 内容投稿

`POST /api/v1/publisher/submissions` 使用 `multipart/form-data`，唯一必填文件字段为
`package`，值为完整 `.scpkg`。类型、标识、名称、版本、简介、说明与 metadata 均来自包内清单，
不接受外部字段覆盖。首次创建与已有内容的新版本使用同一提交入口；标识归属、类型稳定、
版本冲突和包内容由服务端检查，同版本同 hash 可幂等返回既有结果。

图片制造端点处理 PNG，支持源验证、构建下载和生成后直接投稿；详细输入与草稿边界见
[内容制造协议](Protocol/ContentCreationProtocol.md)。

## 发布与管理规则

- 内容版本从 Pending 审核为 Published 或 Rejected，重复审核返回 409；内容上下架独立于版本状态。
- 匿名包下载要求存在已发布且内容仍上架的有效版本引用；下架、删除后不再可下载。
- 管理员可下载尚未通过审核的提交包，用于审核测试。
- 第三方服务器源投稿与批准时会读取并校验来源；服务不持久化对方返回的服务器列表。
- 服务器投稿编辑后重新进入待审核；发布者上下架与管理员停用分别管理。
- 信令服务登记只保存服务信息，不保存临时房间；修改后重新审核，公开控制地址要求 HTTPS。
- 公告与游戏版本先创建草稿，可编辑后发布，已发布记录可撤回。一个游戏版本包含多个平台安装包。
  记录外部链接和 SHA-256，不访问链接、不记录安装包大小、不托管安装包。

## 端点清单

清单按受众分组。初始化及身份申请位于相应身份组，但不要求已经持有该身份的 Key。

### 匿名浏览

```text
GET    /api/v1/announcements
GET    /api/v1/content/{contentId}
GET    /api/v1/content
GET    /api/v1/content/{contentId}/versions
GET    /api/v1/health
GET    /api/v1/mods/{modId}/versions/{version}
GET    /api/v1/mods/{modId}
GET    /api/v1/mods
GET    /api/v1/packages/{hash}
GET    /api/v1/game-releases/latest/{platform}
GET    /api/v1/game-releases
GET    /api/v1/rendezvous-services
GET    /api/v1/server-sources
GET    /api/v1/server-directory
```

### 发布者

```text
POST   /api/v1/publishers
GET    /api/v1/publisher
GET    /api/v1/publisher/content
POST   /api/v1/publisher/content/{contentId}/disable
POST   /api/v1/publisher/content/{contentId}/enable
GET    /api/v1/publisher/submissions/{versionId}
GET    /api/v1/publisher/submissions
POST   /api/v1/publisher/submissions
POST   /api/v1/publisher/packages/image/build
POST   /api/v1/publisher/packages/inspect
POST   /api/v1/publisher/packages/image/submit
POST   /api/v1/publisher/packages/image/validate-source
DELETE /api/v1/publisher/rendezvous-services/{id}
POST   /api/v1/publisher/rendezvous-services/{id}/edit
GET    /api/v1/publisher/rendezvous-services
POST   /api/v1/publisher/rendezvous-services
GET    /api/v1/publisher/server-sources
POST   /api/v1/publisher/server-sources
POST   /api/v1/publisher/servers/{id}/disable
POST   /api/v1/publisher/servers/{id}/enable
GET    /api/v1/publisher/servers
POST   /api/v1/publisher/servers
PUT    /api/v1/publisher/servers/{id}
```

### 管理员及初始化

```text
GET    /api/v1/admin/administrator-applications
POST   /api/v1/admin/administrator-applications/{administratorId}/approve
POST   /api/v1/admin/administrator-applications/{administratorId}/reject
POST   /api/v1/admin/administrators/{administratorId}/restore-key
POST   /api/v1/admin/administrators/{administratorId}/revoke-key
GET    /api/v1/admin/self
POST   /api/v1/administrators/applications
GET    /api/v1/administrator
GET    /api/v1/administrators/initialization
POST   /api/v1/administrators/initialize
POST   /api/v1/admin/announcements
GET    /api/v1/admin/announcements
POST   /api/v1/admin/announcements/{id}/publish
PUT    /api/v1/admin/announcements/{id}
POST   /api/v1/admin/announcements/{id}/withdraw
POST   /api/v1/admin/submissions/{versionId}/approve
GET    /api/v1/admin/content
POST   /api/v1/admin/content/{contentId}/disable
GET    /api/v1/admin/submissions/{versionId}/package
POST   /api/v1/admin/content/{contentId}/enable
POST   /api/v1/admin/submissions/{versionId}/reject
GET    /api/v1/admin/submissions
DELETE /api/v1/admin/content/{contentId}
POST   /api/v1/admin/publishers/{publisherId}/approve
GET    /api/v1/admin/publishers
POST   /api/v1/admin/publishers/{publisherId}/reject
POST   /api/v1/admin/publishers/{publisherId}/restore-key
POST   /api/v1/admin/publishers/{publisherId}/revoke-key
POST   /api/v1/admin/publishers/{publisherId}/suspend
POST   /api/v1/admin/game-releases
GET    /api/v1/admin/game-releases
POST   /api/v1/admin/game-releases/{id}/publish
PUT    /api/v1/admin/game-releases/{id}
POST   /api/v1/admin/game-releases/{id}/withdraw
POST   /api/v1/admin/rendezvous-services/{id}/approve
DELETE /api/v1/admin/rendezvous-services/{id}
POST   /api/v1/admin/rendezvous-services/{id}/disable
GET    /api/v1/admin/rendezvous-services
POST   /api/v1/admin/rendezvous-services/{id}/reject
POST   /api/v1/admin/server-sources/{id}/approve
DELETE /api/v1/admin/server-sources/{id}
GET    /api/v1/admin/server-sources
POST   /api/v1/admin/server-sources/{id}/reject
POST   /api/v1/admin/servers/{id}/approve
DELETE /api/v1/admin/servers/{id}
GET    /api/v1/admin/servers
POST   /api/v1/admin/servers/{id}/reject
POST   /api/v1/admin/servers/{id}/restore
POST   /api/v1/admin/servers/{id}/suspend
PUT    /api/v1/admin/servers/{id}
```
