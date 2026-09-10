using Game;

namespace Survivalcraft.Test.World;

public sealed class WorldSeedConverterTest
{
    [Fact]
    public void CreateRandomTextSeedUsesEightAsciiLettersOrDigits()
    {
        var seed = WorldSeedConverter.CreateRandomTextSeed();

        Assert.Matches("^[A-Za-z0-9]{8}$", seed);
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("123456", 123456)]
    [InlineData("-123456", -123456)]
    public void FromTextPreservesIntegerSeeds(string text, int expected)
    {
        Assert.Equal(expected, WorldSeedConverter.FromText(text));
    }

    [Fact]
    public void FromTextUsesLegacyCharacterMappingForTextSeeds()
    {
        Assert.Equal(8878, WorldSeedConverter.FromText("abc"));
    }
}
