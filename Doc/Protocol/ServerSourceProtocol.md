# 服务器源协议

服务器源是一个可安装的 HTTP JSON 目录，用于向 SCNET 客户端提供游戏服务器条目。
它与 `.scpkg` 内容制品、ContentServer 中的服务器源登记，以及客户端中的安装配置具有独立生命周期。

`SCNET.ServerSource.Protocol` 是协议的 .NET 参考实现，提供传输契约、结构验证和有界 HTTP 客户端。
其 JSON Schema 位于 `ServerSource.Protocol/Schemas/server-source-v1.schema.json`，非 .NET 实现应以本文档和 Schema 为依据。

## 请求

安装配置保存完整的服务器源 API URL。客户端通过 `GET` 请求该 URL，并附加：

- `limit`：本页最大条目数，范围为 1–100。
- `cursor`：可选的不透明分页游标。

如果已安装来源的 URL 已有查询参数，协议参数追加在现有参数之后。服务器应返回
`application/json` 或以 `+json` 结尾的媒体类型。

## 响应

```json
{
  "protocolVersion": 1,
  "source": {
    "id": "community-source",
    "name": "Community Servers"
  },
  "servers": [
    {
      "id": "server-001",
      "name": "Example Server",
      "address": "play.example.org:28887",
      "description": "Survival server",
      "tags": ["survival"]
    }
  ],
  "nextCursor": null
}
```

`protocolVersion` 必须为 `1`。`source.id` 是来源的稳定身份，不应因名称或 URL 变更而改变。
`servers[].id` 只需在该来源内稳定且唯一。相同地址在不同来源中是独立条目，客户端不进行跨来源聚合。

`address` 必须包含显式端口，例如 `example.org:28887`、`127.0.0.1:28887` 或
`[2001:db8::1]:28887`。响应不携带延迟、在线人数、游戏模式、世界时间或所需 Mod；这些是由游戏协议实时探测的短期状态。

`nextCursor` 为 `null` 表示没有后续页。来源只能返回游标，不能为下一页指定新 URL。
参考客户端最多读取 100 页，并拒绝跨页重复的条目 ID、变化的来源 ID 和重复游标。

## 客户端来源模型

Survivalcraft 将“我的服务器”“收藏”“最近使用”“局域网”和每一个已安装 HTTP 来源视为独立来源。
列表项身份由客户端来源 ID 与来源内条目 ID 共同组成；即使地址相同也不会跨来源合并。
在线状态、延迟和世界信息只保存在当前刷新周期；客户端发起连接时写入“最近使用”。收藏是收藏来源中的
独立本地条目，不反向关联原始来源，并按规范化后的地址与端口去重。收藏与最近使用条目均可从各自本地来源删除。

## ContentServer 参考实现

ContentServer 内置实现本协议，并通过 `/api/v1/server-directory` 提供服务器列表。服务器投稿与编辑、发布者上下架、
管理员审核、编辑和停用属于 ContentServer 的管理 API，不属于服务器源协议。第三方服务器源可以自行决定条目如何进入
目录；不希望自行实现认证、审核和管理能力的部署者可以直接运行 ContentServer。

已安装服务器源存放在 Settings 的 `ServerDirectory/InstalledSources` 节点中，包含稳定本地 ID、可选
ContentServer 登记 ID、显示名称、API URL、启用状态和顺序。NetPlay 只读取已启用来源；安装、禁用、排序和删除由内容页面中的
服务器源管理 Screen 负责。

## 限制

- 单页最多 100 个服务器条目。
- 单个响应最多 1 MiB。
- 来源 ID 最长 64 个字符，条目 ID 最长 128 个字符。
- 名称最长 100 个字符，描述最长 1024 个字符。
- 每个条目最多 16 个标签，每个标签最长 32 个字符。
- 游标最长 512 个字符。
- ID 只能由 ASCII 字母、数字、点、下划线和连字符组成，且首字符必须是字母或数字。

## 安全边界

协议层校验 HTTP 结构和响应内容，但不决定某个 URL 是否允许访问。Survivalcraft 可请求用户主动安装的来源；
ContentServer 验证外部提交时必须额外阻止 loopback、私有地址、链路本地地址、保留地址和重定向到内网的请求。

来源返回的名称、描述和标签都是不可信文本。客户端必须依赖协议限制，不得将这些字段解释为格式化指令、路径、请求头或代码。
