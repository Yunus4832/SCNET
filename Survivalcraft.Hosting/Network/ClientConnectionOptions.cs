namespace Game.Network;

/// <summary>Explicit immutable application facts for a client admission attempt.</summary>
public sealed class ClientConnectionOptions
{
    public string ModDataHash { get; }

    public ClientConnectionOptions(string modDataHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modDataHash);
        ModDataHash = modDataHash;
    }
}
