using Game;
using Game.Content;
using Game.Managers;

namespace Survivalcraft.Test.Modding;

public sealed class ContentRepositoryManagerTest
{
    [Fact]
    public void GameInformationSourceIsExplicitAndClearsWhenDisabledOrDeleted()
    {
        var first = new ContentRepository { Name = "A", BaseUrl = "https://a.example" };
        var second = new ContentRepository { Name = "B", BaseUrl = "https://b.example" };
        var service = CreateManager([first, second]);
        Assert.Null(service.GameInformationSource);

        service.SelectGameInformationSource(first.Id);
        Assert.Equal(first.Id, service.GameInformationSource?.Id);
        service.SelectGameInformationSource(second.Id);
        Assert.Equal(second.Id, service.GameInformationSource?.Id);

        service.Edit(second with { IsEnabled = false });
        Assert.Null(service.GameInformationSource);
        Assert.Throws<InvalidOperationException>(() => service.SelectGameInformationSource(second.Id));

        service.SelectGameInformationSource(first.Id);
        service.Delete(first.Id);
        Assert.Null(service.GameInformationSource);
    }

    [Fact]
    public void SetOrderPersistsContiguousPriorities()
    {
        var saved = new ContentRepositorySettings();
        var first = new ContentRepository { Name = "A", BaseUrl = "https://a.example", Priority = 10 };
        var second = new ContentRepository { Name = "B", BaseUrl = "https://b.example", Priority = 20 };
        var service = CreateManager([first, second], settings => saved = settings);

        service.SetOrder([second.Id, first.Id]);

        Assert.Equal([second.Id, first.Id], service.Snapshot().Select(item => item.Id));
        Assert.Equal([0, 1], saved.Repositories.Select(item => item.Priority));
        Assert.Throws<ArgumentException>(() => service.SetOrder([first.Id, first.Id]));
    }

    [Fact]
    public void EditsPersistConfigurationChanges()
    {
        var saved = new ContentRepositorySettings();
        var service = CreateManager([], settings => saved = settings);
        var repository = new ContentRepository { Name = "A", BaseUrl = "https://a.example" };
        service.Add(repository);
        service.Edit(repository with { Name = "B", BaseUrl = "https://b.example", Priority = 2 });
        Assert.Equal(repository.Id, Assert.Single(saved.Repositories).Id);
        Assert.Equal("B", Assert.Single(service.Snapshot()).Name);
        service.Delete(repository.Id);
        Assert.Empty(saved.Repositories);
    }

    [Fact]
    public void FailedPersistenceLeavesConfigurationUnchanged()
    {
        var repository = new ContentRepository { Name = "A", BaseUrl = "https://a.example" };
        var service = CreateManager([repository], _ => throw new IOException("disk failure"));
        Assert.Throws<IOException>(() => service.Delete(repository.Id));
        Assert.Equal(repository, Assert.Single(service.Snapshot()));
    }

    private static ContentRepositoryManager CreateManager(IEnumerable<ContentRepository> repositories,
        Action<ContentRepositorySettings>? save = null) => new(new ContentRepositorySettings
        {
            Repositories = repositories.ToArray()
        }, save ?? (_ => { }));
}
