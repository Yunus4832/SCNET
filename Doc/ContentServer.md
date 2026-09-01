# ContentServer

ContentServer 是 SCNET 的独立 ASP.NET Core 内容服务，项目位于 `ContentServer/`，并注册在
`SCNET.slnx` 的 `06 Content` 逻辑文件夹中。它负责发布者申请、内容版本审核、匿名目录查询和包下载；
玩家身份、联机身份和平台 FilePicker 不属于该服务。

ContentServer 也是游戏客户端唯一的远程内容地址。`ModProfile.ContentServerUrl` 和联机服务器信息可以为
特定会话声明该地址；未声明时使用 `Settings.ContentServerUrl`。模组解析通过 `/api/v1/mods` 查询精确版本，
所有类型最终都从 `/api/v1/packages/{sha256}` 流式下载并进入统一 ContentPackageCache。仓库中不再包含独立的
模组服务或另一套模组存储协议。

## 分层与依赖方向

ContentServer 保持为一个 Web 项目，但源码按 DDD 职责分为四个命名空间：

```text
Controllers -> Application -> Domain
                   |
                   v
              Infrastructure
```

- `ContentServer.Domain`：Publisher、Content、Administrator 聚合、强类型 ID、值状态和领域事件；聚合继承 NetCorePal `Entity<TStronglyTypedId>` 并实现 `IAggregateRoot`。
- `ContentServer.Application.Commands`：每个文件包含一个 Command 及其对应的 CommandHandler，写入使用 `ICommand`/`ICommandHandler`。
- `ContentServer.Application.Queries`：每个文件包含一个 Query 及其对应的 QueryHandler，读取使用 `IQuery`/`IQueryHandler`。
- `ContentServer.Domain.Reviews`：独立的审核记录域；Publisher 和 Content 聚合只发布审核结果领域事件，
  对应事件处理器发送 `CreateReviewRecordCommand` 创建审核记录。
- `ContentServer.Domain.Packages`：独立的包存储域。上传 Endpoint 完成大小和表单校验后，先发送
  `StorePackageBlobCommand` 持久化并按 SHA-256 去重，再把返回的 `PackageBlobId` 传给
  Content 域命令；Content 聚合只引用包 ID，不持有包数据。
- `ContentServer.Infrastructure`：NetCorePal EF Core 聚合仓储、`AppDbContextBase`、实体映射、SQLite、数据库初始化和迁移。
- `ContentServer.Controllers`：HTTP 路由、表单解析和响应映射，不直接实现认证或业务状态转换。
  HTTP 请求与响应模型分别位于 `Controllers/Contracts/Requests` 和
  `Controllers/Contracts/Responses`，Application 层不引用这些 Web API 契约。

Controller 将 HTTP Request 映射为 Command 或 Query，并将 Application DTO 通过
`Controllers/Mappings` 映射为 HTTP Response。Command 和 Query 只返回 Application DTO，不返回领域实体、
`ResponseData` 或其他 HTTP 类型；API 响应外壳统一由 Controller 调用 `AsResponseData()` 创建。

除包下载的原始二进制流外，HTTP API 统一返回 NetCorePal `ResponseData<T>`：

```json
{
  "success": true,
  "message": "",
  "code": 200,
  "errorData": null,
  "data": {}
}
```

Controller 端点直接声明具体的 `ResponseData<TResponse>` 返回类型，响应载荷使用
`PublisherResponse`、`ContentVersionResponse`、`ModPackageResponse` 等明确 DTO；列表统一使用
NetCorePal `PagedData<T>`。只有需要返回 `FileResult` 的包下载端点使用 `IActionResult`，从而让编译器和 OpenAPI
能够获得其余接口的完整响应契约。

业务代码不捕获异常以拼装 HTTP 错误。可预期的请求、认证和业务错误抛出 `KnownException`，状态码写入
`ErrorCode`；`GlobalExceptionHandler` 在 HTTP 边界统一转换响应。EF 唯一约束冲突统一返回 409，无法识别的异常
记录完整服务端日志并只向客户端返回 `internal_server_error` 和 500，避免泄露内部信息。模型绑定错误也使用同一
响应结构。无返回数据的成功操作返回 HTTP 200 和不带 `data` 的 `ResponseData`；包下载成功时仍直接返回文件流。

