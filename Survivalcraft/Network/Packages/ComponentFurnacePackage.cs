using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public class ComponentFurnacePackage : IPackage
{
    public readonly List<FurnacePackageData> FurnaceDataList = [];

    public byte ID => (byte)PackageType.ComponentFurnace;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.ProjectLoaded;

    public ComponentFurnacePackage()
    {
    }

    public ComponentFurnacePackage(List<ComponentFurnace> componentFurnaces)
    {
        foreach (var componentFurnace in componentFurnaces)
        {
            var furnaceData = new FurnacePackageData
            {
                EntityID = componentFurnace.Entity.EntityId,
                FireTimeRemaining = componentFurnace.FireTimeRemaining,
                SmeltingProgress = componentFurnace.SmeltingProgress,
                HeatLevel = componentFurnace.HeatLevel
            };
            FurnaceDataList.Add(furnaceData);
        }
    }

    public ComponentFurnacePackage(int entityId, float fireTimeRemaining, float smeltingProgress, float heatLevel)
    {
        var furnaceData = new FurnacePackageData
        {
            EntityID = entityId,
            FireTimeRemaining = fireTimeRemaining,
            SmeltingProgress = smeltingProgress,
            HeatLevel = heatLevel
        };

        FurnaceDataList.Add(furnaceData);
    }


    public void ReadData(PackageStreamReader reader)
    {
        var count = reader.ReadInt32();
        for (var i = 0; i < count; i++)
        {
            var furnaceData = new FurnacePackageData
            {
                EntityID = reader.ReadInt32(),
                FireTimeRemaining = reader.ReadSingle(),
                SmeltingProgress = reader.ReadSingle(),
                HeatLevel = reader.ReadSingle()
            };
            FurnaceDataList.Add(furnaceData);
        }
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(FurnaceDataList.Count);
        foreach (var furnace in FurnaceDataList)
        {
            writer.Write(furnace.EntityID);
            writer.Write(furnace.FireTimeRemaining);
            writer.Write(furnace.SmeltingProgress);
            writer.Write(furnace.HeatLevel);
        }
    }

    public struct FurnacePackageData
    {
        public float FireTimeRemaining;

        public float SmeltingProgress;

        public float HeatLevel;

        public int EntityID;
    }
}
