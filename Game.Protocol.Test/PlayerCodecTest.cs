using Engine.Core.Mathematics;

using Game.Protocol.Characters;
using Game.Protocol.Connection;
using Game.Protocol.Entities;
using Game.Protocol.Serialization;

namespace Game.Protocol.Test;

public sealed class PlayerCodecTest
{
    private static readonly CharacterTerrainHit _hit = new(
        new Ray3(new Vector3(1, 2, 3), Vector3.UnitZ),
        42,
        2,
        1.5f,
        new Point3(-17, 64, 15),
        3
    );

    public static TheoryData<ComponentCharacterPackage> Messages => new()
    {
        new CharacterStatePackage
        {
            ControllerSessionId = 7, PackageChangeFlag = (ComponentCharacterPackage.ChangFlag)255,
            LookAngles = Vector2.One,
            ChildLookAngles = Vector2.One, Position = new Vector3(-17, 64, 15), Rotation = Quaternion.Identity,
            Velocity = Vector3.UnitX, LadderValue = 10, Sneaking = true
        },
        new CharacterPlacePackage
        {
            ControllerSessionId = 7, PlacementRequestId = 42, PlacementCell = new Point3(-17, 64, 15),
            PlacementExpectedValue = 100, NetInteractRay = _hit.Ray, NetInteractRaycast = _hit
        },
        new CharacterAimPackage { ControllerSessionId = 7, AimEvent = SyncAction.Complete, NetAimRay = _hit.Ray },
        new CharacterDigPackage
        {
            ControllerSessionId = 7, DigEvent = SyncAction.Complete, DigRequestId = 42, DigExpectedValue = 100,
            NetDigRay = _hit.Ray, NetDigRaycast = _hit
        },
        new CharacterHitPackage
            { ControllerSessionId = 7, BodyId = 42, HitPosition = Vector3.One, HitDirection = Vector3.UnitX },
        new CharacterFlightPackage { ControllerSessionId = 7, IsCreativeFly = true },
        new CharacterReadyPackage { ControllerSessionId = 7 }, new CharacterRestartPackage { ControllerSessionId = 7 },
        new CharacterExperiencePackage { ControllerSessionId = 7, Count = 2, PlaySound = true, Level = 3.5f },
        new CharacterDropPackage { ControllerSessionId = 7 },
        new CharacterDragDropPackage
            { ControllerSessionId = 7, InventoryID = 10, ActiveSlot = 3, HitPosition = Vector3.One, Count = 4 },
        new CharacterStatisticsPackage { ControllerSessionId = 7, Stat = [1, 2, 3] },
        new CharacterPositionPackage
            { ControllerSessionId = 7, Position = new Vector3(-17, 64, 15), Velocity = Vector3.UnitY }
    };

    [Theory]
    [MemberData(nameof(Messages))]
    public void EachActionUsesItsOwnRecordWithExplicitIdentity(ComponentCharacterPackage message)
    {
        var encoded = Encode(message);
        var reader = encoded;
        var restored =
            Assert.IsAssignableFrom<ComponentCharacterPackage>(ProtocolMessageCodec.Decode(reader));
        Assert.Equal(message.GetType(), restored.GetType());
        Assert.Equal(message.Type, restored.Type);
        Assert.Equal(7, restored.ControllerSessionId);

        Assert.Equal(encoded, Encode(restored));
    }

    [Fact]
    public void ReadyMessageUsesTheDeclaredUnionAndIdentityField()
    {
        var encoded = Encode(new CharacterReadyPackage { ControllerSessionId = 7 });
        var reader = new MessagePack.MessagePackReader(encoded);
        Assert.Equal(2, reader.ReadArrayHeader());
        Assert.Equal(WireTestData.Tag<CharacterReadyPackage>(), reader.ReadInt32());
        Assert.Equal(1, reader.ReadArrayHeader());
        Assert.Equal(7, reader.ReadByte());
        Assert.True(reader.End);
    }

    [Fact]
    public void UnknownActionsAreRejected()
    {
        Assert.Throws<InvalidDataException>(() =>
            ProtocolMessageCodec.Decode(new byte[] { 0x92, 0xcd, 0x7f, 0xff, 0x90 })
        );
    }

    private static byte[] Encode(ComponentCharacterPackage message)
    {
        var writer = ProtocolMessageCodec.Encode(message);
        return writer;
    }
}
