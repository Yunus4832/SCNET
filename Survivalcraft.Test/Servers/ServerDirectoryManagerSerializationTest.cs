using Game;
using Game.Managers;
using Game.Servers;

namespace Survivalcraft.Test.Servers;

public sealed class ServerDirectoryManagerSerializationTest
{
    [Fact]
    public void RoundTripsLocalTagsAndInstalledSources()
    {
        var timestamp = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var state = new ServerDirectorySettings
        {
            LocalServers =
            [
                new StoredServerEntry
                {
                    Name = "Mine",
                    Address = "example.com:28887",
                    Tags = LocalServerTag.MyServer | LocalServerTag.Favorite | LocalServerTag.Recent,
                    UpdatedAt = timestamp
                }
            ],
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
        var document = ServerDirectoryManager.Write(state);
        var restored = ServerDirectoryManager.Read(document);

        var entry = Assert.Single(restored.LocalServers);
        Assert.Equal("Mine", entry.Name);
        Assert.Equal(LocalServerTag.MyServer | LocalServerTag.Favorite | LocalServerTag.Recent, entry.Tags);
        Assert.Equal(timestamp, entry.UpdatedAt);
        Assert.Equal("source-1", Assert.Single(restored.InstalledSources).RegistrationId);
        Assert.Equal("ServerDirectory", document.Name.LocalName);
        Assert.Null(document.Element("Lan"));
    }
}
