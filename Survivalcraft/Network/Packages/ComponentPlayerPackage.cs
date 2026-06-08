using EntitySystem.TemplatesDatabase;

using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public class ComponentPlayerPackage : IPackage
{
    [Flags]
    public enum ChangFlag : byte
    {
        None = 0,
        ParentBodyChange = 1,
        LookAnglesChange = 2,
        LadderChange = 4,
        PositionChange = 8,
        RotationChange = 16,
        VelocityChange = 32,
        ChildLookAnglesChange = 64,
        SneakChange = 128
    }

    public enum PlayerAction
    {
        BodyUpdate,
        InteractEvent,
        AimEvent,
        DigEvent,
        Hit,
        CreativeFlyChange,
        IntoPlaying,
        Restart,
        AddExperience,
        Drop,
        DragDrop,
        SyncStat,
        PositionSet
    }

    public int BodyId;

    public ChangFlag PackageChangeFlag;

    public Vector2 ChildLookAngles;

    public Vector2 LookAngles;

    public Vector2 LookOrder;

    public int ActiveSlot;

    public AimEvent AimEvent;

    public int Count;

    public DigEvent DigEvent;

    public byte FromPlayerId;

    public Vector3 HitDirection;

    public Vector3 HitPosition;

    public InteractEvent InteractEvent;

    public int InventoryID;

    public bool IsCreativeFly;

    public int LadderValue;

    public float Level;

    public Ray3? NetAimRay;

    public Ray3? NetDigRay;

    public TerrainRaycastResult? NetDigRaycast;

    public Ray3 NetInteractRay;

    public TerrainRaycastResult? NetInteractRaycast;

    public PlayerData? PlayerData;

    public bool PlaySound;

    public bool Sneaking;

    public ValuesDictionary? Stat;

    public PlayerAction Type;

    public bool NeedHandleMainPlayer = true;

    public Vector3 Position;

    public Quaternion Rotation;

    public Vector3 Velocity;

    public byte ID => (byte)PackageType.ComponentPlayer;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.ProjectLoaded;

    public ComponentPlayerPackage()
    {
    }

    public ComponentPlayerPackage(ComponentPlayer player, PlayerAction type)
    {
        Type = type;
        PlayerData = player.PlayerData;
        switch (type)
        {
            case PlayerAction.BodyUpdate:
                var body = player.ComponentBody.ParentBody;
                if (body != null)
                {
                    PackageChangeFlag |= ChangFlag.ParentBodyChange;
                    if (body.Locomotion is { SendLookAngles: not null })
                    {
                        PackageChangeFlag |= ChangFlag.LookAnglesChange;
                        LookAngles = body.Locomotion.SendLookAngles.Value;
                        body.Locomotion.SendLookAngles = null;
                    }

                    if (player.ComponentLocomotion.SendLookAngles.HasValue)
                    {
                        PackageChangeFlag |= ChangFlag.ChildLookAnglesChange;
                        ChildLookAngles = player.ComponentLocomotion.SendLookAngles.Value;
                        player.ComponentLocomotion.SendLookAngles = null;
                    }
                }
                else
                {
                    body = player.ComponentBody;
                    if (player.ComponentLocomotion.SendLookAngles.HasValue)
                    {
                        PackageChangeFlag |= ChangFlag.LookAnglesChange;
                        LookAngles = player.ComponentLocomotion.SendLookAngles.Value;
                        player.ComponentLocomotion.SendLookAngles = null;
                    }

                    if (player.ComponentLocomotion.LadderValue.HasValue)
                    {
                        PackageChangeFlag |= ChangFlag.LadderChange;
                        LadderValue = player.ComponentLocomotion.LadderValue.Value;
                    }

                    if (body.CrouchFactor.UncloseTo(body.TargetCrouchFactor))
                    {
                        PackageChangeFlag |= ChangFlag.SneakChange;
                        Sneaking = body.TargetCrouchFactor.CloseTo(1f);
                    }
                }

                if (body.SendPosition.HasValue)
                {
                    Position = body.SendPosition.Value;
                    PackageChangeFlag |= ChangFlag.PositionChange;
                    body.SendPosition = null;
                }

                if (body.SendRotation.HasValue)
                {
                    PackageChangeFlag |= ChangFlag.RotationChange;
                    Rotation = body.SendRotation.Value;
                    body.SendRotation = null;
                }

                if (body.SendVelocity.HasValue)
                {
                    PackageChangeFlag |= ChangFlag.VelocityChange;
                    Velocity = body.SendVelocity.Value;
                    body.SendVelocity = null;
                }

                break;
            case PlayerAction.DigEvent:
                DigEvent = player.CurDigEventItem.DigEvent;
                NetDigRay = player.CurDigEventItem.NetDigRay;
                NetDigRaycast = player.CurDigEventItem.NetDigRaycast;
                break;
            case PlayerAction.AimEvent:
                AimEvent = player.CurAimEventItem.AimEvent;
                NetAimRay = player.CurAimEventItem.NetAim;
                break;
            case PlayerAction.InteractEvent:
                InteractEvent = player.CurInteractEventItem.InteractEvent;
                NetInteractRay = player.CurInteractEventItem.NetInteractRay;
                NetInteractRaycast = player.CurInteractEventItem.NetPlaceRaycast;
                break;
            case PlayerAction.CreativeFlyChange:
                IsCreativeFly = player.ComponentLocomotion.IsCreativeFlyEnabled;
                break;
            case PlayerAction.SyncStat:
                Stat = new ValuesDictionary();
                player.PlayerStats.Save(Stat);
                break;
            case PlayerAction.PositionSet:
                Position = player.ComponentBody.Position;
                Velocity = player.ComponentBody.Velocity;
                break;
        }
    }

    public ComponentPlayerPackage(PlayerData playerData, PlayerAction type)
    {
        Type = type;
        PlayerData = playerData;
    }

    public ComponentPlayerPackage(PlayerData playerData, int count, bool playSound, float level)
    {
        PlayerData = playerData;
        Type = PlayerAction.AddExperience;
        Count = count;
        PlaySound = playSound;
        Level = level;
    }

    public ComponentPlayerPackage(PlayerData playerData, int inventoryID, int slotIndex, Vector3 position, int count)
    {
        PlayerData = playerData;
        Type = PlayerAction.DragDrop;
        InventoryID = inventoryID;
        ActiveSlot = slotIndex;
        HitPosition = position;
        Count = count;
    }

    public ComponentPlayerPackage(ComponentPlayer player, ComponentBody body, Vector3 hitPosition, Vector3 hitDirection)
    {
        PlayerData = player.PlayerData;
        BodyId = body.Entity.EntityId;
        HitPosition = hitPosition;
        HitDirection = hitDirection;
        Type = PlayerAction.Hit;
    }


    public void WriteData(PackageStreamWriter writer)
    {
        if (PlayerData != null)
        {
            writer.Write(PlayerData.ClientId);
        }

        writer.WriteEnum(Type);
        switch (Type)
        {
            case PlayerAction.BodyUpdate:
                writer.WriteEnum(PackageChangeFlag);
                if (PackageChangeFlag.HasFlag(ChangFlag.ParentBodyChange))
                {
                    if (PackageChangeFlag.HasFlag(ChangFlag.LookAnglesChange))
                    {
                        writer.Write(LookAngles);
                    }

                    if (PackageChangeFlag.HasFlag(ChangFlag.ChildLookAnglesChange))
                    {
                        writer.Write(ChildLookAngles);
                    }
                }
                else
                {
                    if (PackageChangeFlag.HasFlag(ChangFlag.LookAnglesChange))
                    {
                        writer.Write(LookAngles);
                    }
                }

                if (PackageChangeFlag.HasFlag(ChangFlag.PositionChange))
                {
                    writer.Write(Position);
                }

                if (PackageChangeFlag.HasFlag(ChangFlag.RotationChange))
                {
                    writer.Write(Rotation);
                }

                if (PackageChangeFlag.HasFlag(ChangFlag.VelocityChange))
                {
                    writer.Write(Velocity);
                }

                if (PackageChangeFlag.HasFlag(ChangFlag.LadderChange))
                {
                    writer.Write(LadderValue);
                }

                if (PackageChangeFlag.HasFlag(ChangFlag.SneakChange))
                {
                    writer.Write(Sneaking);
                }

                break;
            case PlayerAction.InteractEvent:
                writer.WriteEnum(InteractEvent);
                writer.Write(NetInteractRay);
                writer.Write(NetInteractRaycast);
                break;
            case PlayerAction.AimEvent:
                writer.WriteEnum(AimEvent);
                writer.Write(NetAimRay);
                break;
            case PlayerAction.DigEvent:
                writer.WriteEnum(DigEvent);
                writer.Write(NetDigRay);
                writer.Write(NetDigRaycast);
                break;
            case PlayerAction.Hit:
                writer.Write(BodyId);
                writer.Write(HitPosition);
                writer.Write(HitDirection);
                break;
            case PlayerAction.AddExperience:
                writer.Write(Count);
                writer.Write(PlaySound);
                writer.Write(Level);
                break;
            case PlayerAction.DragDrop:
                writer.Write(InventoryID);
                writer.Write(ActiveSlot);
                writer.Write(HitPosition);
                writer.Write(Count);
                break;
            case PlayerAction.CreativeFlyChange:
                writer.Write(IsCreativeFly);
                break;
            case PlayerAction.SyncStat:
                if (Stat != null)
                {
                    writer.WriteBuff(Stat.ToMessagePack());
                }

                break;
            case PlayerAction.PositionSet:
                writer.Write(Position);
                writer.Write(Velocity);
                break;
        }
    }

    public void ReadData(PackageStreamReader reader)
    {
        FromPlayerId = reader.ReadByte();
        Type = reader.ReadEnum<PlayerAction>();
        switch (Type)
        {
            case PlayerAction.BodyUpdate:
                PackageChangeFlag = reader.ReadEnum<ChangFlag>();
                if (PackageChangeFlag.HasFlag(ChangFlag.ParentBodyChange))
                {
                    if (PackageChangeFlag.HasFlag(ChangFlag.LookAnglesChange))
                    {
                        LookAngles = reader.ReadVector2();
                    }

                    if (PackageChangeFlag.HasFlag(ChangFlag.ChildLookAnglesChange))
                    {
                        ChildLookAngles = reader.ReadVector2();
                    }
                }
                else
                {
                    if (PackageChangeFlag.HasFlag(ChangFlag.LookAnglesChange))
                    {
                        LookAngles = reader.ReadVector2();
                    }
                }

                if (PackageChangeFlag.HasFlag(ChangFlag.PositionChange))
                {
                    Position = reader.ReadVector3();
                }

                if (PackageChangeFlag.HasFlag(ChangFlag.RotationChange))
                {
                    Rotation = reader.ReadQuaternion();
                }

                if (PackageChangeFlag.HasFlag(ChangFlag.VelocityChange))
                {
                    Velocity = reader.ReadVector3();
                }

                if (PackageChangeFlag.HasFlag(ChangFlag.LadderChange))
                {
                    LadderValue = reader.ReadInt32();
                }

                if (PackageChangeFlag.HasFlag(ChangFlag.SneakChange))
                {
                    Sneaking = reader.ReadBoolean();
                }

                break;
            case PlayerAction.InteractEvent:
                InteractEvent = reader.ReadEnum<InteractEvent>();
                NetInteractRay = reader.ReadRay3();
                NetInteractRaycast = reader.ReadTerrainRaycastResultNullable();
                break;
            case PlayerAction.AimEvent:
                AimEvent = reader.ReadEnum<AimEvent>();
                NetAimRay = reader.ReadRay3Nullable();
                break;
            case PlayerAction.DigEvent:
                DigEvent = reader.ReadEnum<DigEvent>();
                NetDigRay = reader.ReadRay3Nullable();
                NetDigRaycast = reader.ReadTerrainRaycastResultNullable();
                break;
            case PlayerAction.Hit:
                BodyId = reader.ReadInt32();
                HitPosition = reader.ReadVector3();
                HitDirection = reader.ReadVector3();
                break;
            case PlayerAction.AddExperience:
                Count = reader.ReadInt32();
                PlaySound = reader.ReadBoolean();
                Level = reader.ReadSingle();
                break;
            case PlayerAction.DragDrop:
                InventoryID = reader.ReadInt32();
                ActiveSlot = reader.ReadInt32();
                HitPosition = reader.ReadVector3();
                Count = reader.ReadInt32();
                break;
            case PlayerAction.CreativeFlyChange:
                IsCreativeFly = reader.ReadBoolean();
                break;
            case PlayerAction.SyncStat:
                var messagePack = reader.ReadBuff();
                Stat = new ValuesDictionary();
                Stat.ApplyOverridesUseMessagePack(messagePack);
                break;
            case PlayerAction.PositionSet:
                Position = reader.ReadVector3();
                Velocity = reader.ReadVector3();
                break;
        }
    }


    public void PlayerEvent(Action<ComponentPlayer>? action, Action? fail = null)
    {
        if (PlayerData is { ComponentPlayer: not null })
        {
            action?.Invoke(PlayerData.ComponentPlayer);
        }
        else
        {
            fail?.Invoke();
        }
    }
}
