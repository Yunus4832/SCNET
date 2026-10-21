using Engine.Core;

using Game.Protocol.Connection;
using Game.Protocol.Entities;
using Game.Protocol.Players;
using Game.Protocol.Serialization;

namespace Game.Protocol.Test;

public sealed class PlayerCodecTest
{
    private static readonly PlayerTerrainHit _hit = new(
        new Ray3(new Vector3(1, 2, 3), Vector3.UnitZ),
        42,
        2,
        1.5f,
        new Point3(-17, 64, 15),
        3
    );

    public static TheoryData<ComponentPlayerPackage> Messages => new()
    {
        new PlayerStatePackage
        {
            FromPlayerId = 7, PackageChangeFlag = (ComponentPlayerPackage.ChangFlag)255, LookAngles = Vector2.One,
            ChildLookAngles = Vector2.One, Position = new Vector3(-17, 64, 15), Rotation = Quaternion.Identity,
            Velocity = Vector3.UnitX, LadderValue = 10, Sneaking = true
        },
        new PlayerInteractionPackage
        {
            FromPlayerId = 7, InteractEvent = InteractionMessageKind.Place, PlacementRequestId = 42,
            PlacementCell = new Point3(-17, 64, 15), PlacementExpectedValue = 100, NetInteractRay = _hit.Ray,
            NetInteractRaycast = _hit
        },
        new PlayerAimPackage { FromPlayerId = 7, AimEvent = SyncAction.Complete, NetAimRay = _hit.Ray },
        new PlayerDigPackage
        {
            FromPlayerId = 7, DigEvent = SyncAction.Complete, DigRequestId = 42, DigExpectedValue = 100,
            NetDigRay = _hit.Ray, NetDigRaycast = _hit
        },
        new PlayerHitPackage { FromPlayerId = 7, BodyId = 42, HitPosition = Vector3.One, HitDirection = Vector3.UnitX },
        new PlayerFlightPackage { FromPlayerId = 7, IsCreativeFly = true },
        new PlayerReadyPackage { FromPlayerId = 7 }, new PlayerRestartPackage { FromPlayerId = 7 },
        new PlayerExperiencePackage { FromPlayerId = 7, Count = 2, PlaySound = true, Level = 3.5f },
        new PlayerDropPackage { FromPlayerId = 7 },
        new PlayerDragDropPackage
            { FromPlayerId = 7, InventoryID = 10, ActiveSlot = 3, HitPosition = Vector3.One, Count = 4 },
        new PlayerStatisticsPackage { FromPlayerId = 7, Stat = [1, 2, 3] },
        new PlayerPositionPackage { FromPlayerId = 7, Position = new Vector3(-17, 64, 15), Velocity = Vector3.UnitY }
    };

    [Theory]
    [MemberData(nameof(Messages))]
    public void EachActionUsesItsOwnRecordWithExplicitIdentity(ComponentPlayerPackage message)
    {
        var encoded = Encode(message);
        using var reader = new ProtocolReader(encoded);
        var restored = Assert.IsAssignableFrom<ComponentPlayerPackage>(ProtocolMessageCodec.Read(message.ID, reader));
        Assert.Equal(message.GetType(), restored.GetType());
        Assert.Equal(message.Type, restored.Type);
        Assert.Equal(7, restored.FromPlayerId);
        Assert.Equal(reader.BaseStream.Length, reader.BaseStream.Position);
        Assert.Equal(encoded, Encode(restored));
    }

    [Fact]
    public void ReadyIdentityAndActionHaveAnExplicitTwoByteWireLayout()
    {
        var encoded = Encode(new PlayerReadyPackage { FromPlayerId = 7 });
        Assert.Equal(new byte[] { 7, 6 }, ProtocolFrameCodec.DecodeFrame(encoded));
    }

    [Fact]
    public void UnknownActionsAreRejected()
    {
        using var writer = new ProtocolWriter();
        writer.Write((byte)7);
        writer.Write((byte)255);
        using var reader = new ProtocolReader(writer.Data(CompressionPolicy.None));
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Read((byte)PackageType.ComponentPlayer, reader));
    }

    private static byte[] Encode(ComponentPlayerPackage message)
    {
        using var writer = new ProtocolWriter();
        ProtocolMessageCodec.Write(message, writer);
        return writer.Data(CompressionPolicy.None);
    }
}