API Key 认证由 `Middlewares/ApiKeyAuthenticationMiddleware` 在进入 Controller 前统一处理。Middleware 把已认证的
Publisher 或 Administrator ID 写入 scoped `ApiKeyAuthenticationContext`；该上下文只保存强类型身份 ID，不保存
由 EF Core 跟踪的领域实体。需要认证的 Controller 通过构造函数显式
依赖该上下文，不从 `HttpContext` 隐式查找身份。无需认证的发布者申请使用独立的
`PublisherApplicationController`。Key 的随机生成、显示前缀和 SHA-256 计算集中在 `Utils/ApiKeyUtility`，不与 HTTP
或数据库查询混合。
管理员 Key 有效但身份尚未 Active 时，身份状态接口仍允许访问，管理操作返回 HTTP 403；只有 Key 不存在或已撤销时
才返回 HTTP 401。WebUI 仅在 401 时将本地 Key 标记为失效并退回工作台入口，不能把权限不足误判为凭证失效。

ContentServer 不提供登录或服务端会话。Web UI 在浏览器 `localStorage` 中缓存用户主动保存的 API Key，
每次请求显式发送 Bearer Key；退出工作台只清除当前 Key 的本地缓存。游戏客户端同样自行负责 Key 的安全存储。
公共内容、发布者申请、首次初始化、工作台入口、发布者工作台和管理员工作台使用独立页面。

命令由 `AddUnitOfWorkBehaviors` 自动开启事务并在处理器成功后保存工作单元，因此命令处理器不得自行调用
`SaveChangesAsync`。聚合的写入通过 `RepositoryBase` 仓储完成；认证使用时间和启动初始化不属于命令流程，可以显式保存。
内容上传 Endpoint 先发送 `FindContentItemQuery` 判断发布者的内容标识是否存在，再分别路由到
`CreateContentItemCommand` 或 `UpdateContentItemVersionCommand`。创建聚合和追加版本是两个独立职责；Command
不返回领域实体或重复的提交参数。工作单元提交后，Endpoint 使用自己已有的发布者 ID、内容标识和版本发送
`GetContentVersionQuery`，读取包含最终强类型 ID 和包信息的
查询 DTO；版本集合的新增由 EF Core 变更检测自动持久化，不需要为子实体增加专用仓储写入 API。
WebUI 将提交入口拆分为“创建新内容”和“更新已有内容”。更新时先从发布者自己的内容中选择目标，保持类型和
`identifier` 不变，预填并允许修改名称与简介，然后提交新的版本号和资源包；更新命令会在追加版本的同一事务中
保存名称与简介。发布者工作台按内容聚合列出自己提交的内容，并允许发布者直接下架或恢复自己的内容；该操作不会
改变已经审核通过的版本状态。下架同时从公共列表隐藏内容并停止所有版本的匿名包下载，已有脚本直链也会返回 404；
恢复上架后原下载链接重新有效。版本记录为每个已发布版本提供可复制的匿名下载链接。

领域事件使用 NetCorePal 自带的 `IDomainEvent`/`IDomainEventHandler`，由 `AppDbContextBase` 协调保存与进程内发布。
审核类事件处理器发送 Reviews 域命令，并通过独立工作单元持久化审核记录；
将来若接入邮件、对象存储或外部消息队列，应增加 Outbox 后再异步投递，不能把“数据库提交后直接发布”误当成可靠消息。

