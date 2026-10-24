using System.Text;

using Engine.Core.Security;

using Game.Protocol.Identity;

namespace Game.Protocol.Serialization;

/// <summary>Canonical bounded subject addresses for game messages and mod protocols.</summary>
public static class PlayerAddressCodec
{
    public static PlayerAddress Read(ProtocolReader reader) => ReadCore(reader, false)!.Value;

    public static PlayerAddress? ReadOptional(ProtocolReader reader) => ReadCore(reader, true);

    private static PlayerAddress? ReadCore(ProtocolReader reader, bool allowEmpty)
    {
        var size = reader.ReadByte();
        if (size == 0 && allowEmpty) { return null; }

        if (size != IdentityAddress.EncodedLength)
        {
            throw new InvalidDataException("Invalid subject address length.");
        }

        var data = reader.ReadBytes(size);
        try { return new PlayerAddress(Encoding.ASCII.GetString(data)); }
        catch (ArgumentException exception) { throw new InvalidDataException("Invalid subject address.", exception); }
    }

    public static void Write(ProtocolWriter writer, PlayerAddress address) => writer.Write(address.Value);

    public static void WriteOptional(ProtocolWriter writer, PlayerAddress? address)
    {
        writer.Write(address?.Value ?? string.Empty);
    }
}
