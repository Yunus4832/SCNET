# ContentWebUI

ContentWebUI 是 ContentServer 的独立 Vue 3 单页应用，提供匿名内容市场、首次管理员初始化、发布者申请与工作台、管理员申请与审核台。
API Key 只保存在当前浏览器标签页的 `sessionStorage`，服务器不创建登录会话。

## 本地开发

```bash
npm install
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