CQRS 在这里表示代码职责分离，而不是把数据拆成两套数据库：命令和查询仍使用同一个 SQLite 数据源，避免当前规模下不必要的最终一致性成本。
列表 Query 使用 NetCorePal `PageRequest` 和 `ToPagedData`，HTTP 缺省值为第一页、每页 10 条，并计算总条数。
`pageSize` 最大为 100。内容、提交、发布者和管理员列表还接受可选的 `query` 参数，使用参数化 LINQ
在名称、稳定标识、简介或联系方式等适用字段上进行模糊搜索。
版本列表先对 `ContentVersions` 单表应用过滤和分页，再分别读取当前页关联的 `Contents` 与 `PackageBlobs`
并组合查询 DTO；其他读取也使用 LINQ 方法链和单表分步查询，不使用 LINQ query-comprehension 的多表 Join。
管理员内容管理列表使用独立的 `ListContentItemsQuery` 直接分页查询 `Contents`，每个聚合只返回一项，不通过版本
列表去重，并支持按内容类型和关键字组合筛选；版本查询只用于审核、内容广场和版本历史。
版本列表按 UUID v7 强类型 ID 倒序排列，与版本创建顺序一致。公共内容广场按内容聚合只返回最新已发布版本，版本历史接口
分页返回同一内容的全部已发布版本；新版本发布不会取消旧版本的公开下载资格。

实体 ID 使用 `IGuidStronglyTypedId`，`ContentServerDbContext.ConfigureConventions` 统一注册强类型 ID 转换，
每个实体的 `EntityConfiguration` 通过 `UseGuidVersion7ValueGenerator()` 配置生成器。领域工厂不生成或赋值 ID；
ID 在实体加入 EF 跟踪时自动产生。`PackageBlob` 使用独立的 `PackageBlobId`，同时以 SHA-256 `Hash` 作为唯一内容去重键。
HTTP API 在边界处把强类型 ID 显式输出为字符串，协议不暴露内部值对象结构。

## 存储

服务使用单个 SQLite 数据库文件。默认路径为：

```text
ContentServer/Data/content-server.db
```

发布者、Key hash、内容元数据、审核记录和包 BLOB 都保存在该文件中。部署备份时只需一致性备份该数据库文件。

核心表：

- `Administrators` / `AdministratorKeys`
- `Publishers` / `PublisherKeys`
- `Contents` / `ContentVersions`
- `PackageBlobs`
- `ReviewRecords`

数据库结构由 `Infrastructure/Migrations` 中的 EF Core 迁移维护，服务启动时自动执行尚未应用的迁移。模型发生变化后，在仓库根目录创建迁移：

```bash
dotnet ef migrations add <MigrationName> \
  --project ContentServer/ContentServer.csproj \
  --startup-project ContentServer/ContentServer.csproj \
  --output-dir Infrastructure/Migrations
```

提交模型修改时必须同时提交迁移和 `ContentServerDbContextModelSnapshot.cs`。部署升级前仍应备份 SQLite 文件；
自动迁移解决结构升级，不代替数据备份。

## 配置

`appsettings.json`：

```json
{
  "ContentServer": {
    "DatabasePath": "Data/content-server.db",
    "MaximumPackageBytes": 268435456,
    "AllowedOrigins": ["https://content-ui.example.com"]
  }
}
```

生产环境可以通过环境变量覆盖数据库路径：

```text
ContentServer__DatabasePath
```

`AllowedOrigins` 只用于独立部署的浏览器 SPA，必须填写完整来源（协议、域名和端口），不使用通配符，
也不允许跨域凭据。匿名下载和 Bearer API Key 请求均可跨域；响应暴露 `Content-Disposition` 供前端取得文件名。

## 独立 WebUI

`ContentWebUI/` 是注册在 `SCNET.slnx` 的 `06 Content` 逻辑文件夹中的 Vue 3 单页应用。它可以与
ContentServer 分别部署，生产构建后的 `runtime-config.json` 配置 API 地址，静态服务器需将未知路由回退到
`index.html`。WebUI 负责 API Key 的临时缓存和角色导航，不改变 ContentServer 的无账户、无 Cookie 会话模型。

服务器启动只执行数据库迁移，不隐式创建或修改管理员。空数据库通过
`POST /api/v1/administrators/initialize` 创建唯一的初始管理员；请求提交管理员名称和 API Key，服务只保存
Key 的 SHA-256 hash。数据库中一旦存在管理员，该端点固定返回 HTTP 409，不允许覆盖已有管理员或轮换其 Key。

## 状态模型

