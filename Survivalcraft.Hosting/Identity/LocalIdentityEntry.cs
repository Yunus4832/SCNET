namespace Game.Identity;

/// <summary>实例身份的公开元数据，不暴露私钥。</summary>
public sealed record LocalIdentityEntry(IdentityPurpose Purpose, string Name, string Address);
