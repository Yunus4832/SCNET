using EntitySystem.TemplatesDatabase;

using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public class ComponentPlayerPackage : IPackage
{
    [Flags]
    private enum ChangFlag : byte
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

    private int _bodyId;

    private ChangFlag _changFlag;

    public Vector2 ChildLookAngles;

    public Vector2 LookAngles;

    public Vector2 LookOrder;

    private int _activeSlot;

    private AimEvent _aimEvent;

    private int _count;

    private DigEvent _digEvent;

    private byte _fromPlayerId;

    private Vector3 _hitDirection;

    private Vector3 _hitPosition;

    private InteractEvent _interactEvent;

    private int _inventoryID;

    private bool _isCreativeFly;

    private int _ladderValue;

    private float _level;

    private Ray3? _netAimRay;

    private Ray3? _netDigRay;

    private TerrainRaycastResult? _netDigRaycast;

    private Ray3 _netInteractRay;

    private TerrainRaycastResult? _netInteractRaycast;

    private PlayerData? _playerData;

    private bool _playSound;

    private bool _sneaking;

    private ValuesDictionary? _stat;

    private PlayerAction _type;

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
        _type = type;
        _playerData = player.PlayerData;
        switch (type)
        {
            case PlayerAction.BodyUpdate:
                var body = player.ComponentBody.ParentBody;
                if (body != null)
                {
                    _changFlag |= ChangFlag.ParentBodyChange;
                    if (body.Locomotion is { SendLookAngles: not null })
                    {
                        _changFlag |= ChangFlag.LookAnglesChange;
                        LookAngles = body.Locomotion.SendLookAngles.Value;
                        body.Locomotion.SendLookAngles = null;
                    }

                    if (player.ComponentLocomotion.SendLookAngles.HasValue)
                    {
                        _changFlag |= ChangFlag.ChildLookAnglesChange;
                        ChildLookAngles = player.ComponentLocomotion.SendLookAngles.Value;
                        player.ComponentLocomotion.SendLookAngles = null;
                    }
                }
                else
                {
                    body = player.ComponentBody;
                    if (player.ComponentLocomotion.SendLookAngles.HasValue)
                    {
                        _changFlag |= ChangFlag.LookAnglesChange;
                        LookAngles = player.ComponentLocomotion.SendLookAngles.Value;
                        player.ComponentLocomotion.SendLookAngles = null;
                    }

                    if (player.ComponentLocomotion.LadderValue.HasValue)
                    {
                        _changFlag |= ChangFlag.LadderChange;
                        _ladderValue = player.ComponentLocomotion.LadderValue.Value;
                    }

                    if (body.CrouchFactor.UncloseTo(body.TargetCrouchFactor))
                    {
                        _changFlag |= ChangFlag.SneakChange;
                        _sneaking = body.TargetCrouchFactor.CloseTo(1f);
                    }
                }

                if (body.SendPosition.HasValue)
                {
                    Position = body.SendPosition.Value;
                    _changFlag |= ChangFlag.PositionChange;
                    body.SendPosition = null;
                }

                if (body.SendRotation.HasValue)
                {
                    _changFlag |= ChangFlag.RotationChange;
                    Rotation = body.SendRotation.Value;
                    body.SendRotation = null;
                }

                if (body.SendVelocity.HasValue)
                {
                    _changFlag |= ChangFlag.VelocityChange;
                    Velocity = body.SendVelocity.Value;
                    body.SendVelocity = null;
                }

                break;
            case PlayerAction.DigEvent:
                _digEvent = player.CurDigEventItem.DigEvent;
                _netDigRay = player.CurDigEventItem.NetDigRay;
                _netDigRaycast = player.CurDigEventItem.NetDigRaycast;
                break;
            case PlayerAction.AimEvent:
                _aimEvent = player.CurAimEventItem.AimEvent;
                _netAimRay = player.CurAimEventItem.NetAim;
                break;
            case PlayerAction.InteractEvent:
                _interactEvent = player.CurInteractEventItem.InteractEvent;
                _netInteractRay = player.CurInteractEventItem.NetInteractRay;
                _netInteractRaycast = player.CurInteractEventItem.NetPlaceRaycast;
                break;
            case PlayerAction.CreativeFlyChange:
                _isCreativeFly = player.ComponentLocomotion.IsCreativeFlyEnabled;
                break;
            case PlayerAction.SyncStat:
                _stat = new ValuesDictionary();
                player.PlayerStats.Save(_stat);
                break;
            case PlayerAction.PositionSet:
                Position = player.ComponentBody.Position;
                Velocity = player.ComponentBody.Velocity;
                break;
        }
    }

    //Restart
    public ComponentPlayerPackage(PlayerData playerData, PlayerAction type)
    {
        _type = type;
        _playerData = playerData;
    }

    public ComponentPlayerPackage(PlayerData playerData, int count, bool playSound, float level)
    {
        _playerData = playerData;
        _type = PlayerAction.AddExperience;
        _count = count;
        _playSound = playSound;
        _level = level;
    }

    public ComponentPlayerPackage(PlayerData playerData, int inventoryID, int slotIndex, Vector3 position, int count)
    {
        _playerData = playerData;
        _type = PlayerAction.DragDrop;
        _inventoryID = inventoryID;
        _activeSlot = slotIndex;
        _hitPosition = position;
        _count = count;
    }

    public ComponentPlayerPackage(ComponentPlayer player, ComponentBody body, Vector3 hitPosition, Vector3 hitDirection)
    {
        _playerData = player.PlayerData;
        _bodyId = body.Entity.EntityId;
        _hitPosition = hitPosition;
        _hitDirection = hitDirection;
        _type = PlayerAction.Hit;
    }


    public void WriteData(PackageStreamWriter writer)
    {
        if (_playerData != null)
        {
            writer.Write(_playerData.ClientId);
        }

        writer.WriteEnum(_type);
        switch (_type)
        {
            case PlayerAction.BodyUpdate:
                writer.WriteEnum(_changFlag);
                if (_changFlag.HasFlag(ChangFlag.ParentBodyChange))
                {
                    if (_changFlag.HasFlag(ChangFlag.LookAnglesChange))
                    {
                        writer.Write(LookAngles);
                    }

                    if (_changFlag.HasFlag(ChangFlag.ChildLookAnglesChange))
                    {
                        writer.Write(ChildLookAngles);
                    }
                }
                else
                {
                    if (_changFlag.HasFlag(ChangFlag.LookAnglesChange))
                    {
                        writer.Write(LookAngles);
                    }
                }

                if (_changFlag.HasFlag(ChangFlag.PositionChange))
                {
                    writer.Write(Position);
                }

                if (_changFlag.HasFlag(ChangFlag.RotationChange))
                {
                    writer.Write(Rotation);
                }

                if (_changFlag.HasFlag(ChangFlag.VelocityChange))
                {
                    writer.Write(Velocity);
                }

                if (_changFlag.HasFlag(ChangFlag.LadderChange))
                {
                    writer.Write(_ladderValue);
                }

                if (_changFlag.HasFlag(ChangFlag.SneakChange))
                {
                    writer.Write(_sneaking);
                }

                break;
            case PlayerAction.InteractEvent:
                writer.WriteEnum(_interactEvent);
                writer.Write(_netInteractRay);
                writer.Write(_netInteractRaycast);
                break;
            case PlayerAction.AimEvent:
                writer.WriteEnum(_aimEvent);
                writer.Write(_netAimRay);
                break;
            case PlayerAction.DigEvent:
                writer.WriteEnum(_digEvent);
                writer.Write(_netDigRay);
                writer.Write(_netDigRaycast);
                break;
            case PlayerAction.Hit:
                writer.Write(_bodyId);
                writer.Write(_hitPosition);
                writer.Write(_hitDirection);
                break;
            case PlayerAction.AddExperience:
                writer.Write(_count);
                writer.Write(_playSound);
                writer.Write(_level);
                break;
            case PlayerAction.DragDrop:
                writer.Write(_inventoryID);
                writer.Write(_activeSlot);
                writer.Write(_hitPosition);
                writer.Write(_count);
                break;
            case PlayerAction.CreativeFlyChange:
                writer.Write(_isCreativeFly);
                break;
            case PlayerAction.SyncStat:
                if (_stat != null)
                {
                    writer.WriteBuff(_stat.ToMessagePack());
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
        _fromPlayerId = reader.ReadByte();
        _type = reader.ReadEnum<PlayerAction>();
        switch (_type)
        {
            case PlayerAction.BodyUpdate:
                _changFlag = reader.ReadEnum<ChangFlag>();
                if (_changFlag.HasFlag(ChangFlag.ParentBodyChange))
                {
                    if (_changFlag.HasFlag(ChangFlag.LookAnglesChange))
                    {
                        LookAngles = reader.ReadVector2();
                    }

                    if (_changFlag.HasFlag(ChangFlag.ChildLookAnglesChange))
                    {
                        ChildLookAngles = reader.ReadVector2();
                    }
                }
                else
                {
                    if (_changFlag.HasFlag(ChangFlag.LookAnglesChange))
                    {
                        LookAngles = reader.ReadVector2();
                    }
                }

                if (_changFlag.HasFlag(ChangFlag.PositionChange))
                {
                    Position = reader.ReadVector3();
                }

                if (_changFlag.HasFlag(ChangFlag.RotationChange))
                {
                    Rotation = reader.ReadQuaternion();
                }

                if (_changFlag.HasFlag(ChangFlag.VelocityChange))
                {
                    Velocity = reader.ReadVector3();
                }

                if (_changFlag.HasFlag(ChangFlag.LadderChange))
                {
                    _ladderValue = reader.ReadInt32();
                }

                if (_changFlag.HasFlag(ChangFlag.SneakChange))
                {
                    _sneaking = reader.ReadBoolean();
                }

                break;
            case PlayerAction.InteractEvent:
                _interactEvent = reader.ReadEnum<InteractEvent>();
                _netInteractRay = reader.ReadRay3();
                _netInteractRaycast = reader.ReadTerrainRaycastResultNullable();
                break;
            case PlayerAction.AimEvent:
                _aimEvent = reader.ReadEnum<AimEvent>();
                _netAimRay = reader.ReadRay3Nullable();
                break;
            case PlayerAction.DigEvent:
                _digEvent = reader.ReadEnum<DigEvent>();
                _netDigRay = reader.ReadRay3Nullable();
                _netDigRaycast = reader.ReadTerrainRaycastResultNullable();
                break;
            case PlayerAction.Hit:
                _bodyId = reader.ReadInt32();
                _hitPosition = reader.ReadVector3();
                _hitDirection = reader.ReadVector3();
                break;
            case PlayerAction.AddExperience:
                _count = reader.ReadInt32();
                _playSound = reader.ReadBoolean();
                _level = reader.ReadSingle();
                break;
            case PlayerAction.DragDrop:
                _inventoryID = reader.ReadInt32();
                _activeSlot = reader.ReadInt32();
                _hitPosition = reader.ReadVector3();
                _count = reader.ReadInt32();
                break;
            case PlayerAction.CreativeFlyChange:
                _isCreativeFly = reader.ReadBoolean();
                break;
            case PlayerAction.SyncStat:
                var messagePack = reader.ReadBuff();
                _stat = new ValuesDictionary();
                _stat.ApplyOverridesUseMessagePack(messagePack);
                break;
            case PlayerAction.PositionSet:
                Position = reader.ReadVector3();
                Velocity = reader.ReadVector3();
                break;
        }
    }

    public void Handle(NetNode netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        _playerData = project.FindSubsystem<SubsystemPlayers>(true)!
            .FindPlayerData(playerData => playerData.ClientId == _fromPlayerId);
        if (!NeedHandleMainPlayer && _playerData is { IsMainPlayer: true } && _type != PlayerAction.AddExperience)
        {
            return;
        }

        if (From != null && (_playerData == null || _playerData.ClientId != From.ID))
        {
            return;
        }

        switch (_type)
        {
            case PlayerAction.BodyUpdate:
                PlayerEvent(player =>
                {
                    ComponentBody body;
                    if (_changFlag.HasFlag(ChangFlag.ParentBodyChange))
                    {
                        if (player.ComponentBody.ParentBody != null)
                        {
                            body = player.ComponentBody.ParentBody;
                            if (_changFlag.HasFlag(ChangFlag.LookAnglesChange))
                            {
                                var loco = body.Locomotion;
                                loco?.NetLookAngles.SetNext(LookAngles);
                            }

                            if (_changFlag.HasFlag(ChangFlag.ChildLookAnglesChange))
                            {
                                player.ComponentBody.Locomotion?.NetLookAngles.SetNext(ChildLookAngles);
                            }
                        }
                        else
                        {
                            body = player.ComponentBody;
                        }
                    }
                    else
                    {
                        body = player.ComponentBody;
                        if (_changFlag.HasFlag(ChangFlag.LookAnglesChange))
                        {
                            player.ComponentLocomotion.NetLookAngles.SetNext(LookAngles);
                        }
                    }

                    if (_changFlag.HasFlag(ChangFlag.VelocityChange))
                    {
                        body.NetVelocity.SetNext(Velocity);
                    }

                    if (body.Locomotion != null)
                    {
                        if (_changFlag.HasFlag(ChangFlag.LadderChange))
                        {
                            body.Locomotion.LadderValue = _ladderValue;
                        }
                        else
                        {
                            body.Locomotion.LadderValue = null;
                        }
                    }

                    if (_changFlag.HasFlag(ChangFlag.PositionChange))
                    {
                        body.NetPosition.SetNext(Position);
                    }

                    if (_changFlag.HasFlag(ChangFlag.RotationChange))
                    {
                        body.NetRotation.SetNext(Rotation);
                    }

                    if (_changFlag.HasFlag(ChangFlag.SneakChange))
                    {
                        body.IsSneaking = _sneaking;
                    }
                });
                break;
            case PlayerAction.InteractEvent:
                PlayerEvent(player =>
                {
                    player.AddInteractEvent(_interactEvent, _netInteractRay, _netInteractRaycast);
                    if (isServer)
                    {
                        Except = From;
                        netNode.QueuePackage(this);
                    }
                });
                break;
            case PlayerAction.AimEvent:
                PlayerEvent(player =>
                {
                    player.AddAimEvent(_aimEvent, _netAimRay);
                    if (isServer)
                    {
                        Except = From;
                        netNode.QueuePackage(this);
                    }
                });
                break;
            case PlayerAction.DigEvent:
                PlayerEvent(player =>
                {
                    player.AddDigEvent(_digEvent, _netDigRay, _netDigRaycast);
                    if (isServer)
                    {
                        Except = From;
                        netNode.QueuePackage(this);
                    }
                });
                break;
            case PlayerAction.CreativeFlyChange:
                PlayerEvent(player =>
                {
                    player.ComponentLocomotion.IsCreativeFlyEnabled = _isCreativeFly;
                    if (isServer)
                    {
                        Except = From;
                        netNode.QueuePackage(this);
                    }
                });
                break;
            case PlayerAction.Hit:
                PlayerEvent(player =>
                {
                    project.FindEntityById(_bodyId, entity =>
                    {
                        var body = entity.FindComponent<ComponentBody>();
                        if (body != null)
                        {
                            player.ComponentMiner.Hit(body, _hitPosition, _hitDirection);
                        }

                        if (!isServer)
                        {
                            return;
                        }

                        Except = From;
                        netNode.QueuePackage(this);
                    });
                });
                break;
            case PlayerAction.IntoPlaying:
                PlayerEvent(player => { player.ComponentHealth.IsInvulnerable = false; });
                break;
            case PlayerAction.Restart:
                PlayerEvent(player => { player.PlayerData.ReadyToRestart = true; });
                break;
            case PlayerAction.AddExperience:
                PlayerEvent(player =>
                {
                    player.ComponentLevel.NetAddExperience(_count, _playSound);
                    player.PlayerData.Level = _level;
                });
                break;
            case PlayerAction.Drop:
                PlayerEvent(player => { player.DoDrop(); });
                break;
            case PlayerAction.DragDrop:
                // 我去，别这样搞啊，回调地狱可是会很头疼的！！！！！
                PlayerEvent(player =>
                {
                    project.FindSubsystem<SubsystemInventories>(true)!.FindInventoryById(_inventoryID, inventory =>
                    {
                        // 丢弃背包内的物品，不是活动栏的
                        player.ViewWidget.NetDragDrop(_hitPosition,
                            new InventoryDragData { Inventory = inventory, SlotIndex = _activeSlot }, _count);
                    });
                });
                break;
            case PlayerAction.SyncStat:
                PlayerEvent(player =>
                {
                    if (_stat != null)
                    {
                        player.PlayerStats.Load(_stat);
                    }
                });
                break;
            case PlayerAction.PositionSet:
                PlayerEvent(player =>
                {
                    player.ComponentBody.Position = Position;
                    player.ComponentBody.Velocity = Velocity;
                });
                break;
        }
    }

    public void PlayerEvent(Action<ComponentPlayer>? action, Action? fail = null)
    {
        if (_playerData is { ComponentPlayer: not null })
        {
            action?.Invoke(_playerData.ComponentPlayer);
        }
        else
        {
            fail?.Invoke();
        }
    }
}