创建发布者申请时，服务立即生成 Publisher Key，只在响应中返回一次，并创建 `Pending` Publisher。
同一个未撤销 Key 可以调用自身状态接口。只有 `Active` Publisher 可以上传内容。

发布者状态：

- `Pending`
- `Active`
- `Rejected`
- `Suspended`

内容版本状态：

- `Pending`
- `Published`
- `Rejected`

内容本身可以是 `Active` 或 `Disabled`。匿名 API 只返回 Content 为 `Active` 且版本为 `Published` 的记录。

## API

匿名接口：

```text
GET  /api/v1/health
GET  /api/v1/content
GET  /api/v1/content/{contentId}
GET  /api/v1/content/{contentId}/versions
GET  /api/v1/packages/{sha256}
GET  /api/v1/mods
GET  /api/v1/mods/{modId}
GET  /api/v1/mods/{modId}/versions/{version}
```

一次性管理员初始化接口：

```text
GET  /api/v1/administrators/initialization
POST /api/v1/administrators/initialize
```

GET 响应中的 `required` 表示管理员表是否为空，独立 WebUI 据此只在首次运行时显示初始化入口。
初始化会在同一聚合事务中创建 Active 管理员及其 Key；如果数据库存在管理员但不存在 Key，说明数据库已经
处于不符合领域不变量的状态，匿名初始化不会为既有管理员补发 Key。未发布阶段的测试数据库应删除后重新初始化。

请求：

```json
{
  "name": "Administrator",
  "apiKey": "replace-with-a-strong-secret"
}
```

初始 API Key 必须为 16–128 个字符，只允许 ASCII 字母、数字以及 `. _ ~ -`。WebUI 从初始化状态接口
读取这些约束，实时显示长度、字符集和两次输入是否一致，并可以在浏览器本地生成 `scadm_` 随机密钥。

该端点不需要认证，但只在管理员表为空时有效；创建成功返回 HTTP 201，之后再次调用返回
`administrator_already_initialized` 和 HTTP 409。响应不会回显 API Key 明文。

后续管理员通过申请产生，不能由现有管理员直接创建：

```text
POST /api/v1/administrators/applications
GET  /api/v1/administrator
GET  /api/v1/admin/administrator-applications
POST /api/v1/admin/administrator-applications/{id}/approve
POST /api/v1/admin/administrator-applications/{id}/reject
```

申请时提交名称、联系方式和可选说明，服务器生成只显示一次的 Key。未撤销的 Key 可以查询自身状态；
只有 Active 管理员能访问管理 API。管理员申请只能从 Pending 审核为 Active 或 Rejected，重复审核返回 HTTP 409。


所有列表接口接受以下查询参数：

```text
pageIndex  页码，从 1 开始，默认 1
pageSize   每页条数，默认 10
query      可选搜索词
```

响应 `data` 为 `PagedData<T>`，包含 `items`、`total`、`pageIndex` 和 `pageSize`。适用接口包括匿名内容和
模组列表、发布者提交列表，以及管理员发布者、提交和内容列表；单项查询和文件下载不分页。

发布者接口：

```text
POST /api/v1/publishers
GET  /api/v1/publisher
GET  /api/v1/publisher/submissions
GET  /api/v1/publisher/submissions/{versionId}
POST /api/v1/publisher/submissions
```

内容提交使用 `multipart/form-data`：

```text
type        Mod / World / BlocksTexture / CharacterSkin / FurniturePack
identifier  稳定唯一标识
name        显示名称
version     版本
summary     可选简介
metadata    可选 JSON
package     内容包文件
```

发布者接口使用发布者 Bearer Key：

```text
GET  /api/v1/publisher/content
POST /api/v1/publisher/content/{id}/disable
POST /api/v1/publisher/content/{id}/enable
```

上下架命令会再次校验内容归属，发布者不能操作其他发布者的内容；非 Active 发布者不能改变内容状态。

管理员接口使用配置的 Bearer Key：

