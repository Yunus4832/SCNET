# SCNET Authentication Server

外部认证协议的独立参考实现。提供签名事务、浏览器账号登录、轮询、取消、长期 grant、撤销及认证级账号封禁。它不管理游戏角色、服务器密码、游戏在线状态，也不要求游戏服务器注册。

当前为单进程文件存储实现，公开部署应使用 HTTPS 反向代理。账号由部署者预先配置，不开放匿名注册；其他认证服务可以保留相同协议，自行实现 Google、邮箱等网页流程。游戏已提供三种准入、认证服务管理及 ContentServer 目录安装；协议与部署约定见 [认证协议](../Doc/Protocol/AuthenticationProtocol.md)。

## 配置

使用标准 ASP.NET 配置或环境变量：

- `Authentication__PublicUrl`：客户端看到的 HTTPS 基地址，以 `/` 结尾；反向代理若使用子路径需剥离该前缀后转发。
- `Authentication__DisplayName`：公开显示名。
- `Authentication__DataDirectory`：持久化目录，便携运行默认为可执行文件目录下的 `AuthenticationData`，启动时自动创建。容器默认为 `/home/app/AuthenticationData`；部署者需保证父目录可写，并持久挂载数据。
- `Authentication__Accounts__账号名`：ASP.NET Identity 兼容密码哈希；不要配置明文密码。
- `Authentication__TrustedProxies__索引`：允许提供客户端地址/协议的直接反向代理 IP，默认只信任 `127.0.0.1` 与 `::1`。只处理一跳，不接受空信任列表；代理链与身份认证无关。

运行 `dotnet run --project Authentication.Server -- --hash-password`，从标准输入提供至少十二字符的密码，得到可写入配置的哈希。不要把明文密码放入命令行历史或日志。服务监听地址使用标准 `ASPNETCORE_URLS` 配置，与公开 HTTPS 地址分开。

首次运行创建独立 P-256 身份；已有授权状态但密钥丢失时拒绝启动，不自动替换身份。私钥和授权状态原子写入私有文件；grant 只持久化哈希，事务和原始 grant 的短期领取状态仅驻留内存。事务在进程重启后失效，未到期的长期 grant 可以继续使用。目录锁拒绝并发写入同一数据目录。

## 封禁与网页

停止服务后，在相同配置及持久目录下执行：

```text
dotnet run --project Authentication.Server -- --ban-account 账号名
dotnet run --project Authentication.Server -- --unban-account 账号名
```

再启动服务。封禁阻止该账号的后续网页批准和缓存 grant 使用，不推送踢出已经进入的游戏会话；存档级封禁仍由各游戏服务器独立管理。

浏览器事务秘密放在 URL fragment，页面读入后清除；它不会进入 HTTP 访问路径。网页要求再次确认被授权的完整用户地址。请求、轮询及网页登录均有限流；响应禁止缓存，页面限制脚本、表单来源和引用传播。不要额外开启请求正文记录，以免记录密码或 grant。

本实现没有自动扩展、跨进程事务或独立密钥轮换。游戏身份私钥仍只由用户设备保管。


## 便携部署

新服务发行单元初始版本为 `0.1.0`，依赖 ASP.NET Core 10 运行时，无平台 apphost。
开发使用 `dotnet build`；需要生成发行物时，统一使用 `bash Scripts/publish.sh --project Authentication.Server`
或 `pwsh Scripts/publish.ps1 -Project Authentication.Server`。输出为 `Publish/authentication-server-<版本>-portable.zip`；
Linux 同时生成容器 bundle。普通构建不生成 `Publish` 发行物。

解压后将 `Deployment/provider.json.example` 复制为根目录 `appsettings.Production.json`，配置实际公开 HTTPS 地址、
独立私有数据目录及账号哈希。Unix 限制配置文件为 `0600`，数据目录只允许运行账号访问。不要修改仓库中的
`appsettings.json` 来准备发行配置；发布会排除本机配置、私钥和授权状态。
从解压根目录运行 `bash Deployment/run.sh`；Windows 使用 `pwsh Deployment/run.ps1`，也可直接运行 `dotnet Authentication.Server.dll`。
标准 `ASPNETCORE_URLS` 控制内部监听；反向代理保持公开 URL 对应的路径，并在子路径部署时剥离前缀。
程序使用配置中的公开 URL 构造和验证协议目标，不信任客户端 Host 头来选择签名目标。

生成哈希和离线封禁也可通过上述 launcher 传入 `--hash-password`、`--ban-account NAME`、`--unban-account NAME`。
前者从标准输入读取一行密码；不要在 shell 参数或操作日志中写明文。封禁命令须在服务停止后运行，以相同用户、
工作目录和配置读取同一数据目录。改变账号密码哈希或移除账号配置不会自动撤销已经签发的 grant；需要阻止已有授权时先封禁账号。
封禁是准入状态限制，解封后未撤销且未到期的旧 grant 可再次使用。

## Linux 容器部署

需要 Podman Compose 或 Docker Compose。解压 `authentication-server-<版本>-linux-amd64.tar.gz` 后执行 `bash deploy.sh --no-start` 导入并校验镜像。
将 `provider.env.example` 复制为 `provider.env` 并限制为 `0600`，填写公开 URL、账号哈希及受信直接代理 IP；
`server.conf` 从模板配置 loopback 监听端口和持久卷名称。然后执行 `bash deploy.sh --skip-import`。
首次直接启动而没有 `provider.env` 时，脚本只创建模板并退出，不运行未配置服务。

容器默认以镜像的非 root 用户运行。独立命名卷挂载到基础镜像中归属运行用户的 `/home/app`；
程序在首次启动时创建卷内的 `AuthenticationData` 子目录并限制为私有访问，随后保存密钥和授权状态。
镜像不包含占位数据目录，不依赖 shell、`mkdir` 或 root 初始化进程。自定义绑定挂载时，部署者须保证父目录可由容器用户写入；
账号哈希通过受保护 env 文件传入，不需要把主机私有配置文件开放给其他用户。
每个独立认证方使用不同卷名；升级时保留原卷名、`provider.env` 和 `server.conf`。不要运行删除卷的清理命令。
备份/恢复整个数据卷及配置，保持运行用户可读写；仅复制授权状态而丢失身份私钥会使启动失败。
普通应用更新包不包含这些业务文件，镜像组装和发行检查也拒绝夹带它们。
容器停机后可用 `podman compose run --rm authentication-server --ban-account NAME` 执行离线封禁；
Docker 使用相同的 `docker compose run` 形式，保持原 env 文件和数据卷。

内置 HTTP 端口只映射到主机 `127.0.0.1:31992`，对外由可信 HTTPS 反向代理提供服务。
容器桥接网络下直接代理可能显示为网关地址，需配置实际、精确的代理 IP；不要把任意客户端声明的转发头当作可信输入。
默认全局限流为每源 IP 每分钟 180 次，网页入口另为 15 次；受信代理先恢复客户端 IP，再应用限流。
缺少正确代理配置时多个客户端会共享代理计数。已有 grant 有效期为参考实现的 30 天，认证方可在自己的实现中制定其他策略。

容器生成使用现有 Podman/Docker 工作流，默认不拉取基础镜像；依赖与覆盖变量见 [构建文档](../Doc/BuildAndConfig.md)。
