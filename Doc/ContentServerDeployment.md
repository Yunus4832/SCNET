# ContentServer 部署

本文说明 ContentServer 发行物、容器配置、持久化和升级。服务端领域模型、API 与客户端行为见
[ContentServer](ContentServer.md)，统一发布流程见[构建与共享配置](BuildAndConfig.md)。

## 创建发行物

日常开发和验证使用 `dotnet build`，不会更新 `Publish/`。需要发行物时，从仓库根目录运行统一发布脚本：

```bash
./Scripts/publish.sh --project ContentServer
```

```powershell
./Scripts/publish.ps1 -Project ContentServer
```

发布生成 framework-dependent portable ZIP；Linux 还会生成包含 .NET 10 Chiseled 运行镜像和部署文件的 bundle：

```text
Publish/content-server-<version>-portable.zip
Publish/content-server-<version>-linux-amd64.tar.gz
```

portable 包不携带 .NET，目标机器需要 ASP.NET Core Runtime 10。Linux 镜像构建需要 Podman 或 Docker，
默认优先使用 Podman；可以通过 `CONTAINER_ENGINE=docker` 显式选择。镜像基础层为
`mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled`，默认只使用本地已有镜像；首次构建前需要主动拉取：

```bash
podman pull mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled
```

需要允许构建过程拉取缺失基础镜像时使用：

```bash
IMAGE_PULL_POLICY=missing ./Scripts/publish.sh --project ContentServer
```

可以通过 `IMAGE_NAME`、`IMAGE_TAG`、`PLATFORM` 和发行输出属性覆盖镜像与输出设置。Git revision
记录在 OCI Label 中；工作区不干净时 revision 带 `-dirty`，不会改变发行版本标签。

`ContentServer/Deployment/build-image.sh` 只组装项目发布流程提供的 Linux 输出，不是独立发行入口。

## 部署 bundle

Linux bundle 包含 Docker archive、归档 SHA-256、`compose.yaml`、`Caddyfile` 和 `deploy.sh`。
可以通过任意文件传输方式复制到目标主机；部署不需要源码或 Git 仓库。例如：

```bash
scp Publish/content-server-<version>-linux-amd64.tar.gz server:/srv/content-server/
ssh server
cd /srv/content-server
tar -xzf content-server-<version>-linux-amd64.tar.gz
./deploy.sh
```

`deploy.sh` 自动探测 Podman 或 Docker，也可以通过 `CONTAINER_ENGINE` 指定。无参数时交互选择镜像导入、
HTTP/HTTPS 和是否启动。无人值守的默认 HTTP 部署使用：

```bash
./deploy.sh --default
```

可用选项为 `--import`、`--skip-import`、`--http`、`--https`、`--no-start` 和 `--default`。
stdin 不可用且参数不足时脚本会失败，不会隐式选择。

## HTTP

HTTP 模式默认将宿主 `5000` 映射到容器 `8080`：

```bash
mkdir -p Data
docker compose -f compose.yaml up -d content-server
curl http://127.0.0.1:5000/api/v1/health
```

使用 `CONTENTSERVER_PORT` 修改宿主端口，使用 `CONTENTSERVER_BIND_ADDRESS` 修改绑定地址。匿名浏览和下载
可以使用 HTTP；发布者和管理员 Key 不应经过不可信网络传输。

## HTTPS

HTTPS 模式启用 bundle 中的 Caddy 服务：

```bash
mkdir -p Data CaddyData CaddyConfig
CONTENTSERVER_HOSTNAME=content.example.test \
  CONTENTSERVER_BIND_ADDRESS=127.0.0.1 \
  ./deploy.sh --import --https
curl --cacert caddy-root.crt https://content.example.test/api/v1/health
```

Caddy 默认监听宿主 `80` 和 `443`，通过容器网络代理到 ContentServer。可以使用
`CONTENTSERVER_HTTP_PORT`、`CONTENTSERVER_HTTPS_PORT` 和 `CONTENTSERVER_HOSTNAME` 覆盖入口配置。
`deploy.sh` 会将 Caddy 内部 CA 根证书复制为 `caddy-root.crt`；访问设备需要信任该证书并将所选域名解析到
部署主机。`CaddyData` 和 `CaddyConfig` 必须持久化，否则重新生成 CA 后客户端需要重新建立信任。

公网部署应为可公开解析的域名配置受信任证书，并按部署环境调整 Caddyfile，不能继续依赖内部 CA。
TLS 在 Caddy 终止，ContentServer 应用和镜像不直接管理证书。

## 数据与运行边界

ContentServer 数据目录默认挂载为 `/data`，容器内固定配置为：

```text
ContentServer__DatabasePath=/data/content-server.db
ContentServer__PackageStoragePath=/data
```

使用 `CONTENTSERVER_DATA_DIR` 可以覆盖宿主数据目录。SQLite 数据库与 `Data/packages/` 必须作为同一个
持久化单元备份和恢复。当前存储模型只支持单副本部署，不得让多个容器或 Pod 并发写入同一份数据。

Chiseled 镜像没有 shell、curl 或包管理器。健康检查应由宿主、反向代理或集群请求
`/api/v1/health`。当前镜像不固定运行 UID；部署环境增加非 root 限制时，需要同时保证挂载目录权限正确。

## 升级

部署新版本时，传输并解压对应版本 bundle，再执行其中的 `deploy.sh`。bundle 的 `compose.yaml` 已写入精确
镜像引用；不要依赖隐式 `latest` 标签。升级前备份数据库和包目录，启动后通过健康检查确认服务可用。
