using Engine.Core.Mathematics;
using Engine.Core.Security;

using Game.Protocol.Blocks;
using Game.Protocol.Characters;
using Game.Protocol.Connection;
using Game.Protocol.Entities;
using Game.Protocol.Identity;
using Game.Protocol.Serialization;
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
            ClientList = new ClientListPackage
            {
                List =
                [
                    new ClientIdentity(1, _identity, _identity, new PlayerAddress(IdentityAddress.Encode(new byte[32])))
                ]
            },
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
    public void LargeOpaqueEntityPayloadPreservesSessionMetadata()
    {
        var message = new InitialWorldSnapshotPackage
        {
            Epoch = _identity,
            ClientList = new ClientListPackage
            {
                List =
                [
                    new ClientIdentity(1, _identity, _identity, new PlayerAddress(IdentityAddress.Encode(new byte[32])))
                ]
            },
            EntityData = Enumerable.Range(0, 4096).Select(index => (byte)index).ToArray()
        };
        var restored = RoundTrip(message);
        Assert.Equal(message.Epoch, restored.Epoch);
        Assert.Equal(message.ClientList.List, restored.ClientList.List);
        Assert.Equal(message.EntityData, restored.EntityData);
    }

    [Fact]
    public void BootstrapContainsWorldMetadataWithoutParsingGameProjectData()
    {
        var message = new BootstrapPackage
        {
            Epoch = _identity,
            World = new WorldMetadata("Test", 123, 0, 20),
            ClientList = new ClientListPackage
            {
                List =
                [
                    new ClientIdentity(1, _identity, _identity, new PlayerAddress(IdentityAddress.Encode(new byte[32])))
                ]
            },
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
        var characters = new[]
        {
            new CharacterSnapshot(
                [1, 2],
                new ClientIdentity(1, _identity, _identity, new PlayerAddress(IdentityAddress.Encode(new byte[32])))
            )
        };
        var summaries = new[] { new CharacterEntitySnapshot(_identity, 42, Vector3.One, 0) };
        EntityPackage message = type == EntityPackage.EventType.LoadOne
            ? new SingleEntitySnapshotPackage
                { EntityData = [5, 6], Characters = characters, CharacterEntities = summaries }
            : new EntitySnapshotPackage { EntityData = [5, 6], Characters = characters, CharacterEntities = summaries };
        var encoded = ProtocolMessageCodec.Encode(message);
        Assert.Equal(encoded, ProtocolMessageCodec.Encode(ProtocolMessageCodec.Decode(encoded)));
    }

    [Fact]
    public void DuplicateAndTruncatedPlayerEntityMetadataAreRejected()
    {
        var summary = new CharacterEntitySnapshot(_identity, 42, Vector3.One, 0);
        var message = new SingleEntitySnapshotPackage { CharacterEntities = [summary] };
        Assert.Throws<InvalidDataException>(() =>
            ProtocolMessageCodec.Encode(message with { CharacterEntities = [summary, summary] })
        );
        var payload = ProtocolMessageCodec.Encode(message);
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Decode(payload.AsMemory(0, payload.Length - 1)));
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
        var writer = ProtocolMessageCodec.Encode(message);
        var reader = writer;
        var restored = (T)ProtocolMessageCodec.Decode(reader);

        return restored;
    }
}
