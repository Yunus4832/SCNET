using System.Text;
using System.Xml.Linq;

namespace Game.Modding.Data;

public sealed class GameDataCatalog
{
    private readonly NamespacedRegistry<XmlDataRegistration> _database;
    private readonly NamespacedRegistry<XmlDataRegistration> _recipes;
    private readonly NamespacedRegistry<XmlDataRegistration> _clothing;

    private GameDataCatalog(
        NamespacedRegistry<XmlDataRegistration> database,
        NamespacedRegistry<XmlDataRegistration> recipes,
        NamespacedRegistry<XmlDataRegistration> clothing)
    {
        _database = database;
        _recipes = recipes;
        _clothing = clothing;
    }

    public XElement BuildDatabase() => CompileDocument(_database, ApplyDatabasePatch);

    public XElement BuildRecipes() => CompileDocument(
        _recipes,
        static (document, patch) => ModXmlPatcher.CombineCrLogic(document, patch));

    public XElement BuildClothing() => CompileDocument(_clothing, ApplyClothingPatch);

    public static GameDataCatalog Compile(ExtensionRegistry extensions)
    {
        var database = extensions.GetRegistry<XmlDataRegistration>(XmlDataExtensions.DatabaseRegistryName);
        var recipes = extensions.GetRegistry<XmlDataRegistration>(XmlDataExtensions.RecipeRegistryName);
        var clothing = extensions.GetRegistry<XmlDataRegistration>(XmlDataExtensions.ClothingRegistryName);
        ValidateBase(database, XmlDataExtensions.DatabaseRegistryName);
        ValidateBase(recipes, XmlDataExtensions.RecipeRegistryName);
        ValidateBase(clothing, XmlDataExtensions.ClothingRegistryName);
        return new GameDataCatalog(database, recipes, clothing);
    }

    private static XElement CompileDocument(
        NamespacedRegistry<XmlDataRegistration> registry,
        Action<XElement, XElement> applyPatch)
    {
        var entries = registry.Entries.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal).ToArray();
        var bases = entries.Where(pair => pair.Value.Mode == XmlContributionMode.Base).ToArray();
        if (bases.Length != 1)
        {
            throw new InvalidOperationException(
                $"Registry requires exactly one base document, but found {bases.Length}.");
        }

        var document = new XElement(bases[0].Value.Read());
        foreach (var (_, contribution) in entries.Where(pair => pair.Value.Mode == XmlContributionMode.Patch))
        {
            applyPatch(document, new XElement(contribution.Read()));
        }

        return document;
    }

    private static void ValidateBase(NamespacedRegistry<XmlDataRegistration> registry, string registryName)
    {
        var baseCount = registry.Entries.Count(pair => pair.Value.Mode == XmlContributionMode.Base);
        if (baseCount != 1)
        {
            throw new InvalidOperationException(
                $"Registry {registryName} requires exactly one base document, but found {baseCount}.");
        }
    }

    private static void ApplyDatabasePatch(XElement document, XElement patch)
    {
        using var stream = ToStream(patch);
        ModXmlPatcher.CombineDataBase(document, stream);
    }

    private static void ApplyClothingPatch(XElement document, XElement patch)
    {
        using var stream = ToStream(patch);
        ModXmlPatcher.CombineClo(document, stream);
    }

    private static MemoryStream ToStream(XElement element)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(element.ToString(SaveOptions.DisableFormatting)));
    }
}
