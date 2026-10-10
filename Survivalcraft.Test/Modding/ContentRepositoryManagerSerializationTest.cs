using System.Xml.Linq;

using Game;
using Game.Content;
using Game.Managers;

namespace Survivalcraft.Test.Modding;

public sealed class ContentRepositoryManagerSerializationTest
{
    [Fact]
    public void RoundTripsAllFields()
    {
        var repository = new ContentRepository
        { Name = "A & B", BaseUrl = "https://example.com/content", Priority = 3 };
        var document = ContentRepositoryManager.Write(new ContentRepositorySettings
        {
            GameInformationSourceId = repository.Id,
            Repositories = [repository]
        });

        Assert.Equal("ContentRepositories", document.Name.LocalName);
        var settings = ContentRepositoryManager.Read(XElement.Parse(document.ToString()));
        Assert.Equal(repository.Id, settings.GameInformationSourceId);
        Assert.Equal(repository, Assert.Single(settings.Repositories));
    }

    [Fact]
    public void InvalidInputIsRejectedBeforeWriting()
    {
        var repository = new ContentRepository { Name = "A", BaseUrl = "https://example.com" };
        Assert.Throws<ArgumentException>(() => ContentRepositoryManager.Write(new ContentRepositorySettings
        {
            Repositories = [repository with { Id = Guid.Empty }]
        }));

        var document = ContentRepositoryManager.Write(new ContentRepositorySettings
        {
            Repositories = [repository]
        });
        document.Element("Repository")!.Attribute("Id")!.Remove();
        Assert.Throws<FormatException>(() => ContentRepositoryManager.Read(document));
    }
}
