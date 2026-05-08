namespace Game.NetWork.Packages;

public class ComponentBehaviorPackage : IPackage
{
    public enum EventType
    {
        ChaseBehavior,
        RandomPeck,
        RandomFeed,
        EatPickable,
        HumanRow,
        CreatureSound,
        DigInMud,
        FishOutOfWater
    }

    private EventType _eventType;

    private ushort _entityId;

    private bool _rowLeft;

    private bool _rowRight;

    private byte _type;

    public byte ID => (byte)PackageType.ComponentBehavior;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.Playing;


    public ComponentBehaviorPackage()
    {
    }

    public ComponentBehaviorPackage(ComponentFishOutOfWaterBehavior fishOutOfWaterBehavior, bool isBend)
    {
        _entityId = fishOutOfWaterBehavior.Entity.EntityId;
        _eventType = EventType.FishOutOfWater;
        _rowLeft = isBend;
    }

    public ComponentBehaviorPackage(ComponentChaseBehavior behavior, bool isChase)
    {
        _entityId = behavior.Entity.EntityId;
        _eventType = EventType.ChaseBehavior;
        _rowLeft = isChase;
    }

    public ComponentBehaviorPackage(ComponentCreatureSounds creatureSound, byte type, bool skip = false)
    {
        _entityId = creatureSound.Entity.EntityId;
        _eventType = EventType.CreatureSound;
        _type = type;
        _rowLeft = skip;
    }

    public ComponentBehaviorPackage(ComponentRandomPeckBehavior behavior, bool inRange)
    {
        _entityId = behavior.Entity.EntityId;
        _eventType = EventType.RandomPeck;
        _rowLeft = inRange;
    }

    public ComponentBehaviorPackage(ComponentRandomFeedBehavior behavior, bool inRange)
    {
        _entityId = behavior.Entity.EntityId;
        _eventType = EventType.RandomFeed;
        _rowLeft = inRange;
    }

    public ComponentBehaviorPackage(ComponentEatPickableBehavior behavior, bool inRange)
    {
        _entityId = behavior.Entity.EntityId;
        _eventType = EventType.EatPickable;
        _rowLeft = inRange;
    }

    public ComponentBehaviorPackage(ComponentDigInMudBehavior behavior, bool isDigIn)
    {
        _entityId = behavior.Entity.EntityId;
        _eventType = EventType.DigInMud;
        _rowLeft = isDigIn;
    }

    public ComponentBehaviorPackage(ComponentHumanModel componentModel)
    {
        _eventType = EventType.HumanRow;
        _entityId = componentModel.Entity.EntityId;
        _rowLeft = componentModel.RowLeft;
        _rowRight = componentModel.RowRight;
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.WriteEnum(_eventType);
        writer.Write(_entityId);
        switch (_eventType)
        {
            case EventType.FishOutOfWater:
            case EventType.DigInMud:
            case EventType.ChaseBehavior:
            case EventType.RandomFeed:
            case EventType.RandomPeck:
            case EventType.EatPickable:
                writer.Write(_rowLeft);
                break;
            case EventType.HumanRow:
                writer.Write(_rowLeft);
                writer.Write(_rowRight);
                break;
            case EventType.CreatureSound:
                writer.Write(_type);
                writer.Write(_rowLeft);
                break;
        }
    }

    public void ReadData(PackageStreamReader reader)
    {
        _eventType = reader.ReadEnum<EventType>();
        _entityId = reader.ReadUInt16();
        switch (_eventType)
        {
            case EventType.FishOutOfWater:
            case EventType.DigInMud:
            case EventType.ChaseBehavior:
            case EventType.RandomFeed:
            case EventType.RandomPeck:
            case EventType.EatPickable:
                _rowLeft = reader.ReadBoolean();
                break;
            case EventType.HumanRow:
                _rowLeft = reader.ReadBoolean();
                _rowRight = reader.ReadBoolean();
                break;
            case EventType.CreatureSound:
                _type = reader.ReadByte();
                _rowLeft = reader.ReadBoolean();
                break;
        }
    }

