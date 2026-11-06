using System.Reflection;
using System.Text;
using System.Xml.Linq;

using Game.Commands.BuiltIn;
using Game.Modding.Content;
using Game.Modding.Data;

namespace Game.Modding.Blocks;

public sealed class BuiltInContentMod(Action<IModCommands, ModId>? registerAppCommands = null) : IMod
{
    public static readonly ModManifest Manifest = new(
        BuiltInBlockIdentities.Namespace.Value,
        "Built-in Game Content",
        typeof(BuiltInContentMod).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion.Split('+', 2)[0] ??
        throw new InvalidOperationException("Host product version is missing.")
    );

    public static ModDescriptor CreateDescriptor(Action<IModCommands, ModId>? registerAppCommands = null)
    {
        return new ModDescriptor(Manifest, () => new BuiltInContentMod(registerAppCommands));
    }

    public void Configure(IModContext context)
    {
        BuiltInWorldCommands.Register(context.Commands, context.Manifest.ModId);
        registerAppCommands?.Invoke(context.Commands, context.Manifest.ModId);

        var assets = BuiltInContentAssets.Load();
        foreach (var asset in assets)
        {
            context.Extensions.RegisterContent(
                new ResourceId(context.Manifest.ModId, asset.RelativePath),
                asset.RelativePath,
                asset.CopyBytes()
            );
        }

        context.Extensions.RegisterBlockData(
            new ResourceId(context.Manifest.ModId, "base"),
            () => ReadText(assets, "BlocksData.txt")
        );
        context.Extensions.RegisterXmlData(
            XmlDataExtensions.DatabaseRegistryName,
            new ResourceId(context.Manifest.ModId, "base"),
            XmlContributionMode.Base,
            () => ReadBuiltInXml(assets, "Database.xml")
        );
        context.Extensions.RegisterXmlData(
            XmlDataExtensions.RecipeRegistryName,
            new ResourceId(context.Manifest.ModId, "base"),
            XmlContributionMode.Base,
            () => ReadBuiltInXml(assets, "CraftingRecipes.xml")
        );
        context.Extensions.RegisterXmlData(
            XmlDataExtensions.ClothingRegistryName,
            new ResourceId(context.Manifest.ModId, "base"),
            XmlContributionMode.Base,
            () => ReadBuiltInXml(assets, "Clothes.xml")
        );

        BuiltInBlockRegistration.Register(context.Extensions);
    }

    public void Start(IModContext context)
    {
    }

    public void Stop()
    {
    }

    private static string ReadText(IReadOnlyList<ContentRegistration> assets, string name)
    {
        using var stream = assets.Single(asset => asset.RelativePath == name).OpenRead();
        using var reader = new StreamReader(stream, Encoding.UTF8, true);
        return reader.ReadToEnd();
    }

    private static XElement ReadBuiltInXml(IReadOnlyList<ContentRegistration> assets, string name)
    {
        using var stream = assets.Single(asset => asset.RelativePath == name).OpenRead();
        return XElement.Load(stream);
    }
}
