using System.Xml.Linq;

using EntitySystem.TemplatesDatabase;

using Game;
using Game.Managers;
using Game.Modding;
using Game.Modding.Blocks;
using Game.Modding.Data;
using Game.Terrains;

using TerritoryStoneMod;

namespace Survivalcraft.Test.Modding;

[Collection(ConfigFileCollection.Name)]
public class TerritoryRegistrationTest
{
    [Theory]
    [InlineData("zh-CN", "领地石")]
    [InlineData("en-US", "Territory Stone")]
    [InlineData("pt-PT", "Pedra de Território")]
    [InlineData("ru-RU", "Камень территории")]
    public void ModLanguageAddsLocalizedBlockAndHelpText(string culture, string name)
    {
        ContentManager.Initialize();
        using var runtime = GameModRuntime.Start([
            new ModDescriptor(new ModManifest(ModEntry.ModId, "Territory Stone", "1.0.3"), () => new ModEntry())
        ]);

        runtime.InitializeLanguage(culture);

        var block = new TerritoryBlock { DisplayName = "Territory Stone" };
        var value = Terrain.MakeBlockValue(TerritoryBlock.Index);
        Assert.Equal(name, block.GetDisplayName(null, value));
        Assert.False(string.IsNullOrWhiteSpace(block.GetDescription(value)));
        Assert.Equal(name, LanguageManager.GetHelpTopic("48", "Title"));
        Assert.Equal("TerritoryStone", LanguageManager.GetHelpTopic("48", "Name"));
        var help = LanguageManager.GetHelpTopic("48", "value");
        Assert.Contains("G", help);
        Assert.Contains("tp", help);
    }

    [Fact]
    public void EntryContributesItsBlockAndDatabaseTemplateThroughOwnedRegistries()
    {
        var host = new ModHost();
        host.LoadAndStart([
            new ModDescriptor(new ModManifest(ModEntry.ModId, "Territory Stone", "1.0.0"), () => new ModEntry())
        ]);
        try
        {
            var block = Assert.Single(host.Extensions.GetRegistry<BlockRegistration>(BlockExtensions.RegistryName).Entries);
            Assert.Equal(ModEntry.ModId, block.Key.Namespace.Value);
            Assert.Equal(TerritoryStoneMod.TerritoryBlock.Index, block.Value.LegacyIndex);
            var territoryBlock = Assert.IsType<TerritoryStoneMod.TerritoryBlock>(block.Value.Factory());
            Assert.True(territoryBlock.IsEditable(TerritoryStoneMod.TerritoryBlock.Index));

            var data = Assert.Single(host.Extensions.GetRegistry<BlockDataRegistration>(BlockExtensions.DataRegistryName).Entries);
            Assert.Contains("TerritoryBlock;Territory Stone;", data.Value.Read());

            var contribution = Assert.Single(host.Extensions.GetRegistry<XmlDataRegistration>(XmlDataExtensions.DatabaseRegistryName).Entries);
            var document = XElement.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Database.xml"));
            ModXmlPatcher.Modify(document.Element("DatabaseObjects")!, contribution.Value.Read().Elements().First());
            ModXmlPatcher.Modify(document.Element("DatabaseObjects")!, contribution.Value.Read().Elements().Last());
            var database = XmlDatabaseSerializer.LoadDatabase(document);
            var member = database.FindDatabaseObject(
                Guid.Parse("0bce54dd-dd98-46ea-8d93-6e5a01692e45"),
                database.FindDatabaseObjectType("MemberSubsystemTemplate", true), true)!;
            var values = new ValuesDictionary();
            values.PopulateFromDatabaseObject(member);
            Assert.Equal("TerritoryStoneMod.TerritorySubsystem", values.GetValue<string>("Class"));
            Assert.False(values.GetValue<bool>("IsOptional"));

            var behavior = database.FindDatabaseObject(
                Guid.Parse("39b6b82c-ae0c-44b5-bf07-a0cb87396830"),
                database.FindDatabaseObjectType("MemberSubsystemTemplate", true), true)!;
            var behaviorValues = new ValuesDictionary();
            behaviorValues.PopulateFromDatabaseObject(behavior);
            Assert.Equal("TerritoryStoneMod.TerritoryBlockBehavior", behaviorValues.GetValue<string>("Class"));
            Assert.Equal("Terrain,Territory", behaviorValues.GetValue<string>("Dependencies"));
            Assert.DoesNotContain(document.Descendants("SubsystemTemplate"),
                element => (string?)element.Attribute("Name") == "TerritoryBlockBehavior");
        }
        finally
        {
            host.StopAll();
        }

        Assert.Empty(host.Extensions.GetRegistry<BlockRegistration>(BlockExtensions.RegistryName).Entries);
        Assert.Empty(host.Extensions.GetRegistry<XmlDataRegistration>(XmlDataExtensions.DatabaseRegistryName).Entries);
    }
}
