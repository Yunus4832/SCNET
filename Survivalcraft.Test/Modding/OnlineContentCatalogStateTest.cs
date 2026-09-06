using Content.Packaging;

using Game.Content;

namespace Survivalcraft.Test.Modding;

public sealed class OnlineContentCatalogStateTest
{
    [Fact]
    public void MergesSourcesAndConflictsAcrossCatalogPages()
    {
        var hashA = new string('a', 64);
        var hashB = new string('b', 64);
        var first = Source("A", 0);
        var second = Source("B", 1);
        var state = new OnlineContentCatalogState();

        state.Append(Page(Entry(Version("1.0.0", hashA, first)), true), 1);
        state.Append(Page(Entry(Version("1.0.0", hashA, second), Version("1.0.0", hashB, second)), false), 2);

        var entry = Assert.Single(state.Filter(null, null, null, OnlineContentStatusFilter.All));
        Assert.Equal(2, entry.Versions.Count);
        Assert.All(entry.Versions, version => Assert.True(version.HasHashConflict));
        Assert.Equal(2, entry.Versions.Single(version => version.PackageHash == hashA).Sources.Count);
        Assert.False(state.HasMore);
        Assert.Equal(3, state.NextPageIndex);
    }

    [Fact]
    public void FiltersCachedProfileMissingRepositoryAndExactNavigation()
    {
        var cachedHash = new string('a', 64);
        var missingHash = new string('b', 64);
        var source = Source("A", 0);
        var entry = Entry(Version("2.0.0", cachedHash, source), Version("1.0.0", missingHash, source));
        var state = new OnlineContentCatalogState();
        state.SetReferences([cachedHash],
            [new OnlineContentReference(ContentPackageType.Mod, "example.mod", missingHash)],
            [new OnlineContentReference(ContentPackageType.Mod, "example.mod", cachedHash)]);
        state.Append(Page(entry, false), 1);

        Assert.Single(state.Filter("example.mod", ContentPackageType.Mod, source.RepositoryId,
            OnlineContentStatusFilter.Cached));
        Assert.Single(state.Filter(null, null, null, OnlineContentStatusFilter.Profile));
        Assert.Single(state.Filter(null, null, null, OnlineContentStatusFilter.CurrentSession));
        Assert.Single(state.Filter(null, null, null, OnlineContentStatusFilter.Missing));
        var found = state.Find(new OnlineContentNavigation(ContentPackageType.Mod, "example.mod", "1.0.0",
            missingHash));
        Assert.Equal(missingHash, found?.Version?.PackageHash);
        Assert.Equal([true, false], state.GetVersions(entry).Select(version => version.IsCached));
    }

    [Fact]
    public void RejectsIncompleteExactNavigationAndOutOfOrderPage()
    {
        var state = new OnlineContentCatalogState();

        Assert.Throws<ArgumentException>(() =>
            new OnlineContentNavigation(Version: "1.0.0").Normalize());
        Assert.Throws<ArgumentOutOfRangeException>(() => state.Append(Page(Entry(), false), 2));
    }

    [Fact]
    public void SeedsOfflineCacheAndMergesRemoteSourceByExactHash()
    {
        var hash = new string('a', 64);
        var state = new OnlineContentCatalogState();
        state.SetReferences([hash], [], []);
        state.SeedCache([
            new ContentPackageCacheEntry("/cache/a.scpkg", hash, ContentPackageType.Mod,
                "example.mod", "Example", "1.0.0", 100)
        ]);

        var offline = Assert.Single(state.Filter(null, null, null, OnlineContentStatusFilter.Cached));
        Assert.Empty(Assert.Single(offline.Versions).Sources);

        state.Append(Page(Entry(Version("1.0.0", hash, Source("A", 0))), false), 1);

        Assert.Single(Assert.Single(state.Filter(null, null, null, OnlineContentStatusFilter.All))
            .Versions.Single().Sources);
    }

    private static AggregatedContentPage Page(AggregatedContentEntry entry, bool hasMore)
    {
        return new AggregatedContentPage([entry], [], hasMore);
    }

    private static AggregatedContentEntry Entry(params AggregatedContentVersion[] versions)
    {
        return new AggregatedContentEntry(ContentPackageType.Mod, "example.mod", "Example", "Summary", versions);
    }

    private static AggregatedContentVersion Version(string version, string hash, params ContentCatalogSource[] sources)
    {
        return new AggregatedContentVersion(version, hash, 100, "example.scpkg", false, sources);
    }

    private static ContentCatalogSource Source(string name, int priority)
    {
        return new ContentCatalogSource(Guid.Empty, Guid.NewGuid(), name, priority, false,
            $"content-{name}", $"version-{name}", $"api/v1/packages/{name}");
    }
}
