using Engine.Core.Security;

namespace Game.Protocol.Identity;

/// <summary>规范的 SCNET Bech32m 公钥指纹，独立于角色和连接标识。</summary>
[MessagePack.MessagePackFormatter(typeof(Serialization.Formatters.ProtocolValueFormatter))]
public readonly record struct PlayerAddress
{
    private readonly string? _value;

    public PlayerAddress(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        try { _value = IdentityAddress.Normalize(value); }
        catch (FormatException exception)
        {
            throw new ArgumentException(
                "A player address must contain a valid SCNET Bech32m fingerprint.",
                nameof(value),
                exception
            );
        }
    }

    public string Value =>
        _value ?? throw new InvalidOperationException("The player address has not been initialized.");

    public override string ToString() => _value ?? string.Empty;
}
