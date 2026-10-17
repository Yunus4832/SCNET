# ContentServer

ContentServer 是 SCNET 的独立 ASP.NET Core 内容服务，项目位于 `ContentServer/`，并注册在
`SCNET.slnx` 的 `09 ContentServer` 逻辑文件夹中。它负责发布者申请、内容版本审核、匿名目录查询、包下载，
以及服务器源地址的登记与审核、管理员维护的游戏公告和版本目录；
玩家身份、联机身份和平台 FilePicker 不属于该服务。

ContentServer 是统一的远程内容协议和部署单元，但一个客户端可以配置多个独立部署作为内容仓库。持久仓库集合保存在实例的
`ContentRepositories.xml` 中；ModProfile 不保存来源。联机服务器还可以在握手中声明只对本次连接有效的匿名临时候选仓库。所有部署使用相同的
公共目录、版本历史、精确 Mod 查询和 `/api/v1/packages/{sha256}` 流式下载协议，下载结果进入统一
ContentPackageCache。仓库中不再包含独立的模组服务或另一套模组存储协议。

客户端每个实例可以从已启用的持久内容仓库中选择一个公告与版本来源。该选择属于实例配置，不是内容仓库自身的属性。新实例不预设来源；临时联机仓库不能成为公告与版本来源。禁用或删除当前来源会清除选择，不自动切换到其他仓库。

## 游戏公告与版本目录

管理员工作台的“公告与游戏版本”页面管理两类独立于 `.scpkg` 内容包的数据。公告草稿可编辑，发布后通过 `GET /api/v1/announcements` 匿名读取，撤回后不再出现在公开列表。每个版本草稿保存统一版本号和更新说明，并包含多个平台安装包的外部下载地址及 SHA-256；发布后通过 `GET /api/v1/game-releases?platform=...` 查询列表，通过 `GET /api/v1/game-releases/latest/{platform}` 查询最新版本，撤回后不再公开。平台标识为 `windows-x64`、`linux-x64`、`android-arm64`、`android-arm32`。ContentServer 不托管游戏安装包，也不会在记录版本时访问下载地址。

