using Engine.Core.Mathematics;
using Engine.Core.Security;

using Game.Protocol.Blocks;
using Game.Protocol.Characters;
using Game.Protocol.Connection;
using Game.Protocol.Entities;
using Game.Protocol.Identity;
using Game.Protocol.Serialization;
using Game.Protocol.Teams;
using Game.Protocol.World;

namespace Game.Protocol.Test;

public sealed class WorldSnapshotCodecTest
{
    private static readonly Guid _identity = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Fact]
    public void SnapshotCarriesOpaqueDataAndExplicitPlayerEntityMetadata()
    {
        var sample = new InitialWorldSnapshotPackage
        {
            Epoch = _identity,
            ClientList = new ClientPackage(
                1,
                _identity,
                _identity,
                new PlayerAddress(IdentityAddress.Encode(new byte[32]))
            ),
            Characters = [new byte[] { 1, 2, 3 }],
            EntityData = [4, 5, 6],
            CharacterEntities = [new CharacterEntitySnapshot(_identity, 42, new Vector3(-100, 80, 200), 12)]
        };
        var restored = RoundTrip(sample);
        Assert.Equal(sample.Epoch, restored.Epoch);
        Assert.Equal(sample.EntityData, restored.EntityData);
        Assert.Equal(sample.Characters[0], restored.Characters[0]);
        Assert.Equal(sample.CharacterEntities[0], Assert.Single(restored.CharacterEntities));
    }

    [Fact]
    public void ExplicitMetadataPrecedesTheCapturedLargeEntityPayload()
    {
        var frame = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "initial-snapshot.bin"));
        var original = ProtocolFrameCodec.DecodeFrame(frame);
        var message = new InitialWorldSnapshotPackage
        {
            Epoch = _identity,
            ClientList = new ClientPackage(
                1,
                _identity,
                _identity,
                new PlayerAddress(IdentityAddress.Encode(new byte[32]))
            ),
            EntityData = Enumerable.Range(0, 4096).Select(index => (byte)index).ToArray()
        };
        using var writer = new ProtocolWriter();
        writer.Write((byte)0x88);
        writer.Write(message.ID);
        ProtocolMessageCodec.Write(message, writer);
        var payload = ProtocolFrameCodec.DecodeFrame(writer.Data(CompressionPolicy.None));
        // Add the new subject-address field independently of the codec under test.
        const int addressOffset = 2 + 17 + 1 + 1 + 17 + 17;
        var updated = original[..addressOffset]
            .Concat(new byte[] { IdentityAddress.EncodedLength })
            .Concat(System.Text.Encoding.ASCII.GetBytes(IdentityAddress.Encode(new byte[32])))
            .Concat(new byte[] { (byte)ClientState.NotConnected })
            .Concat(original[addressOffset..])
            .ToArray();
        var payloadOffset = updated.Length - 4096 - sizeof(int);
        var expected = updated[..payloadOffset].Concat(new byte[2]).Concat(updated[payloadOffset..]).ToArray();
        Assert.Equal(expected, payload);
    }

    [Fact]
    public void BootstrapContainsWorldMetadataWithoutParsingGameProjectData()
    {
        var message = new BootstrapPackage
        {
            Epoch = _identity,
            World = new WorldMetadata("Test", 123, 0, 20),
            ClientList = new ClientPackage(
                [new ClientIdentity(1, _identity, _identity, new PlayerAddress(IdentityAddress.Encode(new byte[32])))]
            ),
            BlockMapping = [new BlockIndexEntry(ResourceId.Parse("example:block"), 300)],
            ProjectData = [1, 2, 3],
            TextureData = [4, 5]
        };
        var restored = RoundTrip(message);
        Assert.Equal(message.World, restored.World);
        Assert.Equal(message.BlockMapping[0], Assert.Single(restored.BlockMapping));
        Assert.Equal(message.ProjectData, restored.ProjectData);
        Assert.Equal(message.TextureData, restored.TextureData);
        Assert.Equal(message.ClientList.List[0], Assert.Single(restored.ClientList.List));
    }

    [Theory]
    [InlineData(EntityPackage.EventType.LoadOne)]
    [InlineData(EntityPackage.EventType.LoadList)]
    public void EntityMessagesCarrySnapshotsRatherThanRuntimeEntities(EntityPackage.EventType type)
    {
        var message = new EntityPackage
        {
            Type = type,
            EntityData = [5, 6],
            Characters =
            [
                new CharacterSnapshot(
                    [1, 2],
                    new ClientIdentity(1, _identity, _identity, new PlayerAddress(IdentityAddress.Encode(new byte[32])))
                )
            ],
            CharacterEntities = [new CharacterEntitySnapshot(_identity, 42, Vector3.One, 0)]
        };
        var restored = RoundTrip(message);
        Assert.Equal(message.EntityData, restored.EntityData);
        Assert.Equal(message.Characters[0].Data, Assert.Single(restored.Characters).Data);
        Assert.Equal(message.Characters[0].Client, restored.Characters[0].Client);
        Assert.Equal(message.CharacterEntities[0], Assert.Single(restored.CharacterEntities));
    }

    [Fact]
    public void DuplicateAndTruncatedPlayerEntityMetadataAreRejected()
    {
        var summary = new CharacterEntitySnapshot(_identity, 42, Vector3.One, 0);
        var message = new EntityPackage
            { Type = EntityPackage.EventType.LoadOne, CharacterEntities = [summary, summary] };
        using var writer = new ProtocolWriter();
        ProtocolMessageCodec.Write(message, writer);
        using var reader = new ProtocolReader(writer.Data(CompressionPolicy.None));
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Read(message.ID, reader));
        var bytes = ProtocolFrameCodec.DecodeFrame(writer.Data(CompressionPolicy.None));
        using var truncated = new ProtocolReader(new MemoryStream(bytes[..^1]));
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Read(message.ID, truncated));
    }

    [Fact]
    public void GameAndUiEnumsAreNotDefinedByTheProtocol()
    {
        var assembly = typeof(GameProtocol).Assembly;
        Assert.DoesNotContain(
            assembly.GetTypes(),
            type =>
                type.Name is "GameMode" or "CharacterSex" or "Season" or "DragMode"
        );
        Assert.Equal(assembly, typeof(ClientState).Assembly);
    }

    private static T RoundTrip<T>(T message) where T : IProtocolMessage
    {
        using var writer = new ProtocolWriter();
        ProtocolMessageCodec.Write(message, writer);
        using var reader = new ProtocolReader(writer.Data(CompressionPolicy.None));
        var restored = (T)ProtocolMessageCodec.Read(message.ID, reader);
        Assert.Equal(reader.BaseStream.Length, reader.BaseStream.Position);
        return restored;
    }
}
