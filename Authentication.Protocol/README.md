# SCNET.Authentication.Protocol

独立的外部认证协议契约、签名验证和 HTTPS 客户端。它只依赖 Engine.Core，不依赖游戏、角色、UDP 或 Engine.Network；游戏服务器只需固定认证方公钥即可本地验证连接批准。

当前协议版本为 `1`。游戏准入、客户端服务管理和目录已接入该组件；完整线格式、限值及集成要求见 [认证协议](../Doc/Protocol/AuthenticationProtocol.md)。

## 身份与签名

- 用户、游戏服务器和认证方使用 P-256 公钥。地址为 RFC 7638 JWK 指纹的完整 SHA-256 值，经 SCNET Bech32m 编码；与传输层既有地址一致。
- 使用 Microsoft IdentityModel 的 JWS/JWT 实现，固定 ES256，拒绝其他算法、额外密钥来源、重复 JSON 字段、多 audience、非数值时间及非规范 base64url 编码。
- 用户请求的 `typ` 为 `scnet-auth-request+jwt`，批准为 `scnet-admission+jwt`，两者不能互换。
- 公钥发现不建立信任。管理员和客户端需独立确认认证方指纹；已固定的公钥不会被描述响应自动覆盖。

## HTTP 入口

服务描述的 `serviceUrl` 必须为以 `/` 结尾的 HTTPS 基地址，允许部署在反向代理子路径下。以下路径均相对该地址：

| 方法和路径 | 作用 |
|---|---|
| GET `.well-known/scnet-authentication` | 匿名返回协议版本、显示名、基地址和 base64url SPKI 公钥 |
| POST `v1/transactions` | 创建预登录或当前游戏连接的认证事务，可带本服务已有 grant |
| POST `v1/transactions/poll` | 签名轮询自己的事务，领取结果 |
| POST `v1/transactions/cancel` | 签名取消自己的事务 |
| POST `v1/grants/revoke` | 签名撤销自己的 grant |

POST 请求为 `SignedAuthenticationRequest`，包含 SPKI 公钥和签名 `proof`。验证方通过该公钥验证签名和地址，而不是直接相信请求提供的身份。

请求 JWT 包含 `iss`/`sub`（用户地址）、`aud`（认证方地址）、`jti`（随机请求 ID）、`iat`、`nbf`、`exp`、`htm=POST`、`htu`（完整固定入口 URL）及签名覆盖的 `data` 对象。最长 60 秒，最多允许 5 秒未来时钟偏差，过期不宽限。服务端验证后必须通过有界 replay cache 消费请求 ID；重试必须重新签名。

`VerifyDiscoveryAsync` 使用同一有界客户端匿名读取描述，确认地址及公钥仍与固定信息一致；变化时明确失败，
不能覆盖旧信任或把缓存授权发送到变化后的服务。游戏登录及退出流程在提交授权前执行此检查。

JSON 属性采用 camelCase，枚举为 camelCase 字符串。请求和响应不包含用户私钥。客户端限制响应大小、超时并禁止重定向；发现响应不能替换客户端请求的基地址。

## 事务和长期授权

预登录用途为 `login`，不携带游戏挑战；进入服务器用途为 `join`，必须携带挑战且挑战用户与签名者一致。所有注册、账号验证和人工审核由提供者自己的网页处理。

事务状态为 pending、completed、denied、cancelled、expired；终态不能由客户端声明。Pending 响应提供浏览器地址和建议轮询间隔。完成可返回用户专属的 opaque grant、到期时间和本次连接批准。

Grant 不是游戏身份证明，不发给游戏服务器。它需要同一用户密钥签名才能使用；有效时可以免网页申请新批准。到期、撤销、账号封禁、限流和不可用具有不同错误码，不能一律转成重新登录或降级。

## 游戏连接批准

批准 JWT 包含认证方 `iss`、用户 `sub`、游戏服务器 `aud`、随机 `jti`、`purpose=join`、挑战 `nonce`、连接绑定 `cb` 和标准时间字段。Nonce 与连接绑定均为 32 字节的规范 base64url。

游戏服务器必须以自己的本次挑战调用 `AdmissionApprovalTokens.ValidateAsync`，检查可信公钥、用户、目标、连接、用途及有效期。批准最长 120 秒；用于网页交互的挑战最长 600 秒，两者期限独立。通过密码或批准后，仍需检查存档封禁、重复登录、人数和其他游戏规则。验证成功后应消费对应游戏准入尝试，不能在另一连接复用。

参考依据：[RFC 7515](https://www.rfc-editor.org/rfc/rfc7515)、[RFC 7638](https://www.rfc-editor.org/rfc/rfc7638)、[RFC 8725](https://www.rfc-editor.org/rfc/rfc8725)。
