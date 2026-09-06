using System.Xml.Linq;

using Game.Content;

namespace Survivalcraft.Test.Modding;

public sealed class ContentRepositorySettingsTest
{
    [Fact]
    public void RoundTripsAllFieldsWithoutDuplicatingContainer()
    {
        var root = new XElement("Settings", new XElement("Setting", "unrelated"));
        var repository = new ContentRepository { Name = "A & B", BaseUrl = "https://example.com/content", IsEnabled = false, Priority = 3 };
        ContentRepositorySettings.Write(root, [repository]);
        ContentRepositorySettings.Write(root, [repository]);
        Assert.Single(root.Elements("ContentRepositories"));
        Assert.Equal(repository, Assert.Single(ContentRepositorySettings.Read(XElement.Parse(root.ToString()))));
        Assert.Equal("unrelated", root.Element("Setting")!.Value);
    }

    [Fact]
    public void InvalidInputDoesNotReplaceExistingSettings()
    {
        var root = new XElement("Settings");
        var repository = new ContentRepository { Name = "A", BaseUrl = "https://example.com" };
        ContentRepositorySettings.Write(root, [repository]);
        var before = root.ToString();
        Assert.Throws<ArgumentException>(() => ContentRepositorySettings.Write(root, [repository with { Id = Guid.Empty }]));
        Assert.Equal(before, root.ToString());
        root.Element("ContentRepositories")!.Element("Repository")!.Attribute("Id")!.Remove();
        Assert.Throws<FormatException>(() => ContentRepositorySettings.Read(root));
    }
}
