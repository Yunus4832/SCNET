using Game.Servers;

namespace Survivalcraft.Test.Servers;

public sealed class ServerDirectoryServiceTest
{
    [Fact]
    public void LocalTagsShareOneEntryForTheSameNormalizedAddress()
    {
        ServerDirectoryState? saved = null;
        var service = new ServerDirectoryService(new ServerDirectoryState(), 28887, state => saved = state);

        var added = service.AddMyServer("Mine", "example.com");
        service.AddFavorite("Favorite", "example.com:28887");
        service.RecordConnectionAttempt("Recent", "example.com", DateTimeOffset.UtcNow);

        var entry = Assert.Single(saved!.LocalServers);
        Assert.Equal(added.Id, entry.Id);
        Assert.Equal("Mine", entry.Name);
        Assert.Equal("example.com:28887", entry.Address);
        Assert.Equal(LocalServerTag.MyServer | LocalServerTag.Favorite | LocalServerTag.Recent, entry.Tags);
    }

    [Fact]
    public void FavoriteTagCannotBeAddedTwice()
    {
        var service = new ServerDirectoryService(new ServerDirectoryState(), 28887, _ => { });

        service.AddFavorite("First", "example.com");

        Assert.Throws<ArgumentException>(() => service.AddFavorite("Second", "example.com:28887"));
        Assert.Single(service.Snapshot().LocalServers);
    }

    [Fact]
    public void RemovingLastTagDeletesEntryAndDeletingEntryRemovesAllTags()
    {
        var service = new ServerDirectoryService(new ServerDirectoryState(), 28887, _ => { });
        var favorite = service.AddFavorite("Favorite", "favorite.example");
        var combined = service.AddMyServer("Mine", "mine.example");
        service.AddFavorite("Mine", "mine.example");

        service.RemoveLocalTag(favorite.Id, LocalServerTag.Favorite);
        service.DeleteLocalServer(combined.Id);

        Assert.Empty(service.Snapshot().LocalServers);
    }

    [Fact]
    public void RecentLimitRemovesOnlyTheRecentTagFromOlderEntries()
    {
        var service = new ServerDirectoryService(new ServerDirectoryState(), 28887, _ => { });
        var start = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var favorite = service.AddFavorite("Favorite", "favorite.example");
        service.RecordConnectionAttempt("Favorite", "favorite.example", start);
        for (var index = 0; index < ServerDirectoryService.MaximumRecentServers; index++)
        {
            service.RecordConnectionAttempt($"Recent {index}", $"recent{index}.example", start.AddMinutes(index + 1));
        }

        var entries = service.Snapshot().LocalServers;
        Assert.Equal(ServerDirectoryService.MaximumRecentServers + 1, entries.Count);
        Assert.Equal(LocalServerTag.Favorite, entries.Single(entry => entry.Id == favorite.Id).Tags);
        Assert.Equal(ServerDirectoryService.MaximumRecentServers,
            entries.Count(entry => entry.Tags.HasFlag(LocalServerTag.Recent)));
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
