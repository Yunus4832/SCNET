using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

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

    public EventType PackageEventType;

    public int EntityId;

    public bool RowLeft;

    public bool RowRight;

    public byte Type;

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
        EntityId = fishOutOfWaterBehavior.Entity.EntityId;
        PackageEventType = EventType.FishOutOfWater;
        RowLeft = isBend;
    }

    public ComponentBehaviorPackage(ComponentChaseBehavior behavior, bool isChase)
    {
        EntityId = behavior.Entity.EntityId;
        PackageEventType = EventType.ChaseBehavior;
        RowLeft = isChase;
    }

    public ComponentBehaviorPackage(ComponentCreatureSounds creatureSound, byte type, bool skip = false)
    {
        EntityId = creatureSound.Entity.EntityId;
        PackageEventType = EventType.CreatureSound;
        Type = type;
        RowLeft = skip;
    }

    public ComponentBehaviorPackage(ComponentRandomPeckBehavior behavior, bool inRange)
    {
        EntityId = behavior.Entity.EntityId;
        PackageEventType = EventType.RandomPeck;
        RowLeft = inRange;
    }

    public ComponentBehaviorPackage(ComponentRandomFeedBehavior behavior, bool inRange)
    {
        EntityId = behavior.Entity.EntityId;
        PackageEventType = EventType.RandomFeed;
        RowLeft = inRange;
    }

    public ComponentBehaviorPackage(ComponentEatPickableBehavior behavior, bool inRange)
    {
        EntityId = behavior.Entity.EntityId;
        PackageEventType = EventType.EatPickable;
        RowLeft = inRange;
    }

    public ComponentBehaviorPackage(ComponentDigInMudBehavior behavior, bool isDigIn)
    {
        EntityId = behavior.Entity.EntityId;
        PackageEventType = EventType.DigInMud;
        RowLeft = isDigIn;
    }

    public ComponentBehaviorPackage(ComponentHumanModel componentModel)
    {
        PackageEventType = EventType.HumanRow;
        EntityId = componentModel.Entity.EntityId;
        RowLeft = componentModel.RowLeft;
        RowRight = componentModel.RowRight;
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.WriteEnum(PackageEventType);
        writer.Write(EntityId);
        switch (PackageEventType)
        {
            case EventType.FishOutOfWater:
            case EventType.DigInMud:
            case EventType.ChaseBehavior:
            case EventType.RandomFeed:
            case EventType.RandomPeck:
            case EventType.EatPickable:
                writer.Write(RowLeft);
                break;
            case EventType.HumanRow:
                writer.Write(RowLeft);
                writer.Write(RowRight);
                break;
            case EventType.CreatureSound:
                writer.Write(Type);
                writer.Write(RowLeft);
                break;
        }
    }

    public void ReadData(PackageStreamReader reader)
    {
        PackageEventType = reader.ReadEnum<EventType>();
        EntityId = reader.ReadInt32();
        switch (PackageEventType)
        {
            case EventType.FishOutOfWater:
            case EventType.DigInMud:
            case EventType.ChaseBehavior:
            case EventType.RandomFeed:
            case EventType.RandomPeck:
            case EventType.EatPickable:
                RowLeft = reader.ReadBoolean();
                break;
            case EventType.HumanRow:
                RowLeft = reader.ReadBoolean();
                RowRight = reader.ReadBoolean();
                break;
            case EventType.CreatureSound:
                Type = reader.ReadByte();
                RowLeft = reader.ReadBoolean();
                break;
        }
    }
}
