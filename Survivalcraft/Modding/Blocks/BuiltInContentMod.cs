using System.Text;
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
        "1.0.0",
        Side: ModSide.Common);

    public static ModDescriptor CreateDescriptor()
    {
        return new ModDescriptor(Manifest, static () => new BuiltInContentMod());
    }

    public void Configure(IModContext context)
    {
        context.Commands.Register(
            new ResourceId(context.Manifest.ModId, "help"),
            BuiltInCommands.CreateHelp());
        context.Commands.Register(
            new ResourceId(context.Manifest.ModId, "time"),
            BuiltInCommands.CreateTime());
        context.Commands.Register(
            new ResourceId(context.Manifest.ModId, "stop"),
            BuiltInCommands.CreateStop());
        context.Commands.Register(
            new ResourceId(context.Manifest.ModId, "permission"),
            BuiltInCommands.CreatePermission());
        context.Commands.Register(
            new ResourceId(context.Manifest.ModId, "auth"),
            BuiltInCommands.CreateAuth());

        foreach (var asset in BuiltInContentAssets.Load())
        {
            context.Extensions.RegisterContent(
                new ResourceId(context.Manifest.ModId, asset.RelativePath),
                asset.RelativePath,
                asset.CopyBytes());
        }

        context.Extensions.RegisterBlockData(
            new ResourceId(context.Manifest.ModId, "base"),
            static () =>
            {
                var data = ContentManager.Get<string>("BlocksData");
                ContentManager.Dispose("BlocksData");
                return data;
            });
        context.Extensions.RegisterXmlData(
            XmlDataExtensions.DatabaseRegistryName,
            new ResourceId(context.Manifest.ModId, "base"),
            XmlContributionMode.Base,
            static () => ReadBuiltInXml("Database"));
        context.Extensions.RegisterXmlData(
            XmlDataExtensions.RecipeRegistryName,
            new ResourceId(context.Manifest.ModId, "base"),
            XmlContributionMode.Base,
            static () => ReadBuiltInXml("CraftingRecipes"));
        context.Extensions.RegisterXmlData(
            XmlDataExtensions.ClothingRegistryName,
            new ResourceId(context.Manifest.ModId, "base"),
            XmlContributionMode.Base,
            static () => ReadBuiltInXml("Clothes"));

        var blockTypes = typeof(Block).Assembly.GetTypes()
            .Where(type => type.IsSubclassOf(typeof(Block)) && !type.IsAbstract)
            .OrderBy(type => type.FullName, StringComparer.Ordinal);
        foreach (var blockType in blockTypes)
        {
            var indexField = blockType.GetRuntimeFields()
                .FirstOrDefault(field => field is { Name: "Index", IsPublic: true, IsStatic: true } &&
                                         field.FieldType == typeof(int));
            if (indexField is null)
            {
                continue;
            }

            var id = new ResourceId(context.Manifest.ModId, ToResourcePath(blockType.Name));
            context.Extensions.RegisterBlock(id, (int)indexField.GetValue(null)!, blockType);
        }
    }

    public void Start(IModContext context)
    {
    }

    public void Stop()
    {
    }

    internal static string ToResourcePath(string typeName)
    {
        var name = typeName.EndsWith("Block", StringComparison.Ordinal)
            ? typeName[..^"Block".Length]
            : typeName;
        var result = new StringBuilder(name.Length + 8);
        for (var index = 0; index < name.Length; index++)
        {
            var character = name[index];
            if (char.IsUpper(character) && index > 0 &&
                (char.IsLower(name[index - 1]) ||
                 index + 1 < name.Length && char.IsLower(name[index + 1])))
            {
                result.Append('_');
            }

            result.Append(char.ToLowerInvariant(character));
        }

        return result.ToString();
    }

    private static XElement ReadBuiltInXml(string name)
    {
        var element = ContentManager.Get<XElement>(name);
        var copy = new XElement(element);
        ContentManager.Dispose(name);
        return copy;
    }
}
