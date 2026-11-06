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
            ControllerClientId = 7, PackageChangeFlag = (ComponentCharacterPackage.ChangFlag)255,
            LookAngles = Vector2.One,
            ChildLookAngles = Vector2.One, Position = new Vector3(-17, 64, 15), Rotation = Quaternion.Identity,
            Velocity = Vector3.UnitX, LadderValue = 10, Sneaking = true
        },
        new CharacterPlacePackage
        {
            ControllerClientId = 7, PlacementRequestId = 42, PlacementCell = new Point3(-17, 64, 15),
            PlacementExpectedValue = 100, NetInteractRay = _hit.Ray, NetInteractRaycast = _hit
        },
        new CharacterAimPackage { ControllerClientId = 7, AimEvent = SyncAction.Complete, NetAimRay = _hit.Ray },
        new CharacterDigPackage
        {
            ControllerClientId = 7, DigEvent = SyncAction.Complete, DigRequestId = 42, DigExpectedValue = 100,
            NetDigRay = _hit.Ray, NetDigRaycast = _hit
        },
        new CharacterHitPackage
            { ControllerClientId = 7, BodyId = 42, HitPosition = Vector3.One, HitDirection = Vector3.UnitX },
        new CharacterFlightPackage { ControllerClientId = 7, IsCreativeFly = true },
        new CharacterReadyPackage { ControllerClientId = 7 }, new CharacterRestartPackage { ControllerClientId = 7 },
        new CharacterExperiencePackage { ControllerClientId = 7, LevelUps = 2, PlaySound = true, Level = 3.5f },
        new CharacterDropPackage { ControllerClientId = 7 },
        new CharacterDragDropPackage
            { ControllerClientId = 7, InventoryID = 10, ActiveSlot = 3, HitPosition = Vector3.One, Count = 4 },
        new CharacterStatisticsPackage { ControllerClientId = 7, Stat = [1, 2, 3] },
        new CharacterPositionPackage
            { ControllerClientId = 7, Position = new Vector3(-17, 64, 15), Velocity = Vector3.UnitY }
    };

    [Theory]
    [InlineData(float.NaN, 0)]
    [InlineData(0f, 0)]
    [InlineData(2f, -1)]
    [InlineData(2f, 3)]
    public void ExperienceStateRejectsInvalidLevelOrCrossingCount(float level, int levelUps)
    {
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Encode(
                new CharacterExperiencePackage
                {
                    Level = level, LevelUps = levelUps
                }
            )
        );
    }

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
        Assert.Equal(7, restored.ControllerClientId);

        Assert.Equal(encoded, Encode(restored));
    }

    [Fact]
    public void ReadyMessageUsesTheDeclaredUnionAndIdentityField()
    {
        var encoded = Encode(new CharacterReadyPackage { ControllerClientId = 7 });
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
