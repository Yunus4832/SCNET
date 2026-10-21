namespace Game.Network.Snapshots;

public static class BehaviorPackageBuilder
{
    public static ComponentBehaviorPackage Capture(
        ComponentFishOutOfWaterBehavior fishOutOfWaterBehavior,
        bool isBend
    ) => new()
    {
        EntityId = fishOutOfWaterBehavior.Entity.EntityId,
        PackageEventType = ComponentBehaviorPackage.EventType.FishOutOfWater, RowLeft = isBend
    };

    public static ComponentBehaviorPackage Capture(ComponentChaseBehavior behavior, bool isChase) => new()
    {
        EntityId = behavior.Entity.EntityId, PackageEventType = ComponentBehaviorPackage.EventType.ChaseBehavior,
        RowLeft = isChase
    };

    public static ComponentBehaviorPackage Capture(
        ComponentCreatureSounds creatureSound,
        byte type,
        bool skip = false
    ) => new()
    {
        EntityId = creatureSound.Entity.EntityId, PackageEventType = ComponentBehaviorPackage.EventType.CreatureSound,
        Type = type, RowLeft = skip
    };

    public static ComponentBehaviorPackage Capture(ComponentRandomPeckBehavior behavior, bool inRange) => new()
    {
        EntityId = behavior.Entity.EntityId, PackageEventType = ComponentBehaviorPackage.EventType.RandomPeck,
        RowLeft = inRange
    };

    public static ComponentBehaviorPackage Capture(ComponentRandomFeedBehavior behavior, bool inRange) => new()
    {
        EntityId = behavior.Entity.EntityId, PackageEventType = ComponentBehaviorPackage.EventType.RandomFeed,
        RowLeft = inRange
    };

    public static ComponentBehaviorPackage Capture(ComponentEatPickableBehavior behavior, bool inRange) => new()
    {
        EntityId = behavior.Entity.EntityId, PackageEventType = ComponentBehaviorPackage.EventType.EatPickable,
        RowLeft = inRange
    };

    public static ComponentBehaviorPackage Capture(ComponentDigInMudBehavior behavior, bool isDigIn) => new()
    {
        EntityId = behavior.Entity.EntityId, PackageEventType = ComponentBehaviorPackage.EventType.DigInMud,
        RowLeft = isDigIn
    };

    public static ComponentBehaviorPackage Capture(ComponentHumanModel componentModel) => new()
    {
        PackageEventType = ComponentBehaviorPackage.EventType.HumanRow, EntityId = componentModel.Entity.EntityId,
        RowLeft = componentModel.RowLeft, RowRight = componentModel.RowRight
    };
}
