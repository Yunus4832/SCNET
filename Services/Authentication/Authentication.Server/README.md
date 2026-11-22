# SCNET Authentication Server

外部认证协议的单进程参考实现。它使用部署者预先配置的账号，处理浏览器登录、签名批准、可复用授权和认证级封禁。
游戏服务器根据批准继续处理准入，角色与存档权限由游戏服务器管理。见[外部准入协议](../../../Doc/Protocol/AuthenticationProtocol.md)。

选择[便携部署](#便携部署)或 [Linux 容器部署](#linux-容器部署)。公开入口通过可信 HTTPS 反向代理提供。

## 配置

使用标准 ASP.NET 配置或环境变量：

- `Authentication__PublicUrl`：客户端看到的 HTTPS 基地址，以 `/` 结尾；反向代理若使用子路径需剥离该前缀后转发。
- `Authentication__DisplayName`：公开显示名。
- `Authentication__DataDirectory`：持久化目录，便携运行默认为可执行文件目录下的 `AuthenticationData`，启动时自动创建。容器默认为 `/home/app/AuthenticationData`；部署者需保证父目录可写，并持久挂载数据。
- `Authentication__Accounts__账号名`：ASP.NET Identity 兼容密码哈希；不要配置明文密码。
- `Authentication__TrustedProxies__索引`：允许提供客户端地址/协议的直接反向代理 IP，默认只信任 `127.0.0.1` 与 `::1`。只处理一跳，不接受空信任列表；代理链与身份认证无关。

运行 `dotnet run --project Services/Authentication/Authentication.Server -- --hash-password`，从标准输入提供至少十二字符的密码，得到可写入配置的哈希。不要把明文密码放入命令行历史或日志。服务监听地址使用标准 `ASPNETCORE_URLS` 配置，与公开 HTTPS 地址分开。

首次运行创建独立 P-256 身份；已有授权状态但密钥丢失时拒绝启动，不自动替换身份。私钥和授权状态原子写入私有文件；grant 只持久化哈希，事务和原始 grant 的短期领取状态仅驻留内存。事务在进程重启后失效，未到期的长期 grant 可以继续使用。目录锁拒绝并发写入同一数据目录。

## 封禁与网页

停止服务后，在相同配置及持久目录下执行：

```text
dotnet run --project Services/Authentication/Authentication.Server -- --ban-account 账号名
dotnet run --project Services/Authentication/Authentication.Server -- --unban-account 账号名
```

再启动服务。封禁阻止后续网页批准和已有授权的使用，当前游戏会话由对应服务器管理。修改或移除密码哈希无法撤销已签发的授权。解封后，仍有效且未被撤销的授权可继续使用。

浏览器事务秘密放在 URL fragment，页面读入后清除；它不会进入 HTTP 访问路径。网页要求再次确认被授权的完整用户地址。请求、轮询及网页登录均有限流；响应禁止缓存，页面限制脚本、表单来源和引用传播。不要额外开启请求正文记录，以免记录密码或 grant。

## 便携部署

便携包需要 ASP.NET Core 10 Runtime，通过 `dotnet Authentication.Server.dll` 启动。发行版本取自 `SharedProperties/Versions.props` 的 `AuthenticationServerVersion`。
开发使用 `dotnet build`；需要生成发行物时，统一使用 `bash Scripts/publish.sh --project Authentication.Server`
或 `pwsh Scripts/publish.ps1 -Project Authentication.Server`。输出为 `Publish/authentication-server-<版本>-portable.zip`；
Linux 同时生成容器 bundle。普通构建不生成 `Publish` 发行物。

解压后将 `Deployment/provider.json.example` 复制为根目录 `appsettings.Production.json`，配置实际公开 HTTPS 地址、
独立私有数据目录及账号哈希。Unix 限制配置文件为 `0600`，数据目录只允许运行账号访问。不要修改仓库中的
`appsettings.json` 来准备发行配置；发布会排除本机配置、私钥和授权状态。
从解压根目录运行 `bash Deployment/run.sh`；Windows 使用 `pwsh Deployment/run.ps1`，也可直接运行 `dotnet Authentication.Server.dll`。
标准 `ASPNETCORE_URLS` 控制内部监听；反向代理保持公开 URL 对应的路径，并在子路径部署时剥离前缀。
程序使用配置中的公开 URL 构造和验证协议目标，不信任客户端 Host 头来选择签名目标。

上述启动脚本也可转发密码哈希与离线封禁参数。离线操作须使用服务运行账号及相同工作目录、配置和数据目录。

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

容器生成使用现有 Podman/Docker 工作流，默认不拉取基础镜像；依赖与覆盖变量见 [构建文档](../../../Doc/BuildAndConfig.md)。
