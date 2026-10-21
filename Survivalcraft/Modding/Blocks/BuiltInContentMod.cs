using System.Xml.Linq;

using Game.Commands;
using Game.Modding.Content;
using Game.Modding.Data;

namespace Game.Modding.Blocks;

public sealed class BuiltInContentMod : IMod
{
    public static readonly ModManifest Manifest = new(
        "game",
        "Built-in Game Content",
        VersionsManager.Version,
        Side: ModSide.Common
    );

    public static ModDescriptor CreateDescriptor()
    {
        return new ModDescriptor(Manifest, static () => new BuiltInContentMod());
    }

    public void Configure(IModContext context)
    {
        BuiltInCommands.Register(context.Commands, context.Manifest.ModId);

        foreach (var asset in BuiltInContentAssets.Load())
        {
            context.Extensions.RegisterContent(
                new ResourceId(context.Manifest.ModId, asset.RelativePath),
                asset.RelativePath,
                asset.CopyBytes()
            );
        }

        context.Extensions.RegisterBlockData(
            new ResourceId(context.Manifest.ModId, "base"),
            static () =>
            {
                var data = ContentManager.Get<string>("BlocksData");
                ContentManager.Dispose("BlocksData");
                return data;
            }
        );
        context.Extensions.RegisterXmlData(
            XmlDataExtensions.DatabaseRegistryName,
            new ResourceId(context.Manifest.ModId, "base"),
            XmlContributionMode.Base,
            static () => ReadBuiltInXml("Database")
        );
        context.Extensions.RegisterXmlData(
            XmlDataExtensions.RecipeRegistryName,
            new ResourceId(context.Manifest.ModId, "base"),
            XmlContributionMode.Base,
            static () => ReadBuiltInXml("CraftingRecipes")
        );
        context.Extensions.RegisterXmlData(
            XmlDataExtensions.ClothingRegistryName,
            new ResourceId(context.Manifest.ModId, "base"),
            XmlContributionMode.Base,
            static () => ReadBuiltInXml("Clothes")
        );

        BuiltInBlockRegistry.Register(context.Extensions);
    }

    public void Start(IModContext context)
    {
    }

    public void Stop()
    {
    }

    private static XElement ReadBuiltInXml(string name)
    {
        var element = ContentManager.Get<XElement>(name);
        var copy = new XElement(element);
        ContentManager.Dispose(name);
        return copy;
    }
}
