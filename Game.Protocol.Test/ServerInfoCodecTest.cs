using Game.Protocol.Connection;
using Game.Protocol.Serialization;

namespace Game.Protocol.Test;

public sealed class ServerInfoCodecTest
{
    [Fact]
    public void DiscoveryRequestContainsOnlyItsDiscriminator()
    {
        using var writer = new ProtocolWriter();
        ProtocolMessageCodec.Write(new ServerInfoPackage(true), writer);
        Assert.Equal(new byte[] { 1 }, ProtocolFrameCodec.DecodeFrame(writer.Data(CompressionPolicy.None)));
    }

    [Fact]
    public void ResponseCarriesOnlyPublishedRepositoryAndModDescriptors()
    {
        var repository = new ContentRepositoryDescriptor(Guid.NewGuid(), "https://example.com/content", 4);
        var requirement = new RequiredModPackage("example.mod", "1.2.3", new string('a', 64));
        var message = new ServerInfoPackage
        {
            ProtocolVersion = 1,
            ClientCount = 2,
            MaxPlayerCount = 32,
            GameMode = 0,
            Season = 2,
            TimeOfDay = 0.5f,
            TimeOfSeason = 0.25f,
            TemporaryRepositories = [repository],
            RequiredModProfile = new RequiredModProfile("server", [requirement])
        };
        using var writer = new ProtocolWriter();
        ProtocolMessageCodec.Write(message, writer);
        using var reader = new ProtocolReader(writer.Data(CompressionPolicy.None));
        var restored = Assert.IsType<ServerInfoPackage>(ProtocolMessageCodec.Read(message.ID, reader));
        Assert.Equal(message.ProtocolVersion, restored.ProtocolVersion);
        Assert.Equal(message.ClientCount, restored.ClientCount);
        Assert.Equal(message.MaxPlayerCount, restored.MaxPlayerCount);
        Assert.Equal(message.GameMode, restored.GameMode);
        Assert.Equal(message.Season, restored.Season);
        Assert.Equal(message.TimeOfDay, restored.TimeOfDay);
        Assert.Equal(message.TimeOfSeason, restored.TimeOfSeason);
        Assert.Equal(repository, Assert.Single(restored.TemporaryRepositories));
        Assert.Equal(requirement, Assert.Single(restored.RequiredModProfile!.Packages));
        Assert.Equal(reader.BaseStream.Length, reader.BaseStream.Position);
    }

    [Fact]
    public void GameValuesRemainOpaqueBytesInTheProtocol()
    {
        var message = new ServerInfoPackage { GameMode = 254, Season = 255 };
        using var writer = new ProtocolWriter();
        ProtocolMessageCodec.Write(message, writer);
        using var reader = new ProtocolReader(writer.Data(CompressionPolicy.None));
        var restored = Assert.IsType<ServerInfoPackage>(ProtocolMessageCodec.Read(message.ID, reader));
        Assert.Equal(message.GameMode, restored.GameMode);
        Assert.Equal(message.Season, restored.Season);
        Assert.Equal(reader.BaseStream.Length, reader.BaseStream.Position);
    }

    [Fact]
    public void MissingProfileFlagIsRejectedRatherThanTreatedAsAnOlderFormat()
    {
        using var writer = new ProtocolWriter();
        writer.Write(false);
        writer.Write(1);
        writer.Write((ushort)0);
        writer.Write((ushort)32);
        writer.Write((byte)0);
        writer.Write(0.5f);
        writer.Write((ushort)0);
        using var reader = new ProtocolReader(writer.Data(CompressionPolicy.None));
        Assert.Throws<EndOfStreamException>(() => ProtocolMessageCodec.Read((byte)PackageType.ServerInfo, reader));
    }
}
