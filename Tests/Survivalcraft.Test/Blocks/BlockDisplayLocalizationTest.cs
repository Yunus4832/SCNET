using Game.Blocks;
using Game.ClientContent;
using Game.Localization;
using Game.Modding.Blocks;
using Game.Terrains;
using Game.World;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Blocks;

[Collection(ConfigFileCollection.Name)]
public sealed class BlockDisplayLocalizationTest
{
    [Theory]
    [InlineData("zh-CN")]
    [InlineData("en-US")]
    [InlineData("pt-PT")]
    [InlineData("ru-RU")]
    public void VariantNamesResolveAtDisplayIncludingPressurePlateOrientation(string culture)
    {
        Game.Managers.ContentManager.Initialize();
        using var world = new WorldCatalogScope(RuntimeSide.Client);
        using var presentations = new BlockPresentationCatalog(
            world.Catalog,
            new BuiltInClientContentModule().BlockPresentations
        );
        var previous = ClientLanguage.Catalog;
        try
        {
            ClientLanguage.Publish(world.Runtime.Content.CreateLanguageCatalog(culture));
            var language = ClientLanguage.Catalog;
            var arrow = world.Catalog.Blocks.OfType<ArrowBlock>().Single();
            var bullet = world.Catalog.Blocks.OfType<BulletBlock>().Single();
            var pressurePlate = world.Catalog.Blocks.OfType<PressurePlateBlock>().Single();
            var carpet = world.Catalog.Blocks.OfType<CarpetBlock>().Single();
            foreach (var type in Enum.GetValues<ArrowBlock.ArrowType>())
            {
                var value = Terrain.MakeBlockValue(arrow.BlockIndex, ArrowBlock.SetArrowType(0, type));
                Assert.True(language.TryGet(out _, "Blocks", $"ArrowBlock:{(int)type}", "DisplayName"));
                Assert.Equal(
                    language.GetBlock($"ArrowBlock:{(int)type}", "DisplayName"),
                    presentations.Get(arrow.BlockIndex).GetDisplayName(null, value)
                );
            }

            foreach (var type in Enum.GetValues<BulletBlock.BulletType>())
            {
                var value = Terrain.MakeBlockValue(bullet.BlockIndex, BulletBlock.SetBulletType(0, type));
                Assert.True(language.TryGet(out _, "Blocks", $"BulletBlock:{(int)type}", "DisplayName"));
                Assert.Equal(
                    language.GetBlock($"BulletBlock:{(int)type}", "DisplayName"),
                    presentations.Get(bullet.BlockIndex).GetDisplayName(null, value)
                );
            }

            for (var material = 0; material < 2; material++)
            {
                for (var face = 0; face < 6; face++)
                {
                    var data = PressurePlateBlock.SetMaterial(PressurePlateBlock.SetMountingFace(0, face), material);
                    Assert.Equal(
                        language.Get(nameof(PressurePlateBlock), material),
                        presentations.Get(pressurePlate.BlockIndex).GetDisplayName(
                            null,
                            Terrain.MakeBlockValue(pressurePlate.BlockIndex, data)
                        )
                    );
                }
            }

            for (var color = 0; color < 16; color++)
            {
                var name = presentations.Get(carpet.BlockIndex).GetDisplayName(
                    null,
                    Terrain.MakeBlockValue(carpet.BlockIndex, CarpetBlock.SetColor(0, color))
                );
                Assert.Contains(language.GetWorldPalette(color), name);
                Assert.Contains(language.GetBlock("CarpetBlock:0", "DisplayName"), name);
            }

            Assert.Empty(
                presentations.Get(arrow.BlockIndex).GetDisplayName(
                    null,
                    Terrain.MakeBlockValue(arrow.BlockIndex, 15)
                )
            );
            Assert.Empty(
                presentations.Get(bullet.BlockIndex).GetDisplayName(
                    null,
                    Terrain.MakeBlockValue(bullet.BlockIndex, 15)
                )
            );
        }
        finally { ClientLanguage.Publish(previous); }
    }
}
