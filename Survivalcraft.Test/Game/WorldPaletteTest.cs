using Game;

namespace Survivalcraft.Test.Worlds;

public class WorldPaletteTest
{
    [Fact]
    public void DefaultNamesAreStoredAsEmptyOverrides()
    {
        var palette = new WorldPalette();

        Assert.All(palette.Names, Assert.Empty);
    }

    [Fact]
    public void SavePreservesOnlyCustomNameOverrides()
    {
        var palette = new WorldPalette();
        palette.Names[3] = "Ocean";

        var restored = new WorldPalette(palette.Save());

        Assert.Equal("Ocean", restored.Names[3]);
        Assert.All(restored.Names.Where((_, index) => index != 3), Assert.Empty);
    }
}
