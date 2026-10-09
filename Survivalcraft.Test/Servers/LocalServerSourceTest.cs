using Game.Servers;

using ServerSource.Protocol;

namespace Survivalcraft.Test.Servers;

public sealed class LocalServerSourceTest
{
    [Fact]
    public async Task CatalogExposesOneLocalSourceAndSeparateLanSource()
    {
        var directory = new ServerDirectoryService(new ServerDirectoryState(), 28887, _ => { });
        directory.AddMyServer("Mine", "example.com");
        directory.AddFavorite("Mine", "example.com");
        using var httpClient = new HttpClient();
        var catalog = new ServerSourceCatalog(directory, new ServerSourceProtocolClient(httpClient),
            new ServerDiscoveryService());

        var sources = catalog.GetEnabledSources();
        var local = Assert.Single(sources, source => source.Kind == ServerSourceKind.Local);
        Assert.Single(sources, source => source.Kind == ServerSourceKind.Lan);
        var item = Assert.Single(await local.LoadAsync(CancellationToken.None));

        Assert.Equal("example.com:28887", item.Address);
        Assert.Equal(LocalServerTag.MyServer | LocalServerTag.Favorite, item.LocalTags);
    }
}
