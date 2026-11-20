using Engine.Core.Security;
using Engine.Core.Text;

namespace Engine.Test.Security;

public sealed class IdentityAddressTest
{
    private const string _example = "scnet10j0ykz3dvyu0t39qjmnmxtgcp7jxe8sg2xmay65s8axx969tw5wstemtrh";

    [Fact]
    public void FullFingerprintMatchesIndependentReferenceExample()
    {
        var fingerprint = Convert.FromHexString("7c9e4b0a2d6138f5c4a096e7b32d180fa46c9e0851b7d26a903f4c62e8ab751d");
        Assert.Equal(_example, IdentityAddress.Encode(fingerprint));
        Assert.Equal(fingerprint, IdentityAddress.Decode(_example));
        Assert.Equal(_example, IdentityAddress.Normalize(_example.ToUpperInvariant()));
        Assert.Equal(64, _example.Length);
    }

    [Fact]
    public void EveryFingerprintBitSurvivesEncodingAndDecoding()
    {
        for (var bit = 0; bit < 256; bit++)
        {
            var fingerprint = new byte[32];
            fingerprint[bit / 8] = (byte)(1 << (bit % 8));
            Assert.Equal(fingerprint, IdentityAddress.Decode(IdentityAddress.Encode(fingerprint)));
        }
    }

    [Fact]
    public void IdentityRulesRejectWrongPrefixSizePaddingAndLegacyEncoding()
    {
        var symbols = Bech32m.Decode(_example, out _);
        Assert.Throws<FormatException>(() => IdentityAddress.Decode(Bech32m.Encode("other", symbols)));
        symbols[^1] |= 1;
        Assert.Throws<FormatException>(() => IdentityAddress.Decode(Bech32m.Encode("scnet", symbols)));
        Assert.Throws<ArgumentException>(() => IdentityAddress.Encode(new byte[31]));
        Assert.Throws<FormatException>(() => IdentityAddress.Decode(new string('A', 43)));
        Assert.Throws<FormatException>(() => IdentityAddress.Decode(_example[..^1]));
        Assert.Throws<FormatException>(() => IdentityAddress.Decode("S" + _example[1..]));
    }

    [Fact]
    public void EverySingleCharacterSubstitutionIsDetected()
    {
        const string alphabet = "qpzry9x8gf2tvdw0s3jn54khce6mua7l";
        for (var i = 6; i < _example.Length; i++)
        {
            foreach (var replacement in alphabet)
            {
                if (_example[i] == replacement) { continue; }

                var mutated = _example[..i] + replacement + _example[(i + 1)..];
                Assert.Throws<FormatException>(() => IdentityAddress.Decode(mutated));
            }
        }
    }
}
