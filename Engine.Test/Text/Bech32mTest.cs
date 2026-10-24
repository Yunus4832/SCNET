using Engine.Core.Text;

namespace Engine.Test.Text;

public sealed class Bech32mTest
{
    [Theory]
    [InlineData("A1LQFN3A")]
    [InlineData("a1lqfn3a")]
    [InlineData("an83characterlonghumanreadablepartthatcontainsthetheexcludedcharactersbioandnumber11sg7hg6")]
    [InlineData("abcdef1l7aum6echk45nj3s0wdvt2fg8x9yrzpqzd3ryx")]
    [InlineData("11llllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllludsr8")]
    [InlineData("split1checkupstagehandshakeupstreamerranterredcaperredlc445v")]
    [InlineData("?1v759aa")]
    public void Bip350ValidVectorsRoundTrip(string encoded)
    {
        var symbols = Bech32m.Decode(encoded, out var prefix);
        Assert.Equal(encoded.ToLowerInvariant(), Bech32m.Encode(prefix, symbols));
    }

    [Theory]
    [InlineData(" 1xj0phk")]
    [InlineData("\u007f1g6xzxy")]
    [InlineData("\u00801vctc34")]
    [InlineData("an84characterslonghumanreadablepartthatcontainsthetheexcludedcharactersbioandnumber11d6pts4")]
    [InlineData("qyrz8wqd2c9m")]
    [InlineData("1qyrz8wqd2c9m")]
    [InlineData("y1b0jsk6g")]
    [InlineData("lt1igcx5c0")]
    [InlineData("in1muywd")]
    [InlineData("mm1crxm3i")]
    [InlineData("au1s5cgom")]
    [InlineData("M1VUXWEZ")]
    [InlineData("16plkw9")]
    [InlineData("1p2gdwpf")]
    [InlineData("a12uel5l")]
    [InlineData("a1LQFN3A")]
    public void Bip350InvalidVectorsAndOldBech32AreRejected(string encoded)
    {
        Assert.Throws<FormatException>(() => Bech32m.Decode(encoded, out _));
    }

    [Fact]
    public void EncoderRejectsInvalidSymbolsPrefixesAndLength()
    {
        Assert.Throws<ArgumentException>(() => Bech32m.Encode("", []));
        Assert.Throws<ArgumentException>(() => Bech32m.Encode("SCNET", []));
        Assert.Throws<ArgumentException>(() => Bech32m.Encode("scnet", new byte[] { 32 }));
        Assert.Throws<ArgumentException>(() => Bech32m.Encode("scnet", new byte[79]));
    }
}
