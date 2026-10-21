using Engine.Core.Mathematics;

using Game.Protocol.Connection;
using Game.Protocol.Entities;
using Game.Protocol.Players;

namespace Game.Protocol.Serialization;

public static partial class ProtocolMessageCodec
{
    private static void WritePlayer(ComponentPlayerPackage message, ProtocolWriter writer)
    {
        writer.Write(message.FromPlayerId);
        writer.WriteEnum(message.Type);
        switch (message)
        {
            case PlayerStatePackage package:
                writer.WriteEnum(package.PackageChangeFlag);
                if (package.PackageChangeFlag.HasFlag(ComponentPlayerPackage.ChangFlag.ParentBodyChange))
                {
                    if (package.PackageChangeFlag.HasFlag(ComponentPlayerPackage.ChangFlag.LookAnglesChange))
                    {
                        writer.Write(package.LookAngles);
                    }

                    if (package.PackageChangeFlag.HasFlag(ComponentPlayerPackage.ChangFlag.ChildLookAnglesChange))
                    {
                        writer.Write(package.ChildLookAngles);
                    }
                }
                else
                {
                    if (package.PackageChangeFlag.HasFlag(ComponentPlayerPackage.ChangFlag.LookAnglesChange))
                    {
                        writer.Write(package.LookAngles);
                    }
                }

                if (package.PackageChangeFlag.HasFlag(ComponentPlayerPackage.ChangFlag.PositionChange))
                {
                    writer.Write(package.Position);
                }

                if (package.PackageChangeFlag.HasFlag(ComponentPlayerPackage.ChangFlag.RotationChange))
                {
                    writer.Write(package.Rotation);
                }

                if (package.PackageChangeFlag.HasFlag(ComponentPlayerPackage.ChangFlag.VelocityChange))
                {
                    writer.Write(package.Velocity);
                }

                if (package.PackageChangeFlag.HasFlag(ComponentPlayerPackage.ChangFlag.LadderChange))
                {
                    writer.Write(package.LadderValue);
                }

                if (package.PackageChangeFlag.HasFlag(ComponentPlayerPackage.ChangFlag.SneakChange))
                {
                    writer.Write(package.Sneaking);
                }

                break;
            case PlayerInteractionPackage package:
                writer.WriteEnum(package.InteractEvent);
                if (package.InteractEvent == InteractionMessageKind.Place)
                {
                    writer.Write(package.PlacementRequestId);
                    writer.Write(package.PlacementCell);
                    writer.Write(package.PlacementExpectedValue);
                }

                writer.Write(package.NetInteractRay);
                WritePlayerHit(package.NetInteractRaycast, writer);
                break;
            case PlayerAimPackage package:
                writer.WriteEnum(package.AimEvent);
                writer.Write(package.NetAimRay);
                break;
            case PlayerDigPackage package:
                writer.WriteEnum(package.DigEvent);
                writer.Write(package.DigRequestId);
                writer.Write(package.DigExpectedValue);
                writer.Write(package.NetDigRay);
                WritePlayerHit(package.NetDigRaycast, writer);
                break;
            case PlayerHitPackage package:
                writer.Write(package.BodyId);
                writer.Write(package.HitPosition);
                writer.Write(package.HitDirection);
                break;
            case PlayerExperiencePackage package:
                writer.Write(package.Count);
                writer.Write(package.PlaySound);
                writer.Write(package.Level);
                break;
            case PlayerDragDropPackage package:
                writer.Write(package.InventoryID);
                writer.Write(package.ActiveSlot);
                writer.Write(package.HitPosition);
                writer.Write(package.Count);
                break;
            case PlayerFlightPackage package:
                writer.Write(package.IsCreativeFly);
                break;
            case PlayerStatisticsPackage package:
                writer.WriteBuff(package.Stat);

                break;
            case PlayerPositionPackage package:
                writer.Write(package.Position);
                writer.Write(package.Velocity);
                break;
            case PlayerReadyPackage or PlayerRestartPackage or PlayerDropPackage:
                break;
            default: throw new ArgumentException("Unknown player message type.");
        }
    }

