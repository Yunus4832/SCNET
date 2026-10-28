namespace Game.Identity;

/// <summary>Public metadata for an instance-owned identity; never exposes its private key.</summary>
public sealed record LocalIdentityEntry(IdentityPurpose Purpose, string Name, string Address);
