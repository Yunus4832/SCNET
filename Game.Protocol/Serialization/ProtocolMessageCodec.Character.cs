using Engine.Core.Mathematics;

using Game.Protocol.Characters;
using Game.Protocol.Connection;
using Game.Protocol.Entities;

namespace Game.Protocol.Serialization;

public static partial class ProtocolMessageCodec
{
    private static void WritePlayer(ComponentCharacterPackage message, ProtocolWriter writer)
    {
        writer.Write(message.ControllerSessionId);
        writer.WriteEnum(message.Type);
        switch (message)
        {
            case CharacterStatePackage package:
                writer.WriteEnum(package.PackageChangeFlag);
                if (package.PackageChangeFlag.HasFlag(ComponentCharacterPackage.ChangFlag.ParentBodyChange))
                {
                    if (package.PackageChangeFlag.HasFlag(ComponentCharacterPackage.ChangFlag.LookAnglesChange))
                    {
                        writer.Write(package.LookAngles);
                    }

                    if (package.PackageChangeFlag.HasFlag(ComponentCharacterPackage.ChangFlag.ChildLookAnglesChange))
                    {
                        writer.Write(package.ChildLookAngles);
                    }
                }
                else
                {
                    if (package.PackageChangeFlag.HasFlag(ComponentCharacterPackage.ChangFlag.LookAnglesChange))
                    {
                        writer.Write(package.LookAngles);
                    }
                }

                if (package.PackageChangeFlag.HasFlag(ComponentCharacterPackage.ChangFlag.PositionChange))
                {
                    writer.Write(package.Position);
                }

                if (package.PackageChangeFlag.HasFlag(ComponentCharacterPackage.ChangFlag.RotationChange))
                {
                    writer.Write(package.Rotation);
                }

                if (package.PackageChangeFlag.HasFlag(ComponentCharacterPackage.ChangFlag.VelocityChange))
                {
                    writer.Write(package.Velocity);
                }

                if (package.PackageChangeFlag.HasFlag(ComponentCharacterPackage.ChangFlag.LadderChange))
                {
                    writer.Write(package.LadderValue);
                }

                if (package.PackageChangeFlag.HasFlag(ComponentCharacterPackage.ChangFlag.SneakChange))
                {
                    writer.Write(package.Sneaking);
                }

                break;
            case CharacterInteractionPackage package:
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
            case CharacterAimPackage package:
                writer.WriteEnum(package.AimEvent);
                writer.Write(package.NetAimRay);
                break;
            case CharacterDigPackage package:
                writer.WriteEnum(package.DigEvent);
                writer.Write(package.DigRequestId);
                writer.Write(package.DigExpectedValue);
                writer.Write(package.NetDigRay);
                WritePlayerHit(package.NetDigRaycast, writer);
                break;
            case CharacterHitPackage package:
                writer.Write(package.BodyId);
                writer.Write(package.HitPosition);
                writer.Write(package.HitDirection);
                break;
            case CharacterExperiencePackage package:
                writer.Write(package.Count);
                writer.Write(package.PlaySound);
                writer.Write(package.Level);
                break;
            case CharacterDragDropPackage package:
                writer.Write(package.InventoryID);
                writer.Write(package.ActiveSlot);
                writer.Write(package.HitPosition);
                writer.Write(package.Count);
                break;
            case CharacterFlightPackage package:
                writer.Write(package.IsCreativeFly);
                break;
            case CharacterStatisticsPackage package:
                writer.WriteBuff(package.Stat);

                break;
            case CharacterPositionPackage package:
                writer.Write(package.Position);
                writer.Write(package.Velocity);
                break;
            case CharacterReadyPackage or CharacterRestartPackage or CharacterDropPackage:
                break;
            default: throw new ArgumentException("Unknown player message type.");
        }
    }