    public void Handle(ProjectNet projectNet, NetNode netNode, bool isServer)
    {
        projectNet.FindEntityById(_entityId, entity =>
        {
            switch (_eventType)
            {
                case EventType.ChaseBehavior:
                    var chaseBehavior = entity.FindComponent<ComponentChaseBehavior>();
                    if (chaseBehavior != null)
                    {
                        chaseBehavior.IsAttack = _rowLeft;
                    }
                    else
                    {
#if DEBUG
                        Log.Information(
                            $"处理行为ChaseBehavior失败，实体[{_entityId}]{entity.ValuesDictionary.DatabaseObject.Name}没有对应行为");
#endif
                    }

                    break;
                case EventType.RandomFeed:
                    var randomFeedBehavior = entity.FindComponent<ComponentRandomFeedBehavior>();
                    if (randomFeedBehavior != null)
                    {
                        randomFeedBehavior.IsFeed = _rowLeft;
                    }
                    else
                    {
#if DEBUG
                        Log.Information(
                            $"处理行为RandomFeed失败，实体[{_entityId}]{entity.ValuesDictionary.DatabaseObject.Name}没有对应行为");
#endif
                    }

                    break;

                case EventType.RandomPeck:
                    var peck = entity.FindComponent<ComponentRandomPeckBehavior>();
                    if (peck != null)
                    {
                        peck.IsFeed = _rowLeft;
                    }
                    else
                    {
#if DEBUG
                        Log.Information(
                            $"处理行为RandomPeck失败，实体[{_entityId}]{entity.ValuesDictionary.DatabaseObject.Name}没有对应行为");
#endif
                    }

                    break;
                case EventType.EatPickable:
                    var eatPickableBehavior = entity.FindComponent<ComponentEatPickableBehavior>();
                    eatPickableBehavior?.IsFeed = _rowLeft;
                    break;
                case EventType.DigInMud:
                    var digInMudBehavior = entity.FindComponent<ComponentDigInMudBehavior>();
                    digInMudBehavior?.IsDigIn = _rowLeft;
                    break;
                case EventType.FishOutOfWater:
                    var fishOutOfWaterBehavior = entity.FindComponent<ComponentFishOutOfWaterBehavior>();
                    fishOutOfWaterBehavior?.IsBend = _rowLeft;
                    break;
                case EventType.HumanRow:
                    var humanModel = entity.FindComponent<ComponentHumanModel>();
                    if (humanModel != null)
                    {
                        humanModel.HasData = true;
                        humanModel.RowLeft = _rowLeft;
                        humanModel.RowRight = _rowRight;
                        var random = new Random();
                        projectNet.FindSubsystem<SubsystemAudio>(true)!.PlayRandomSound("Audio/Rowing",
                            random.Float(0.4f, 0.6f), random.Float(-0.3f, 0.2f),
                            humanModel.ComponentCreature.ComponentBody.Position, 3f, true);
                        if (isServer)
                        {
                            Except = From;
                            netNode.QueuePackage(this);
                        }
                    }

                    break;
                case EventType.CreatureSound:
                    var creatureSound = entity.FindComponent<ComponentCreatureSounds>();
                    if (creatureSound != null)
                    {
                        switch (_type)
                        {
                            case 0:
                                creatureSound.PlayIdleSoundLogic(_rowLeft);
                                break;
                            case 1:
                                creatureSound.PlayPainSoundLogic();
                                break;
                            case 2:
                                creatureSound.PlayMoanSoundLogic();
                                break;
                            case 3:
                                creatureSound.PlaySneezeSoundLogic();
                                break;
                            case 4:
                                creatureSound.PlayCoughSoundLogic();
                                break;
                            case 5:
                                creatureSound.PlayPukeSoundLogic();
                                break;
                            case 6:
                                creatureSound.PlayAttackSoundLogic();
                                break;
                        }
                    }

                    break;
            }
        });
    }
}
