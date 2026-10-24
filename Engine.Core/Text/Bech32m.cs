namespace Engine.Core.Text;

/// <summary>BIP 350 encoding of five-bit symbols, without Bitcoin-specific address rules.</summary>
public static class Bech32m
{
    private const string _alphabet = "qpzry9x8gf2tvdw0s3jn54khce6mua7l";

    private const uint _constant = 0x2bc830a3;

    private static readonly uint[] _generators = [0x3b6a57b2, 0x26508e6d, 0x1ea119fa, 0x3d4233dd, 0x2a1462b3];

    public static string Encode(string prefix, ReadOnlySpan<byte> symbols)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        if (prefix.Length == 0 ||
            prefix.Length + symbols.Length + 7 > 90 ||
            prefix.Any(character => character is < '!' or > '~') ||
            prefix != prefix.ToLowerInvariant())
        {
            throw new ArgumentException(
                "Bech32m requires a lowercase printable prefix and at most 90 characters.",
                nameof(prefix)
            );
        }

        var result = new char[prefix.Length + symbols.Length + 7];
        prefix.CopyTo(0, result, 0, prefix.Length);
        result[prefix.Length] = '1';
        var checksum = ExpandPrefix(prefix);
        for (var i = 0; i < symbols.Length; i++)
        {
            if (symbols[i] > 31)
            {
                throw new ArgumentException("Bech32m symbols must fit in five bits.", nameof(symbols));
            }

            result[prefix.Length + 1 + i] = _alphabet[symbols[i]];
            checksum = Step(checksum, symbols[i]);
        }

        for (var i = 0; i < 6; i++) { checksum = Step(checksum, 0); }

        checksum ^= _constant;
        for (var i = 0; i < 6; i++)
        {
            result[result.Length - 6 + i] = _alphabet[(int)(checksum >> (5 * (5 - i)) & 31)];
        }

        return new string(result);
    }

    public static byte[] Decode(string value, out string prefix)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length is < 8 or > 90 ||
            value.Any(character => character is < '!' or > '~') ||
            value != value.ToLowerInvariant() && value != value.ToUpperInvariant())
        {
            throw new FormatException("Invalid Bech32m length, character or mixed case.");
        }

        value = value.ToLowerInvariant();
        var separator = value.LastIndexOf('1');
        if (separator < 1 || separator + 7 > value.Length)
        {
            throw new FormatException("Invalid Bech32m separator or checksum length.");
        }

        prefix = value[..separator];
        var checksum = ExpandPrefix(prefix);
        var data = new byte[value.Length - separator - 7];
        for (var i = separator + 1; i < value.Length; i++)
        {
            var symbol = _alphabet.IndexOf(value[i]);
            if (symbol < 0) { throw new FormatException("Invalid Bech32m data character."); }

            checksum = Step(checksum, (byte)symbol);
            if (i < value.Length - 6) { data[i - separator - 1] = (byte)symbol; }
        }

        if (checksum != _constant) { throw new FormatException("Invalid Bech32m checksum."); }

        return data;
    }

    private static uint ExpandPrefix(string prefix)
    {
        uint checksum = 1;
        foreach (var character in prefix) { checksum = Step(checksum, (byte)(character >> 5)); }

        checksum = Step(checksum, 0);
        foreach (var character in prefix) { checksum = Step(checksum, (byte)(character & 31)); }

        return checksum;
    }

    private static uint Step(uint checksum, byte symbol)
    {
        var top = checksum >> 25;
        checksum = (checksum & 0x1ffffff) << 5 ^ symbol;
        for (var i = 0; i < 5; i++)
        {
            if ((top >> i & 1) != 0) { checksum ^= _generators[i]; }
        }

        return checksum;
    }
}
