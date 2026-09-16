using System.Xml.Linq;

using Game.Servers;

namespace Survivalcraft.Test.Servers;

public sealed class ServerDirectorySettingsTest
{
    [Fact]
    public void RoundTripsAllIndependentSources()
    {
        var timestamp = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var state = new ServerDirectoryState
        {
            MyServers = [CreateServer("Mine", 0, timestamp)],
            Favorites = [CreateServer("Favorite", 0, timestamp)],
            RecentServers = [CreateServer("Recent", 0, timestamp)],
            InstalledSources =
            [
                new InstalledServerSource
                {
                    Name = "Public",
                    ApiUrl = "https://example.com/servers",
                    RegistrationId = "source-1"
                }
            ]
        };
        var document = new XElement("Settings");

        ServerDirectorySettings.Write(document, state, 28887);
        var restored = ServerDirectorySettings.Read(document, 28887);

        Assert.Equal("Mine", Assert.Single(restored.MyServers).Name);
        Assert.Equal("Favorite", Assert.Single(restored.Favorites).Name);
        Assert.Equal("Recent", Assert.Single(restored.RecentServers).Name);
        Assert.Equal("source-1", Assert.Single(restored.InstalledSources).RegistrationId);
    }

    private static StoredServerEntry CreateServer(string name, int order, DateTimeOffset timestamp)
    {
        return new StoredServerEntry
        {
            Name = name,
            Address = "example.com:28887",
            Order = order,
            UpdatedAt = timestamp
        };
    }
}