    private static ComponentCharacterPackage ReadPlayer(ProtocolReader reader)
    {
        var id = reader.ReadByte();
        var action = reader.ReadEnum<ComponentCharacterPackage.CharacterAction>();
        return action switch
        {
            ComponentCharacterPackage.CharacterAction.BodyUpdate => ReadPlayerMovement(reader, id),
            ComponentCharacterPackage.CharacterAction.InteractEvent => ReadPlayerInteraction(reader, id),
            ComponentCharacterPackage.CharacterAction.AimEvent => new CharacterAimPackage
            {
                ControllerSessionId = id, AimEvent = reader.ReadEnum<SyncAction>(),
                NetAimRay = reader.ReadRay3Nullable()
            },
            ComponentCharacterPackage.CharacterAction.DigEvent => new CharacterDigPackage
            {
                ControllerSessionId = id, DigEvent = reader.ReadEnum<SyncAction>(), DigRequestId = reader.ReadInt32(),
                DigExpectedValue = reader.ReadInt32(), NetDigRay = reader.ReadRay3Nullable(),
                NetDigRaycast = ReadPlayerHit(reader)
            },
            ComponentCharacterPackage.CharacterAction.Hit => new CharacterHitPackage
            {
                ControllerSessionId = id, BodyId = reader.ReadInt32(), HitPosition = reader.ReadVector3(),
                HitDirection = reader.ReadVector3()
            },
            ComponentCharacterPackage.CharacterAction.CreativeFlyChange => new CharacterFlightPackage
                { ControllerSessionId = id, IsCreativeFly = reader.ReadBoolean() },
            ComponentCharacterPackage.CharacterAction.IntoPlaying => new CharacterReadyPackage
                { ControllerSessionId = id },
            ComponentCharacterPackage.CharacterAction.Restart => new CharacterRestartPackage
                { ControllerSessionId = id },
            ComponentCharacterPackage.CharacterAction.AddExperience => new CharacterExperiencePackage
            {
                ControllerSessionId = id, Count = reader.ReadInt32(), PlaySound = reader.ReadBoolean(),
                Level = reader.ReadSingle()
            },
            ComponentCharacterPackage.CharacterAction.Drop => new CharacterDropPackage { ControllerSessionId = id },
            ComponentCharacterPackage.CharacterAction.DragDrop => new CharacterDragDropPackage
            {
                ControllerSessionId = id, InventoryID = reader.ReadInt32(), ActiveSlot = reader.ReadInt32(),
                HitPosition = reader.ReadVector3(), Count = reader.ReadInt32()
            },
            ComponentCharacterPackage.CharacterAction.SyncStat => new CharacterStatisticsPackage
                { ControllerSessionId = id, Stat = reader.ReadBuff() },
            ComponentCharacterPackage.CharacterAction.PositionSet => new CharacterPositionPackage
                { ControllerSessionId = id, Position = reader.ReadVector3(), Velocity = reader.ReadVector3() },
            _ => throw new InvalidDataException("Unknown player action.")
        };
    }

    private static CharacterInteractionPackage ReadPlayerInteraction(ProtocolReader reader, byte id)
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

        return new CharacterInteractionPackage
        {
            ControllerSessionId = id, InteractEvent = action, PlacementRequestId = requestId, PlacementCell = cell,
            PlacementExpectedValue = expected, NetInteractRay = reader.ReadRay3(),
            NetInteractRaycast = ReadPlayerHit(reader)
        };
    }

    private static CharacterStatePackage ReadPlayerMovement(ProtocolReader reader, byte id)
    {
        var flags = reader.ReadEnum<ComponentCharacterPackage.ChangFlag>();
        var look = (flags & ComponentCharacterPackage.ChangFlag.LookAnglesChange) != 0 ? reader.ReadVector2() : default;
        var child = (flags &
                     (ComponentCharacterPackage.ChangFlag.ParentBodyChange |
                      ComponentCharacterPackage.ChangFlag.ChildLookAnglesChange)) ==
                    (ComponentCharacterPackage.ChangFlag.ParentBodyChange |
                     ComponentCharacterPackage.ChangFlag.ChildLookAnglesChange)
            ? reader.ReadVector2()
            : default;
        return new CharacterStatePackage
        {
            ControllerSessionId = id,
            PackageChangeFlag = flags,
            LookAngles = look,
            ChildLookAngles = child,
            Position = (flags & ComponentCharacterPackage.ChangFlag.PositionChange) != 0
                ? reader.ReadVector3()
                : default,
            Rotation = (flags & ComponentCharacterPackage.ChangFlag.RotationChange) != 0
                ? reader.ReadQuaternion()
                : default,
            Velocity = (flags & ComponentCharacterPackage.ChangFlag.VelocityChange) != 0
                ? reader.ReadVector3()
                : default,
            LadderValue =
                (flags & ComponentCharacterPackage.ChangFlag.LadderChange) != 0 ? reader.ReadInt32() : default,
            Sneaking = (flags & ComponentCharacterPackage.ChangFlag.SneakChange) != 0 && reader.ReadBoolean()
        };
    }

    private static CharacterTerrainHit? ReadPlayerHit(ProtocolReader reader)
    {
        if (!reader.ReadBoolean()) { return null; }

        return new CharacterTerrainHit(
            reader.ReadRay3(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadSingle(),
            reader.ReadBlockPoint(),
            reader.ReadInt32()
        );
    }

    private static void WritePlayerHit(CharacterTerrainHit? hit, ProtocolWriter writer)
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