```text
GET  /api/v1/admin/publishers
POST /api/v1/admin/publishers/{id}/approve
POST /api/v1/admin/publishers/{id}/reject
POST /api/v1/admin/publishers/{id}/suspend
POST /api/v1/admin/publishers/{id}/revoke-key
POST /api/v1/admin/publishers/{id}/restore-key
GET  /api/v1/admin/administrator-applications
POST /api/v1/admin/administrators/{id}/revoke-key (仅超级管理员)
POST /api/v1/admin/administrators/{id}/restore-key (仅超级管理员)
GET  /api/v1/admin/submissions
GET  /api/v1/admin/content
GET  /api/v1/admin/submissions/{id}/package
POST /api/v1/admin/submissions/{id}/approve
POST /api/v1/admin/submissions/{id}/reject
POST /api/v1/admin/content/{id}/disable
POST /api/v1/admin/content/{id}/enable
```

管理员包下载接口不要求版本已经发布，用于在审核 `Pending` 提交时下载和测试原始文件；它仍要求管理员 Bearer
Key。匿名 `/api/v1/packages/{sha256}` 继续只允许下载属于 `Published` 版本且内容状态为 `Active` 的包。
发布者可以按 `versionId` 查询自己的单次提交，响应包含当前审核状态、审核消息和审核时间；不能读取其他发布者的提交。
发布者申请和内容版本只能从 `Pending` 审核一次，重复通过或拒绝返回 HTTP 409；发布者暂停是独立的管理状态操作。
所有 Active 管理员都可以撤销或恢复发布者的 Key。首次初始化创建的管理员是唯一的超级管理员；其余审核与普通管理员一致，
但只有它可以撤销或恢复普通管理员的 Key。撤销只设置 `RevokedAt`，恢复后原 Key 可继续使用；超级管理员 Key
受领域规则保护，不提供撤销或恢复 API。发布者和管理员列表通过 `hasActiveKey` 返回当前 Key 是否有效。

## 启动

```bash
dotnet run --project ContentServer/ContentServer.csproj
```

开发环境前端使用 Vite（`ContentWebUI`）单独运行，端口默认 `5174`，并将 `/api` 代理到 ContentServer
（默认 `http://localhost:5000`），改前端代码即时热更新。ContentServer 构建或发布时会通过
`ContentServer.csproj` 的 build target 自动执行前端构建，并把 `ContentWebUI/dist` 复制到 `wwwroot`，
因此调试时用 `npm run dev`，部署时只需发布 ContentServer。

## 发布与部署（linux-x64 单文件，方案 A）

```bash
./Publish/content-server.sh
```

该脚本执行 `dotnet publish -r linux-x64 --self-contained true -p:PublishSingleFile=true`，默认输出到
`Publish/ContentServer/`。产物为自包含单文件，可整体搬运：

```text
ContentServer            linux-x64 单文件可执行（含 .NET 运行时）
wwwroot/                 前端 SPA 产物（编译后的 HTML/CSS/JS）
appsettings.json         配置
Data/content-server.db   SQLite 元数据数据库（首次运行时在内容根旁创建）
Data/packages/           按 PackageHash 寻址的原始 .scpkg 制品
```

部署时拷贝上述四项即可，运行：

```bash
cd Publish/ContentServer && ./ContentServer
```

数据库 `Data/content-server.db` 只保存包元数据、内容、版本与审核记录，不保存包 BLOB。备份或迁移时必须同时保留 `Data/packages/`；
不启用裁剪以保持 ASP.NET Core / EF Core / MediatR 运行可靠性。发布机需具备 Node 与 npm 以自动构建前端，
若纯 .NET 环境可预先在 `ContentWebUI` 执行 `npm run build` 生成 `dist`。

## 游戏客户端

游戏从 `Settings.ContentServerUrl` 读取服务器地址。“在线内容”页面匿名读取目录并下载内容包：

- 所有下载先进入 `GamePaths.ContentPackageCache`；Mod 再由现有 profile 和按需加载流程管理；
- 世界、材质、皮肤和家具包通过 `ContentPackageManager` 安装到相应 `GamePaths`；
- ContentServer 下载只把原包写入统一缓存；非 Mod 安装后成为与来源脱离的本地资产，不维护来源安装记录；
- 下架只阻止后续查询和下载，不处理客户端已经下载的内容。
