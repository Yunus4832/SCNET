using Game.Protocol.Connection;
using Game.Protocol.Serialization;

namespace Game.Protocol.Test;

public sealed class ServerInfoCodecTest
{
    [Fact]
    public void DiscoveryRequestContainsOnlyItsDiscriminator()
    {
        var encoded = ProtocolMessageCodec.Encode(new ServerInfoRequestPackage());
        var reader = new MessagePack.MessagePackReader(encoded);
        Assert.Equal(2, reader.ReadArrayHeader());
        Assert.Equal(WireTestData.Tag<ServerInfoRequestPackage>(), reader.ReadInt32());
        Assert.Equal(0, reader.ReadArrayHeader());
        Assert.True(reader.End);
    }

    [Fact]
    public void ResponseCarriesOnlyPublishedRepositoryAndModDescriptors()
    {
        var repository = new ContentRepositoryDescriptor(Guid.NewGuid(), "https://example.com/content", 4);
        var requirement = new RequiredModPackage("example.mod", "1.2.3", new string('a', 64));
        var message = new ServerInfoResponsePackage
        {
            ProtocolVersion = 1, ClientCount = 2, MaxPlayerCount = 32, GameMode = 0, Season = 2, TimeOfDay = 0.5f,
            TimeOfSeason = 0.25f, TemporaryRepositories = [repository],
            RequiredModProfile = new RequiredModProfile("server", [requirement])
        };
        var writer = ProtocolMessageCodec.Encode(message);
        var reader = writer;
        var restored = Assert.IsType<ServerInfoResponsePackage>(ProtocolMessageCodec.Decode(reader));
        Assert.Equal(message.ProtocolVersion, restored.ProtocolVersion);
        Assert.Equal(message.ClientCount, restored.ClientCount);
        Assert.Equal(message.MaxPlayerCount, restored.MaxPlayerCount);
        Assert.Equal(message.GameMode, restored.GameMode);
        Assert.Equal(message.Season, restored.Season);
        Assert.Equal(message.TimeOfDay, restored.TimeOfDay);
        Assert.Equal(message.TimeOfSeason, restored.TimeOfSeason);
        Assert.Equal(repository, Assert.Single(restored.TemporaryRepositories));
        Assert.Equal(requirement, Assert.Single(restored.RequiredModProfile!.Packages));
    }

    [Fact]
    public void GameValuesRemainOpaqueBytesInTheProtocol()
    {
        var message = new ServerInfoResponsePackage { GameMode = 254, Season = 255 };
        var writer = ProtocolMessageCodec.Encode(message);
        var reader = writer;
        var restored = Assert.IsType<ServerInfoResponsePackage>(ProtocolMessageCodec.Decode(reader));
        Assert.Equal(message.GameMode, restored.GameMode);
        Assert.Equal(message.Season, restored.Season);
    }

    [Fact]
    public void TruncatedDiscoveryResponseIsRejected()
    {
        var payload = ProtocolMessageCodec.Encode(new ServerInfoResponsePackage());
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Decode(payload.AsMemory(0, payload.Length - 1)));
    }
}
