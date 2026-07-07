using System.Xml.Linq;

using Game.Modding;
using Game.Modding.Data;

namespace Survivalcraft.Test.Modding;

public class GameDataCatalogTest
{
    [Fact]
    public void CatalogReadsDocumentsLazilyAndAppliesPatches()
    {
        var reads = 0;
        var host = new ModHost();
        host.LoadAndStart([new ModDescriptor(
            new ModManifest("example", "Example", "1.0.0"),
            () => new DataMod(() => reads++))]);

        var catalog = GameDataCatalog.Compile(host.Extensions);

        Assert.Equal(0, reads);

        var database = catalog.BuildDatabase();
        var recipes = catalog.BuildRecipes();

        Assert.Equal(2, reads);
        Assert.NotNull(database.Descendants("Entity").SingleOrDefault());
        Assert.NotNull(recipes.Descendants("Recipe").SingleOrDefault());
        host.StopAll();
    }

    private sealed class DataMod(Action onRead) : IMod
    {
        public void Configure(IModContext context)
        {
            Register(
                context,
                XmlDataExtensions.DatabaseRegistryName,
                "database_base",
                XmlContributionMode.Base,
                () => new XElement("Database", new XElement("DatabaseObjects")));
            Register(
                context,
                XmlDataExtensions.DatabaseRegistryName,
                "database_patch",
                XmlContributionMode.Patch,
                () =>
                {
                    onRead();
                    return new XElement("Patch", new XElement("Entity", new XAttribute("Guid", Guid.NewGuid())));
                });
            Register(
                context,
                XmlDataExtensions.RecipeRegistryName,
                "recipes_base",
                XmlContributionMode.Base,
                () => new XElement("Recipes"));
            Register(
                context,
                XmlDataExtensions.RecipeRegistryName,
                "recipes_patch",
                XmlContributionMode.Patch,
                () =>
                {
                    onRead();
                    return new XElement(
                        "Recipes",
                        new XElement("Recipe", new XAttribute("Result", "AirBlock")));
                });
            Register(
                context,
                XmlDataExtensions.ClothingRegistryName,
                "clothing_base",
                XmlContributionMode.Base,
                () => new XElement("Clothes"));
        }

        public void Start(IModContext context)
        {
        }

        public void Stop()
        {
        }

        private static void Register(
            IModContext context,
            string registry,
            string path,
            XmlContributionMode mode,
            Func<XElement> read)
        {
            context.Extensions.RegisterXmlData(
                registry,
                new ResourceId(context.Manifest.ModId, path),
                mode,
                read);
        }
    }
}
