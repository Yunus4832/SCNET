namespace Game.Network;

/// <summary>客户端尝试准入时使用的不可变应用信息。</summary>
public sealed class ClientConnectionOptions
{
    public string ModDataHash { get; }

    public ClientConnectionOptions(string modDataHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modDataHash);
        ModDataHash = modDataHash;
    }
}
