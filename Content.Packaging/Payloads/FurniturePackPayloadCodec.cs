using System.Xml;
using System.Xml.Linq;

namespace Content.Packaging.Payloads;

public sealed class FurniturePackPayloadCodec : IContentPayloadCodec
{
    public ContentPackageType Type => ContentPackageType.FurniturePack;

    public void Validate(ContentPayloadValidationContext context)
    {
        const string entry = "payload/furniture/FurnitureDesigns.xml";
        ContentPayloadValidation.ValidateEnvelope(context, "scnet.furniture-designs-xml-v1", entry,
            "application/xml", ["designCount"]);
        var expectedCount = ContentPackageManifest.GetRequiredInt32(context.Manifest.Metadata, "designCount",
            "manifest.metadata");
        if (expectedCount is < 1 or > 1024)
        {
            throw new ContentPackageException("Furniture metadata.designCount must be 1-1024.");
        }

        try
        {
            using var stream = context.OpenEntry(entry);
            using var reader =
                XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
            var document = XDocument.Load(reader, LoadOptions.None);
            if (document.Root?.Name != "FurnitureDesigns")
            {
                throw new ContentPackageException("Furniture payload root must be FurnitureDesigns.");
            }

            var indexes = new HashSet<int>();
            var designs = document.Root.Elements().ToArray();
            foreach (var design in designs)
            {
                var name = design.Attribute("Name")?.Value;
                if (design.Name != "Values" || design.Name.Namespace != XNamespace.None ||
                    name is null || !int.TryParse(name, System.Globalization.NumberStyles.None,
                        System.Globalization.CultureInfo.InvariantCulture, out var index) || index < 0 ||
                    index.ToString(System.Globalization.CultureInfo.InvariantCulture) != name ||
                    !indexes.Add(index) ||
                    design.Attributes().Any(attribute => attribute.Name.Namespace != XNamespace.None) ||
                    design.DescendantsAndSelf().Any(element => element.Name.Namespace != XNamespace.None))
                {
                    throw new ContentPackageException("Furniture payload contains an invalid design entry.");
                }
            }

            if (designs.Length != expectedCount)
            {
                throw new ContentPackageException("Furniture metadata.designCount does not match the payload.");
            }
        }
        catch (XmlException exception)
        {
            throw new ContentPackageException("Furniture payload is invalid XML.", exception);
        }
    }
}
