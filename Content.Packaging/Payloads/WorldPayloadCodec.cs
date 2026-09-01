using System.Xml;
using System.Xml.Linq;

namespace Content.Packaging.Payloads;

public sealed class WorldPayloadCodec : IContentPayloadCodec
{
    public ContentPackageType Type => ContentPackageType.World;

    public void Validate(ContentPayloadValidationContext context)
    {
        ContentPayloadValidation.ValidateEnvelope(context, "scnet.world-v1", "payload/world/Project.xml",
            "application/xml", ["projectFormat", "regionsDirectory"], allowAdditionalPayload: true);
        if (context.Manifest.Metadata.GetProperty("projectFormat").GetString() != "scnet-project-xml-v1" ||
            context.Manifest.Metadata.GetProperty("regionsDirectory").GetString() != "payload/world/Regions")
        {
            throw new ContentPackageException("World metadata is invalid.");
        }

        foreach (var path in context.Paths.Where(path => path != "manifest.json"))
        {
            if (path != "payload/world/Project.xml" &&
                (!path.StartsWith("payload/world/Regions/", StringComparison.Ordinal) ||
                 path.Count(character => character == '/') != 3 ||
                 !path.EndsWith(".dat", StringComparison.Ordinal)) ||
                path.Contains("/EmbeddedContent/", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("/backup/", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".snapshot", StringComparison.OrdinalIgnoreCase))
            {
                throw new ContentPackageException($"World payload path '{path}' is invalid.");
            }
        }

        try
        {
            using var stream = context.OpenEntry("payload/world/Project.xml");
            using var reader =
                XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
            var document = XDocument.Load(reader, LoadOptions.None);
            var root = document.Root;
            if (root is null || root.Name != "Project" ||
                root.Attributes().Count() != 3 ||
                root.Attribute("Version")?.Value != "SCNET-1" ||
                root.Attribute("Guid")?.Value != "9e9a67f8-79df-4d05-8cfa-61bd8095661e" ||
                root.Attribute("Name")?.Value != "GameProject" ||
                root.Elements("Subsystems").Count() != 1 ||
                root.Elements("Entities").Count() != 1 ||
                root.Elements().Count() != 2 ||
                root.DescendantsAndSelf().Any(element => element.Name.Namespace != XNamespace.None ||
                                                         element.Attributes().Any(attribute =>
                                                             attribute.Name.Namespace != XNamespace.None)))
            {
                throw new ContentPackageException("World Project.xml does not match scnet-project-xml-v1.");
            }
        }
        catch (XmlException exception)
        {
            throw new ContentPackageException("World project XML is invalid.", exception);
        }
    }
}
