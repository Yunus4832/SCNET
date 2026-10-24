using Engine.Core.Text;

namespace Engine.Core.Security;

/// <summary>SCNET identity addresses: a complete 32-byte key thumbprint encoded as lowercase Bech32m.</summary>
public static class IdentityAddress
{
    public const int FingerprintLength = 32;

    public const int EncodedLength = 64;

    public const string Prefix = "scnet";

    public static string Encode(ReadOnlySpan<byte> fingerprint)
    {
        if (fingerprint.Length != FingerprintLength)
        {
            throw new ArgumentException("Identity fingerprints require 32 bytes.", nameof(fingerprint));
        }

        Span<byte> symbols = stackalloc byte[52];
        var accumulator = 0;
        var bits = 0;
        var index = 0;
        foreach (var value in fingerprint)
        {
            accumulator = (accumulator << 8 | value) & 4095;
            bits += 8;
            while (bits >= 5)
            {
                bits -= 5;
                symbols[index++] = (byte)(accumulator >> bits & 31);
            }
        }

        if (bits > 0) { symbols[index] = (byte)(accumulator << (5 - bits) & 31); }

        return Bech32m.Encode(Prefix, symbols);
    }

    public static byte[] Decode(string address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (address.Length != EncodedLength) { throw new FormatException("Invalid SCNET identity address length."); }

        var symbols = Bech32m.Decode(address, out var prefix);
        if (prefix != Prefix || symbols.Length != 52 || (symbols[^1] & 15) != 0)
        {
            throw new FormatException("Invalid SCNET identity prefix, fingerprint length or padding.");
        }

        var bytes = new byte[FingerprintLength];
        var accumulator = 0;
        var bits = 0;
        var index = 0;
        foreach (var symbol in symbols)
        {
            accumulator = (accumulator << 5 | symbol) & 4095;
            bits += 5;
            if (bits >= 8)
            {
                bits -= 8;
                bytes[index++] = (byte)(accumulator >> bits);
            }
        }

        return bytes;
    }

    public static string Normalize(string address) => Encode(Decode(address));
}
