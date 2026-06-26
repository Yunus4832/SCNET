# Mod Server

`ModServer` 是一个最小的私有模组仓库服务，目标是让个人或小团队可以直接部署，用 HTTP 上传和分发 `.scpak`，不引入数据库和复杂的权限系统。

## 设计边界

- 上传需要认证
- 下载不需要认证
- 数据存储在本地文件系统
- 元数据存储在 `index.json`
- API 版本固定为 `v1`
- 支持按 `modId + version` 覆盖和删除
- 不支持签名校验、依赖解析和复杂权限模型

这不是 NuGet 或 Maven 的完整替代品，只是一个简单、可部署、可维护的私有模组源。

## 项目位置

- 项目文件：`ModServer/ModServer.csproj`
- 配置文件：`ModServer/appsettings.json`

## 配置

`appsettings.json`:

```json
{
  "ModServer": {
    "DataDirectory": "Data",
    "UploadApiKeys": [ "change-me" ]
  }
}
```

- `DataDirectory`: 仓库数据目录
- `UploadApiKeys`: 允许上传的 API Key 列表

认证支持两种头：

- `Authorization: Bearer <key>`
- `X-Api-Key: <key>`

## 存储结构

服务运行后会在数据目录下创建：

```text
Data/
  index.json
  packages/
    <sha256>.scpak
```

- `index.json` 保存所有已上传包的元数据
- 包文件按内容 `SHA-256` 命名，避免重复存储

## API

### 健康检查

`GET /api/v1/health`

### 列出所有模组

`GET /api/v1/mods`

### 查询某个模组

`GET /api/v1/mods/{modId}`

### 查询某个模组版本

`GET /api/v1/mods/{modId}/versions/{version}`

### 下载包

`GET /api/v1/packages/{packageHash}`

### 上传包

`POST /api/v1/mods/upload`

请求类型：`multipart/form-data`

字段：

- `package`: `.scpak` 文件，必填
- `description`: 描述，可选
- `replace`: 是否覆盖同 `modId + version` 的不同包，可放在表单或 query string 中

`modId`、`version` 和 `side` 从包内 `manifest.json` 读取。`side` 可为 `common`、`client` 或 `server`，为空时按 `common` 处理。

## 上传行为

- 同一个 `modId + version`
  - 如果包哈希相同：视为幂等上传
  - 如果包哈希不同：返回 `409 Conflict`
- 使用 `replace=true` 时，同版本不同内容会覆盖旧记录

这样可以避免仓库被无意覆盖。

### 删除包版本

`DELETE /api/v1/mods/{modId}/versions/{version}`

需要上传认证。删除后，如果旧包文件不再被其他版本引用，会同步删除包文件。

## 运行

```bash
dotnet run --project ModServer/ModServer.csproj
```

## 后续可扩展点

- 增加废弃标记
- 增加依赖字段
- 增加签名校验
- 增加只读镜像同步
- 增加前端管理页面
