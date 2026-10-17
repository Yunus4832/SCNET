using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace Content.Packaging.Test;

public sealed class WorldPayloadCodecTest
{
    [Theory]
    [InlineData(false, "")]
    [InlineData(true, "")]
    [InlineData(false, "<ExtensionData><Values Name=\"example.mod\"><Value Name=\"enabled\" Type=\"bool\" Value=\"True\" /></Values></ExtensionData>")]
    [InlineData(true, "<ExtensionData><Values Name=\"example.mod\"><Value Name=\"enabled\" Type=\"bool\" Value=\"True\" /></Values></ExtensionData>")]
    public void RoundTripPreservesOptionalRegistryAndExtensionData(bool includeRegistry, string extension)
    {
        var registry = includeRegistry
            ? "<BlockRegistry><Block Id=\"example.mod:stone\" Index=\"300\" /></BlockRegistry>"
            : string.Empty;
        var project = Encoding.UTF8.GetBytes(
            "<Project Version=\"SCNET-1\" Guid=\"9e9a67f8-79df-4d05-8cfa-61bd8095661e\" Name=\"GameProject\">" +
            "<Subsystems /><Entities />" + registry +
            extension + "</Project>");
        using var package = Write(project);

        Assert.Equal(ContentPackageType.World, ContentPackageReader.Inspect(package).Manifest.Type);
        package.Position = 0;
        using var archive = new ZipArchive(package, ZipArchiveMode.Read);
        using var input = archive.GetEntry("payload/world/Project.xml")!.Open();
        using var restored = new MemoryStream();
        input.CopyTo(restored);
        Assert.Equal(project, restored.ToArray());
    }

    [Theory]
    [InlineData("<Subsystems /><Entities /><BlockRegistry /><Unknown />")]
    [InlineData("<Subsystems /><Entities /><BlockRegistry /><BlockRegistry />")]
    [InlineData("<Subsystems /><Entities /><BlockRegistry /><ExtensionData /><ExtensionData />")]
    [InlineData("<Subsystems /><Subsystems /><Entities /><BlockRegistry />")]
    [InlineData("<Subsystems /><Entities /><Entities /><BlockRegistry />")]
    [InlineData("<Subsystems /><BlockRegistry />")]
    [InlineData("<Subsystems /><Entities /><BlockRegistry><Block xmlns=\"urn:unexpected\" /></BlockRegistry>")]
    public void RejectsMissingDuplicateUnknownOrNamespacedProjectNodes(string children)
    {
        var project = Encoding.UTF8.GetBytes(
            "<Project Version=\"SCNET-1\" Guid=\"9e9a67f8-79df-4d05-8cfa-61bd8095661e\" Name=\"GameProject\">" +
            children + "</Project>");

        Assert.Throws<ContentPackageException>(() =>
        {
            using var package = Write(project);
        });
    }

    private static MemoryStream Write(byte[] project)
    {
        var manifest = new ContentPackageManifest(ContentPackageManifest.CurrentFormatVersion,
            ContentPackageType.World, "7e96a4f3-fae8-4727-9fcb-9d35c2fc08b6", "World", "Summary", "Description",
            "1.0.0", new ContentPackagePayload("scnet.world-v1", "payload/world/Project.xml", "application/xml"),
            JsonSerializer.SerializeToElement(new
            {
                projectFormat = "scnet-project-xml-v1",
                regionsDirectory = "payload/world/Regions"
            }));
        var output = new MemoryStream();
        try
        {
            ContentPackageWriter.Write(output, manifest,
            [
                new ContentPackageWriteEntry("payload/world/Project.xml", project.Length,
                    () => new MemoryStream(project, writable: false))
            ]);
            output.Position = 0;
            return output;
        }
        catch
        {
            output.Dispose();
            throw;
        }
    }
}