游戏 GUI 启动后异步从选定来源获取公告与适用平台的最新版本。未选择来源、请求超时或服务不可用均不阻止进入游戏；失败只写日志。主菜单滚动展示公告标题，点击后展开公告列表；点击版本号手动检查更新。桌面端下载完整便携 ZIP 并校验 SHA-256，经用户确认后由平台 Starter 准备、退出并更新，完成后由用户重新启动；无法自动安装时可打开原下载链接手动替换。桌面安装包格式见 [桌面更新包](./BuildAndConfig.md#桌面更新包)，进程交接与恢复见 [桌面程序更新](./StartupSessions.md#桌面程序更新)。Android 下载 APK 后校验 SHA-256，再交给系统安装器，由用户在系统界面确认。该哈希只验证文件与所选内容服务器记录一致，不证明服务器身份。

公告和版本查询有覆盖响应正文的 30 秒总时限；更新包下载、正文读取和校验共用 10 分钟总时限，用户可取消。
同一时间只保留一个更新流程，范围包含检查、提示、下载和安装确认；重复手动检查并入正在运行的检查。
修改公告与版本来源、编辑其地址或禁用/删除该来源时，旧更新请求取消，更新提示关闭，未交给 Starter 的下载文件清理。
切换后再切回同一来源也不会恢复旧结果。响应版本、平台、发布状态、下载地址和 SHA-256 验证通过后才比较版本；
手动检查分别提示尚无发布版本、已是最新或检查失败。Android 自动更新只支持 Arm32/Arm64，x86/x64 明确提示不支持。

## 分层与依赖方向

ContentServer 保持为一个 Web 项目，但源码按 DDD 职责分为四个命名空间：

```text
Endpoints -> Application -> Domain
                   |
                   v
              Infrastructure
```

- `ContentServer.Domain`：Publisher、Content、Administrator 聚合、强类型 ID、值状态和领域事件；聚合继承 NetCorePal `Entity<TStronglyTypedId>` 并实现 `IAggregateRoot`。
- `ContentServer.Application.Commands`：每个文件包含一个 Command 及其对应的 CommandHandler，写入使用 `ICommand`/`ICommandHandler`。
- `ContentServer.Application.Queries`：每个文件包含一个 Query 及其对应的 QueryHandler，读取使用 `IQuery`/`IQueryHandler`。
- `ContentServer.Domain.Reviews`：独立的审核记录域；Publisher 和 Content 聚合只发布审核结果领域事件，
  对应事件处理器发送 `CreateReviewRecordCommand` 创建审核记录。
- `ContentServer.Domain.Packages`：独立的包存储域。上传 Endpoint 完成大小和表单校验后，
  `SubmitContentPackageCommand` 按 SHA-256 去重并持久化包记录；Content 聚合只引用包 ID，不持有包数据。
- `ContentServer.Infrastructure`：NetCorePal EF Core 聚合仓储、`AppDbContextBase`、实体映射、SQLite、数据库初始化和迁移。
- `ContentServer.Endpoints`：每个 HTTP 操作使用独立的 FastEndpoints 类型，按 Admin、Public、Publisher
  等受众及 Contents、Servers、Packages 等领域组织命名空间。请求与响应模型仍定义在端点文件中。
  端点负责表单解析、响应映射以及发送命令和查询；Application 层不引用 HTTP 契约。

Endpoint 将 HTTP Request 映射为 Command 或 Query，并将 Application DTO 映射为 HTTP Response。
Command 和 Query 只返回 Application DTO，不返回领域实体、`ResponseData` 或其他 HTTP 类型；
API 响应外壳由 Endpoint 调用 `AsResponseData()` 创建。跨领域命令由领域事件处理器发送。

端点使用明确的响应 DTO 和 NetCorePal `ResponseData<T>`，分页业务列表使用 `PagedData<T>`。
二进制响应和服务器源协议保持各自契约；完整响应边界见 [HTTP API](ContentServerApi.md#认证与响应)。

业务代码不捕获异常以拼装 HTTP 错误。可预期的请求、认证和业务错误抛出 `KnownException`，状态码写入
`ErrorCode`；`GlobalExceptionHandler` 在 HTTP 边界统一转换响应。EF 唯一约束冲突统一返回 409，无法识别的异常
记录完整服务端日志并只向客户端返回 `internal_server_error` 和 500，避免泄露内部信息。模型绑定错误也使用同一
响应结构。无返回数据的成功操作返回 HTTP 200 和不带 `data` 的 `ResponseData`；包下载成功时仍直接返回文件流。

API Key 认证由 `Middlewares/ApiKeyAuthenticationMiddleware` 在进入 Endpoint 前统一处理。Middleware 把已认证的
Publisher 或 Administrator ID 写入 scoped `ApiKeyAuthenticationContext`；该上下文只保存强类型身份 ID，不保存
由 EF Core 跟踪的领域实体。需要认证的 Endpoint 通过构造函数显式
依赖该上下文，不从 `HttpContext` 隐式查找身份。无需认证的发布者申请使用独立的端点。
Key 的随机生成、显示前缀和 SHA-256 计算集中在 `Utils/ApiKeyUtility`，不与 HTTP
或数据库查询混合。
管理员 Key 有效但身份尚未 Active 时，身份状态接口仍允许访问，管理操作返回 HTTP 403；只有 Key 不存在或已撤销时
才返回 HTTP 401。WebUI 仅在 401 时将本地 Key 标记为失效并退回工作台入口，不能把权限不足误判为凭证失效。

ContentServer 不提供登录或服务端会话。Web UI 在浏览器 `localStorage` 中保存凭据及当前身份，
每次请求显式发送 Bearer Key；退出当前身份和移除已保存凭据是不同操作。游戏匿名浏览和下载
不需要发布者或管理员 Key。
公共内容、发布者申请、首次初始化、工作台入口、发布者工作台和管理员工作台使用独立页面。
管理员审核页的分类统计包含全部申请；各分类依次展示待审核列表和已审核历史，分别分页。历史显示审核结果及审核意见，不再提供批准或拒绝操作。

命令由 `AddUnitOfWorkBehaviors` 自动开启事务并在处理器成功后保存工作单元，因此命令处理器不得自行调用
`SaveChangesAsync`。聚合的写入通过 `RepositoryBase` 仓储完成；认证使用时间和启动初始化不属于命令流程，可以显式保存。
内容上传 Endpoint 先发送 `FindContentItemQuery` 和 `GetContentVersionQuery` 检查标识所有权、类型和重复版本，
再发送 `SubmitContentPackageCommand`。包文件暂存、提交及查询编排留在上传 Endpoint，
不另设代发命令的 Service。工作单元提交后，Endpoint 使用发布者 ID、内容标识和版本发送
`GetContentVersionQuery`，读取包含最终强类型 ID 和包信息的
查询 DTO；版本集合的新增由 EF Core 变更检测自动持久化，不需要为子实体增加专用仓储写入 API。
WebUI 将提交入口拆分为“创建新内容”和“更新已有内容”。更新时先从发布者自己的内容中选择目标，保持类型和
`identifier` 不变，由新包清单提供名称、简短说明和详细描述，然后提交新的版本号和资源包；更新命令会在追加版本的同一事务中
保存名称、简短说明和详细描述。发布者工作台按内容聚合列出自己提交的内容，并允许发布者直接下架或恢复自己的内容；该操作不会
改变已经审核通过的版本状态。下架同时从公共列表隐藏内容并停止所有版本的匿名包下载，已有脚本直链也会返回 404；
恢复上架后原下载链接重新有效。版本记录为每个已发布版本提供可复制的匿名下载链接。

管理员工作台提供删除内容操作，与上下架分开。`DELETE /api/v1/admin/content/{contentId}`
发送内容删除命令；命令处理器调用内容聚合的 `Delete` 方法，由聚合标记自身及版本并发出删除领域事件。EF 全局查询过滤器使已删除内容和版本不再出现在业务查询中，
原下载链接随即失效。删除领域事件调用包域和审核域命令：相关审核记录软删除；没有其他有效版本引用的包记录软删除，
数据库事务成功后移除对应包文件。共用包仍保留。网页操作要求输入该内容的 Identifier 再执行删除；发布者仍只管理上下架。
若文件清理中断，服务下次启动时会清理没有有效包记录引用的文件。

所有持久化领域实体均显式定义 NetCorePal 的 `Deleted` 字段，并配置全局查询过滤器；
删除命令调用对应领域方法修改该字段，保留数据库记录。其他持久化操作不会自动软删除。
审计值使用 NetCorePal 的强类型 `Deleted`；需要读取已删除记录时，可在 `IQueryable` 上链式调用 `IncludeDeleted()`
或 `WhereDeleted(new Deleted(true))`，供删除后的事件处理等流程使用。

领域事件使用 NetCorePal 自带的 `IDomainEvent`/`IDomainEventHandler`，由 `AppDbContextBase` 协调保存与进程内发布。
审核类事件处理器发送 Reviews 域命令，在原命令的事务范围内持久化审核记录；
将来若接入邮件、对象存储或外部消息队列，应增加 Outbox 后再异步投递，不能把“数据库提交后直接发布”误当成可靠消息。

CQRS 在这里表示代码职责分离，而不是把数据拆成两套数据库：命令和查询仍使用同一个 SQLite 数据源，避免当前规模下不必要的最终一致性成本。
列表 Query 使用 NetCorePal `PageRequest` 和 `ToPagedData`，HTTP 缺省值为第一页、每页 10 条，并计算总条数。
`pageSize` 最大为 100。内容、提交、发布者和管理员列表还接受可选的 `query` 参数，使用参数化 LINQ
在名称、稳定标识、简短说明、详细描述或联系方式等适用字段上进行模糊搜索。
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

发布者、Key hash、内容元数据、审核记录和包索引保存在数据库中。原始 `.scpkg` 文件存放于
`PackageStoragePath/packages/`，按 PackageHash 命名；上传暂存位于同根目录的 `temp/`。
数据库与包目录必须作为一个持久化单元一致性备份，不能只备份数据库。

核心表：

- `Administrators` / `AdministratorKeys`
- `Publishers` / `PublisherKeys`
- `Contents` / `ContentVersions`
- `PackageBlobs`
- `ReviewRecords`
- `ServerSources` / `RendezvousServices` / `DirectoryServers`
- `GameAnnouncements` / `GameReleases` / `GameReleaseArtifacts`

数据库结构由 `Infrastructure/Migrations` 中的一份 EF Core 初始迁移和模型快照维护，服务启动时自动应用迁移。项目发布前，模型发生变化时重新生成初始迁移：

```bash
dotnet ef migrations add InitialContentServer \
  --project ContentServer/ContentServer.csproj \
  --startup-project ContentServer/ContentServer.csproj \
  --output-dir Infrastructure/Migrations
```

重新生成前删除旧迁移文件和快照。提交模型修改时必须同时提交新迁移和
`ContentServerDbContextModelSnapshot.cs`。旧开发数据库的迁移历史不再适用，需删除数据库文件后重新初始化。

## 配置

`appsettings.json`：

```json
{
  "ContentServer": {
    "DatabasePath": "Data/content-server.db",
    "PackageStoragePath": "Data",
    "MaximumPackageBytes": 268435456,
    "AllowedOrigins": ["https://content-ui.example.com"],
    "BuiltInServerDirectoryEnabled": true,
    "BuiltInServerDirectoryId": "scnet-content-server",
    "BuiltInServerDirectoryName": "SCNET Servers",
    "PublicBaseUrl": "https://content.example.com"
  }
}
```

生产环境可以通过环境变量覆盖数据库路径：

```text
ContentServer__DatabasePath
```

`AllowedOrigins` 只用于独立部署的浏览器 SPA，必须填写完整来源（协议、域名和端口），不使用通配符，
也不允许跨域凭据。匿名下载和 Bearer API Key 请求均可跨域；响应暴露 `Content-Disposition` 供前端取得文件名。
`PublicBaseUrl` 用于生成内置服务器源的公开协议地址；反向代理部署时应显式配置，避免依据内部请求地址生成 URL。
`BuiltInServerDirectoryId` 必须符合服务器源协议的稳定 ID 规则，部署后不应随显示名称或域名变化。

## 独立 WebUI

`ContentWebUI/` 是注册在 `SCNET.slnx` 的 `09 ContentServer` 逻辑文件夹中的 Vue 3 单页应用。它可以与
ContentServer 分别部署，生产构建后的 `runtime-config.json` 配置 API 地址，静态服务器需将未知路由回退到
`index.html`。WebUI 负责 API Key 的本地保存和角色导航，不改变 ContentServer 的无账户、无 Cookie 会话模型。

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

## HTTP API

接口清单、认证、初始化、分页和投稿格式见 [ContentServer HTTP API](ContentServerApi.md)。
每个端点的请求与响应定义位于对应的 `ContentServer/Endpoints/` 源文件，管理操作与公开查询分开。

## 启动

```bash
dotnet run --project ContentServer/ContentServer.csproj
```

开发环境前端使用 Vite（`ContentWebUI`）单独运行，端口由 Vite 选择（通常为 `5173`，占用时可能顺延），并将 `/api` 代理到 ContentServer
（默认 `http://localhost:5000`），改前端代码即时热更新。ContentServer 构建或发布时会通过
`ContentServer/Directory.Build.targets` 中的构建目标 自动执行前端构建，并把 `ContentWebUI/dist` 复制到 `wwwroot`，
因此调试时用 `npm run dev`，部署时只需发布 ContentServer。

## 发布与部署

ContentServer 的 portable 包、容器镜像、HTTP/HTTPS 配置、持久化、备份和升级方式见
[ContentServer 部署](ContentServerDeployment.md)。日常开发使用 `dotnet build`；只有明确创建发行物时才运行仓库发布脚本。

## 游戏客户端

游戏在实例的 `ContentRepositories.xml` 中保存有序的内容仓库集合，每个仓库包含稳定 ID、名称、规范 HTTP(S) 基础地址、启用状态和优先级。
公告与版本来源由根元素的 `GameInformationSourceId` 单独记录，它引用一个已启用的持久仓库，不是仓库自身的属性。
“内容仓库”页负责增删改、启停、排序和健康检查，不浏览包；删除或禁用仓库不会删除缓存、Profile 或安装资产。

“在线内容”页通过共享 ClientPool 并发读取所有选定仓库，按 `ContentType + Identifier` 聚合目录，提供搜索、类型、仓库和状态
筛选以及真实分页。相同版本和 PackageHash 合并来源，同版本不同 hash 保持为可见冲突；版本详情读取完整历史并显示最新、
Profile/当前会话引用、缓存、缺失、大小、来源和部分仓库失败。本地缓存只标记仓库返回条目的精确 hash，不作为在线目录来源。

- 所有下载先进入 `GamePaths.ContentPackageCache`；Mod 再由现有 profile 和按需加载流程管理；
- 默认下载按仓库顺序选择并只在同 hash 来源间回退；用户也可以显式限定一个仓库；
- Mod 下载只更新缓存，不修改 Profile；世界、材质、皮肤和家具包下载后可显式进入现有安装流程；
- ContentServer 下载只把原包写入统一缓存；非 Mod 安装后成为与来源脱离的本地资产，不维护来源安装记录；
- 下架只阻止后续查询和下载，不处理客户端已经下载的内容。

Mod 管理页不访问 ContentServer。它只合并缓存和 Profile/Runtime requirement，缺失项通过完整
`ModId + Version + PackageHash` 导航到在线内容页。启动补全使用同一个缓存优先下载服务；联机临时仓库优先于持久仓库但不被
持久化。单仓库部署只是集合中只有一项的普通配置，不存在单地址专用客户端路径。

## 信令服务登记

信令服务登记独立于普通服务器源协议和内容包。ContentServer 保存服务名称、控制地址、说明及审核状态，
不保存该服务的临时房间或联机码。社区页面的“信令服务器”板块展示已批准记录；发布者可以在弹窗中投稿或修改，
管理员可以审核、停用和删除。修改后重新进入待审核状态并从公开目录隐藏，批准后重新公开。

- 公开目录：`GET /api/v1/rendezvous-services`。
- 发布者列表及投稿：`GET/POST /api/v1/publisher/rendezvous-services`。
- 发布者修改：`POST /api/v1/publisher/rendezvous-services/{id}/edit`；删除：`DELETE /api/v1/publisher/rendezvous-services/{id}`。
- 管理员列表：`GET /api/v1/admin/rendezvous-services`，可使用 `status=Pending`。
- 管理员审核：`POST /api/v1/admin/rendezvous-services/{id}/approve` 或 `/reject`；拒绝请求提供 `message`。
- 管理员停用：`POST /api/v1/admin/rendezvous-services/{id}/disable`；删除：`DELETE /api/v1/admin/rendezvous-services/{id}`。

投稿和修改包含 `name`、`controlUrl`、可选 `description`。公开控制地址必须使用 HTTPS；服务描述须支持 SCNET
公开房间发现。HTTP 探测有时间和响应大小限制，禁止自动重定向和连接非公共地址。审核事件在事务中记录审核结果，
删除设置登记的软删除字段。服务目录中的“已审核”不代表官方认证，也不保证服务始终可访问。

### 公开信令服务目录分页

`GET /api/v1/rendezvous-services` 使用现有 `pageIndex`（从1开始）、`pageSize`（最多100）查询参数，响应数据为 `items`、`total`、`pageIndex`、`pageSize`。公开查询只返回有效已批准登记；发布者和管理员的管理列表仍按其场景返回记录。游戏及社区页面逐页读取目录，每次读取最多100页；房间的临时目录仍使用独立信令协议游标，不与此目录合并。

## 游戏发行版本规则

游戏发行记录使用三段语义化版本，可带预发布标识（如 `0.1.0-alpha.1`），不接受四段式版本。
公开列表及最新版本查询按语义化优先级排序；安装包链接指向该版本真实产物，SHA-256 应与
对应下载文件一致。产品发行版本不携带构建元数据，提交哈希只用于程序集诊断信息。
