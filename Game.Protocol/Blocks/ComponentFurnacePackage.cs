namespace Game.Protocol.Blocks;

public sealed record ComponentFurnacePackage : IProtocolMessage
{
    public IReadOnlyList<FurnacePackageData> FurnaceDataList { get; init; } = [];
    public byte ID => (byte)PackageType.ComponentFurnace;
    public ComponentFurnacePackage() { }
    public ComponentFurnacePackage(int entityId, float fireTimeRemaining, float smeltingProgress, float heatLevel)
    { FurnaceDataList = [new FurnacePackageData { EntityID = entityId, FireTimeRemaining = fireTimeRemaining, SmeltingProgress = smeltingProgress, HeatLevel = heatLevel }]; }
    public readonly record struct FurnacePackageData
    {
        public float FireTimeRemaining { get; init; }
        public float SmeltingProgress { get; init; }
        public float HeatLevel { get; init; }
        public int EntityID { get; init; }
    }
}
