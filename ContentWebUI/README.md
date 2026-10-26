# ContentWebUI

ContentWebUI 是 ContentServer 的独立 Vue 3 单页应用，提供匿名内容市场、首次管理员初始化、发布者申请与工作台、管理员申请与审核台。
API Key 与当前选择的身份保存在浏览器 `localStorage`；服务器不创建登录会话。
仅在可信设备上保存 Key，离开共享设备前从工作台移除凭据。

## 本地开发

在 `ContentWebUI/` 目录执行（首次运行先安装依赖）：

```bash
npm ci
npm run dev
```

开发服务器把同源 `/api` 请求代理到 `http://localhost:5000`。后端使用其他地址时：

```bash
CONTENT_SERVER_URL=http://localhost:5088 npm run dev
```

生产构建使用 `npm run build`，静态文件输出到 `dist/`，可以部署到任意静态文件服务器。服务器需要把 SPA 的未知路径回退到 `index.html`。

## 运行时配置

部署后修改 `dist/runtime-config.json` 即可切换后端，不需要重新构建：

```json
{
  "apiBaseUrl": "https://content.example.com"
}
```

仓库中的默认值为空，表示通过当前 WebUI 来源同源访问 `/api`，适合反向代理部署。独立域名部署时必须填写
浏览器可以访问的 ContentServer 公网地址；不要使用只对服务器自身有效的 `localhost`。

若 WebUI 与 ContentServer 不同源，需要在 ContentServer 的 `AllowedOrigins` 中精确列出 WebUI 来源。


## 认证服务目录

社区首页、发布者工作台、管理员审核及管理页均提供独立的认证服务板块。公开列表只展示服务来源地址和目录资料；
投稿只填写 HTTPS 基址和说明，身份由游戏客户端直接发现并确认。修改会下架重审，审核、撤回、停用、删除均携带所见修订号，
冲突后需要刷新核对，不自动覆盖或重试批准。管理员可在操作历史中核对历史来源地址与操作人，按记录 ID 查询已删除条目。
页面不保存玩家私钥、认证授权或游戏服务器密码。详细 API 约定见 [ContentServer](../Doc/ContentServer.md#认证服务目录)。
