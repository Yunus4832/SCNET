namespace Game.Identity;

/// <summary>持久化的身份数据，含未加密的 PKCS#8 私钥，严禁发送给客户端。</summary>
public sealed record StoredLocalIdentity(IdentityPurpose Purpose, string Name, string Address, string PrivateKey)
{
    public override string ToString() => $"{Purpose}: {Name} ({Address})";
}
