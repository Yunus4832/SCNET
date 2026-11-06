using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
[Union(0, typeof(CreatureChaseStatePackage))]
[Union(1, typeof(CreatureDigStatePackage))]
[Union(2, typeof(CreatureEatStatePackage))]
[Union(3, typeof(CreatureFeedStatePackage))]
[Union(4, typeof(CreatureOutOfWaterStatePackage))]
[Union(5, typeof(CreaturePeckStatePackage))]
[Union(6, typeof(CreatureRowingStatePackage))]
[Union(7, typeof(CreatureSoundPackage))]
[Union(8, typeof(CreatureShapeshiftStatePackage))]
[Union(9, typeof(CreatureWhalePlumePackage))]
[Union(10, typeof(ClothingEffectPackage))]
public abstract record ComponentBehaviorPackage : IProtocolMessage
{
    public enum EventType
    {
        ChaseBehavior = 0,

        RandomPeck = 1,

        RandomFeed = 2,

        EatPickable = 3,

        HumanRow = 4,

        CreatureSound = 5,

        DigInMud = 6,

        FishOutOfWater = 7,

        Shapeshift = 8,

        WhalePlume = 9,

        ClothingEffect = 10
    }

    [IgnoreMember] public abstract EventType PackageEventType { get; }

    [Key(0)] public int EntityId { get; init; }
}
