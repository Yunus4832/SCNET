using MessagePack;

namespace Game.Protocol.Blocks;

[MessagePackObject]
public sealed record ComponentFurnacePackage() : IProtocolMessage
{
    [Key(0)] public IReadOnlyList<FurnacePackageData> FurnaceDataList { get; init; } = [];

    public ComponentFurnacePackage(
        int entityId,
        float fireTimeRemaining,
        float smeltingProgress,
        float heatLevel
    ) : this()
    {
        FurnaceDataList =
        [
            new FurnacePackageData
            {
                EntityID = entityId,
                FireTimeRemaining = fireTimeRemaining,
                SmeltingProgress = smeltingProgress,
                HeatLevel = heatLevel
            }
        ];
    }

    [MessagePackObject]
    public readonly record struct FurnacePackageData
    {
        [Key(0)] public float FireTimeRemaining { get; init; }

        [Key(1)] public float SmeltingProgress { get; init; }

        [Key(2)] public float HeatLevel { get; init; }

        [Key(3)] public int EntityID { get; init; }
    }
}
