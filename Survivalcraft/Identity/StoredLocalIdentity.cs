namespace Game.Identity;

/// <summary>Persisted identity data, including the unencrypted PKCS#8 private key. Never send to clients.</summary>
public sealed record StoredLocalIdentity(IdentityPurpose Purpose, string Name, string Address, string PrivateKey)
{
    public override string ToString() => $"{Purpose}: {Name} ({Address})";
}
