using System.Text;
using System.Xml.Linq;

namespace Game.Modding.Data;

public enum XmlContributionMode
{
    Base,
    Patch
}

public sealed record XmlDataRegistration(XmlContributionMode Mode, Func<XElement> Read);

public static class XmlDataExtensions
{
    public const string DatabaseRegistryName = "database_data";

    public const string RecipeRegistryName = "recipe_data";

    public const string ClothingRegistryName = "clothing_data";

    public static IDisposable RegisterXmlData(
        this IModExtensions extensions,
        string registryName,
        ResourceId id,
        XmlContributionMode mode,
        Func<XElement> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        return extensions.Register(registryName, id, new XmlDataRegistration(mode, read));
    }

    internal static XElement ParseUtf8(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var reader = new StreamReader(stream, Encoding.UTF8, true);
        return XElement.Load(reader);
    }
}
