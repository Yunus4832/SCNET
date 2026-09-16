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
        service.SetFavorite("Favorite", "example.com", true);
        service.RecordConnectionAttempt("Recent", "example.com", DateTimeOffset.UtcNow);

        Assert.NotNull(saved);
        Assert.Single(saved.MyServers);
        Assert.Single(saved.Favorites);
        Assert.Single(saved.RecentServers);
        Assert.Equal("example.com:28887", saved.MyServers[0].Address);
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
    public void SubscriptionUrlsMustBeUnique()
    {
        var service = new ServerDirectoryService(new ServerDirectoryState(), 28887, _ => { });
        service.AddSubscription(new ServerSourceSubscription { Name = "One", ApiUrl = "https://example.com/list" });

        Assert.Throws<ArgumentException>(() => service.AddSubscription(new ServerSourceSubscription
        {
            Name = "Two",
            ApiUrl = "https://example.com/list"
        }));
    }
}
