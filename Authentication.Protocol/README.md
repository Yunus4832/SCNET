# SCNET.Authentication.Protocol

SCNET 的外部认证契约、ES256 签名验证和有界 HTTPS 客户端。此包使用 Engine.Core 的身份地址和
Microsoft IdentityModel 的 JWT 实现。完整端点、签名字段与期限见[认证协议](../Doc/Protocol/AuthenticationProtocol.md)。

## 接入入口

| 用途 | 类型或接口 |
|---|---|
| 发现服务并核对已固定的地址及公钥 | `AuthenticationServiceDescription`、`VerifyDiscoveryAsync` |
| 签名与验证事务请求 | `SignedAuthenticationRequest`、`AuthenticationRequestProofs` |
| 阻止重复使用同一份请求证明 | `AuthenticationReplayCache` |
| 生成和验证本次游戏连接的批准 | `AdmissionApprovalTokens` |

发现到的地址和公钥须独立确认。访问入口或公钥变化时，按认证协议处理信任更新。
长期授权只用于向签发它的认证服务申请批准。游戏服务器验证绑定到本次挑战和连接的短期批准，
随后执行自己的准入检查。

开发与部署参考服务见 [Authentication.Server](../Services/Authentication/Authentication.Server/README.md)。
