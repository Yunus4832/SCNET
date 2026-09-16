using Game.Servers;

namespace Survivalcraft.Test.Servers;

public sealed class ServerDirectoryServiceTest
{
    [Fact]
    public void SourcesKeepSameAddressAsIndependentEntries()
    {
        ServerDirectoryState? saved = null;
        var service = new ServerDirectoryService(new ServerDirectoryState(), 28887, state => saved = state);

        service.AddMyServer("Mine", "example.com");
        service.AddFavorite("Favorite", "example.com");
        service.RecordConnectionAttempt("Recent", "example.com", DateTimeOffset.UtcNow);

        Assert.NotNull(saved);
        Assert.Single(saved.MyServers);
        Assert.Single(saved.Favorites);
        Assert.Single(saved.RecentServers);
        Assert.Equal("example.com:28887", saved.MyServers[0].Address);
    }

    [Fact]
    public void FavoritesAreDeduplicatedByNormalizedAddress()
    {
        var service = new ServerDirectoryService(new ServerDirectoryState(), 28887, _ => { });

        service.AddFavorite("First", "example.com");

        Assert.Throws<ArgumentException>(() => service.AddFavorite("Second", "example.com:28887"));
        Assert.Single(service.Snapshot().Favorites);
    }

    [Fact]
    public void FavoriteAndRecentEntriesCanBeDeletedByTheirOwnIds()
    {
        var service = new ServerDirectoryService(new ServerDirectoryState(), 28887, _ => { });
        service.AddFavorite("Favorite", "favorite.example");
        service.RecordConnectionAttempt("Recent", "recent.example", DateTimeOffset.UtcNow);
        var snapshot = service.Snapshot();

        service.DeleteFavorite(Assert.Single(snapshot.Favorites).Id);
        service.DeleteRecentServer(Assert.Single(snapshot.RecentServers).Id);

        Assert.Empty(service.Snapshot().Favorites);
        Assert.Empty(service.Snapshot().RecentServers);
    }

    [Fact]
    public void RecentServersAreDeduplicatedAndMostRecentFirst()
    {
        var service = new ServerDirectoryService(new ServerDirectoryState(), 28887, _ => { });
        var earlier = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var later = earlier.AddMinutes(1);

        service.RecordConnectionAttempt("Old", "example.com:28887", earlier);
        service.RecordConnectionAttempt("Other", "other.example:28887", later);
        service.RecordConnectionAttempt("New", "example.com", later.AddMinutes(1));

        var recent = service.Snapshot().RecentServers;
        Assert.Equal(2, recent.Count);
        Assert.Equal("New", recent[0].Name);
        Assert.Equal("example.com:28887", recent[0].Address);
    }

    [Fact]
    public void InstalledSourceUrlsMustBeUnique()
    {
        var service = new ServerDirectoryService(new ServerDirectoryState(), 28887, _ => { });
        service.InstallSource(new InstalledServerSource { Name = "One", ApiUrl = "https://example.com/list" });

        Assert.Throws<ArgumentException>(() => service.InstallSource(new InstalledServerSource
        {
            Name = "Two",
            ApiUrl = "https://example.com/list"
        }));
    }
}