    private static ComponentPlayerPackage ReadPlayer(ProtocolReader reader)
    {
        var id = reader.ReadByte();
        var action = reader.ReadEnum<ComponentPlayerPackage.PlayerAction>();
        return action switch
        {
            ComponentPlayerPackage.PlayerAction.BodyUpdate => ReadPlayerMovement(reader, id),
            ComponentPlayerPackage.PlayerAction.InteractEvent => ReadPlayerInteraction(reader, id),
            ComponentPlayerPackage.PlayerAction.AimEvent => new PlayerAimPackage
                { FromPlayerId = id, AimEvent = reader.ReadEnum<SyncAction>(), NetAimRay = reader.ReadRay3Nullable() },
            ComponentPlayerPackage.PlayerAction.DigEvent => new PlayerDigPackage
            {
                FromPlayerId = id, DigEvent = reader.ReadEnum<SyncAction>(), DigRequestId = reader.ReadInt32(),
                DigExpectedValue = reader.ReadInt32(), NetDigRay = reader.ReadRay3Nullable(),
                NetDigRaycast = ReadPlayerHit(reader)
            },
            ComponentPlayerPackage.PlayerAction.Hit => new PlayerHitPackage
            {
                FromPlayerId = id, BodyId = reader.ReadInt32(), HitPosition = reader.ReadVector3(),
                HitDirection = reader.ReadVector3()
            },
            ComponentPlayerPackage.PlayerAction.CreativeFlyChange => new PlayerFlightPackage
                { FromPlayerId = id, IsCreativeFly = reader.ReadBoolean() },
            ComponentPlayerPackage.PlayerAction.IntoPlaying => new PlayerReadyPackage { FromPlayerId = id },
            ComponentPlayerPackage.PlayerAction.Restart => new PlayerRestartPackage { FromPlayerId = id },
            ComponentPlayerPackage.PlayerAction.AddExperience => new PlayerExperiencePackage
            {
                FromPlayerId = id, Count = reader.ReadInt32(), PlaySound = reader.ReadBoolean(),
                Level = reader.ReadSingle()
            },
            ComponentPlayerPackage.PlayerAction.Drop => new PlayerDropPackage { FromPlayerId = id },
            ComponentPlayerPackage.PlayerAction.DragDrop => new PlayerDragDropPackage
            {
                FromPlayerId = id, InventoryID = reader.ReadInt32(), ActiveSlot = reader.ReadInt32(),
                HitPosition = reader.ReadVector3(), Count = reader.ReadInt32()
            },
            ComponentPlayerPackage.PlayerAction.SyncStat => new PlayerStatisticsPackage
                { FromPlayerId = id, Stat = reader.ReadBuff() },
            ComponentPlayerPackage.PlayerAction.PositionSet => new PlayerPositionPackage
                { FromPlayerId = id, Position = reader.ReadVector3(), Velocity = reader.ReadVector3() },
            _ => throw new InvalidDataException("Unknown player action.")
        };
    }

    private static PlayerInteractionPackage ReadPlayerInteraction(ProtocolReader reader, byte id)
    {
        var action = reader.ReadEnum<InteractionMessageKind>();
        if (!Enum.IsDefined(action)) { throw new InvalidDataException("Unknown player interaction."); }

        var requestId = 0;
        var cell = default(Point3);
        var expected = 0;
        if (action == InteractionMessageKind.Place)
        {
            requestId = reader.ReadInt32();
            cell = reader.ReadPoint3();
            expected = reader.ReadInt32();
        }

        return new PlayerInteractionPackage
        {
            FromPlayerId = id, InteractEvent = action, PlacementRequestId = requestId, PlacementCell = cell,
            PlacementExpectedValue = expected, NetInteractRay = reader.ReadRay3(),
            NetInteractRaycast = ReadPlayerHit(reader)
        };
    }

    private static PlayerStatePackage ReadPlayerMovement(ProtocolReader reader, byte id)
    {
        var flags = reader.ReadEnum<ComponentPlayerPackage.ChangFlag>();
        var look = (flags & ComponentPlayerPackage.ChangFlag.LookAnglesChange) != 0 ? reader.ReadVector2() : default;
        var child = (flags &
                     (ComponentPlayerPackage.ChangFlag.ParentBodyChange |
                      ComponentPlayerPackage.ChangFlag.ChildLookAnglesChange)) ==
                    (ComponentPlayerPackage.ChangFlag.ParentBodyChange |
                     ComponentPlayerPackage.ChangFlag.ChildLookAnglesChange)
            ? reader.ReadVector2()
            : default;
        return new PlayerStatePackage
        {
            FromPlayerId = id,
            PackageChangeFlag = flags,
            LookAngles = look,
            ChildLookAngles = child,
            Position = (flags & ComponentPlayerPackage.ChangFlag.PositionChange) != 0 ? reader.ReadVector3() : default,
            Rotation = (flags & ComponentPlayerPackage.ChangFlag.RotationChange) != 0
                ? reader.ReadQuaternion()
                : default,
            Velocity = (flags & ComponentPlayerPackage.ChangFlag.VelocityChange) != 0 ? reader.ReadVector3() : default,
            LadderValue = (flags & ComponentPlayerPackage.ChangFlag.LadderChange) != 0 ? reader.ReadInt32() : default,
            Sneaking = (flags & ComponentPlayerPackage.ChangFlag.SneakChange) != 0 && reader.ReadBoolean()
        };
    }

    private static PlayerTerrainHit? ReadPlayerHit(ProtocolReader reader)
    {
        if (!reader.ReadBoolean()) { return null; }

        return new PlayerTerrainHit(
            reader.ReadRay3(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadSingle(),
            reader.ReadBlockPoint(),
            reader.ReadInt32()
        );
    }

    private static void WritePlayerHit(PlayerTerrainHit? hit, ProtocolWriter writer)
    {
        writer.Write(hit.HasValue);
        if (hit is not { } value) { return; }

        writer.Write(value.Ray);
        writer.Write(value.Value);
        writer.Write(value.CollisionBoxIndex);
        writer.Write(value.Distance);
        writer.WriteBlockPoint(value.Cell);
        writer.Write(value.Face);
    }
}
