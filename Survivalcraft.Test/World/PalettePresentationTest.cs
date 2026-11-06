using Engine.Core.Mathematics;

using EntitySystem.TemplatesDatabase;

using Game.Subsystems;
using Game.World;
using Game.World.Presentation;

using Survivalcraft.Test.Fixtures;
using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.World;

[Collection(ConfigFileCollection.Name)]
public sealed class PalettePresentationTest
{
    [Fact]
    public void RulesCopyAllPaletteEntriesAndExposeOnlyCustomNames()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        var settings = new WorldSettings();
        settings.Palette.Names[3] = "Ocean";
        var info = new SubsystemGameInfo { WorldSettings = settings };
        project.AddSubsystem(info);
        var palette = new SubsystemPalette();
        project.InitializeSubsystem(palette);
        palette.Load(new ValuesDictionary());
        for (var i = 0; i < WorldPalette.MaxColors; i++)
        {
            Assert.Equal(WorldPalette.DefaultColors[i], palette.GetColor(i));
            Assert.Equal(SubsystemPalette.DefaultFabricColors[i], palette.GetFabricColor(i));
        }

        Assert.Equal("Ocean", palette.GetName(3));
        Assert.Empty(palette.GetName(0));
        settings.Palette.Colors[0] = Color.Black;
        settings.Palette.Names[3] = "Changed";
        Assert.Equal(Color.White, palette.GetColor(0));
        Assert.Equal("Ocean", palette.GetName(3));
        Assert.Equal("Ocean Block", PalettePresentation.GetName(palette, 3, "Block"));
        Assert.Equal(
            Game.Localization.ClientLanguage.Catalog.GetWorldPalette(0),
            PalettePresentation.GetName(palette, 0)
        );
        Assert.Equal("Block", PalettePresentation.GetName(null, "Block"));
    }

    [Theory]
    [InlineData(255, 0, 0, 255, 38, 38)]
    // Preserve the existing RGB/HSV conversion results during the ownership refactor.
    [InlineData(0, 255, 0, 38, 255, 255)]
    [InlineData(0, 0, 255, 38, 255, 255)]
    [InlineData(128, 128, 128, 128, 128, 128)]
    public void FabricColorsRetainTheOriginalSaturationReduction(
        int r,
        int g,
        int b,
        int expectedR,
        int expectedG,
        int expectedB
    )
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        var settings = new WorldSettings();
        settings.Palette.Colors[0] = new Color(r, g, b);
        project.AddSubsystem(new SubsystemGameInfo { WorldSettings = settings });
        var palette = new SubsystemPalette();
        project.InitializeSubsystem(palette);
        palette.Load(new ValuesDictionary());
        Assert.Equal(new Color(expectedR, expectedG, expectedB), palette.GetFabricColor(0));
        Assert.Equal(Color.White, PalettePresentation.GetColor((SubsystemTerrain?)null, null));
        Assert.Equal(WorldPalette.DefaultColors[0], PalettePresentation.GetColor((SubsystemTerrain?)null, 0));
    }
}
