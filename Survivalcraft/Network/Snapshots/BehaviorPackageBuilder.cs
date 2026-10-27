namespace Game.Network.Snapshots;

public static class BehaviorPackageBuilder
{
    public static ComponentBehaviorPackage Capture(
        ComponentFishOutOfWaterBehavior fishOutOfWaterBehavior,
        bool isBend
    ) => new CreatureOutOfWaterStatePackage
        { EntityId = fishOutOfWaterBehavior.Entity.EntityId, IsActive = isBend };

    public static ComponentBehaviorPackage Capture(ComponentChaseBehavior behavior, bool isChase) =>
        new CreatureChaseStatePackage
            { EntityId = behavior.Entity.EntityId, IsActive = isChase };

    public static ComponentBehaviorPackage Capture(
        ComponentCreatureSounds creatureSound,
        byte type,
        bool skip = false
    ) => new CreatureSoundPackage
        { EntityId = creatureSound.Entity.EntityId, SoundKind = type, SkipIfRecentlyPlayed = skip };

    public static ComponentBehaviorPackage Capture(ComponentRandomPeckBehavior behavior, bool inRange) =>
        new CreaturePeckStatePackage { EntityId = behavior.Entity.EntityId, IsActive = inRange };

    public static ComponentBehaviorPackage Capture(ComponentRandomFeedBehavior behavior, bool inRange) =>
        new CreatureFeedStatePackage { EntityId = behavior.Entity.EntityId, IsActive = inRange };

    public static ComponentBehaviorPackage Capture(ComponentEatPickableBehavior behavior, bool inRange) =>
        new CreatureEatStatePackage { EntityId = behavior.Entity.EntityId, IsActive = inRange };

    public static ComponentBehaviorPackage Capture(ComponentDigInMudBehavior behavior, bool isDigIn) =>
        new CreatureDigStatePackage { EntityId = behavior.Entity.EntityId, IsActive = isDigIn };

    public static ComponentBehaviorPackage Capture(ComponentHumanModel componentModel) =>
        new CreatureRowingStatePackage
        {
            EntityId = componentModel.Entity.EntityId, RowLeft = componentModel.RowLeft,
            RowRight = componentModel.RowRight
        };
}
