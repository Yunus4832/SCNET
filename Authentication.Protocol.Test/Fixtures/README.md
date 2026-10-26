# 外部认证互操作向量

`openssl-approval.json` 是固定的公开测试数据，用于验证独立实现与协议库之间的互操作：

- Python 标准库计算规范 JWK JSON、SHA-256 指纹和 Bech32m 地址。
- OpenSSL 生成 P-256 公钥并以 SHA-256/ECDSA 签署 JWS 的原始签名输入。
- 生成器将 OpenSSL DER 签名转换为 JWS 的固定 64 字节 `R || S` 格式。
- 协议库在固定时间验证批准、公钥地址和完整指纹，不要求安装 OpenSSL。

向量不包含私钥、grant 或真实账号；生成用的随机私钥仅存在于系统临时目录，生成结束后删除。
测试批准仅在记录的固定时间窗口有效，不是生产认证凭证。

需要更新向量时，从仓库根目录运行：

```bash
python3 Authentication.Protocol.Test/Fixtures/generate_openssl_vector.py
```

再运行 `dotnet test Authentication.Protocol.Test/Authentication.Protocol.Test.csproj`。
重建使用新的随机密钥及签名，因此会改变公开向量。普通构建和测试只读取已提交的向量，不重新生成。
此测试证明该签名样例的互操作，不代替协议边界测试或安全审查。
