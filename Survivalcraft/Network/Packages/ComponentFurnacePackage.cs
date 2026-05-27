using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public class ComponentFurnacePackage : IPackage
{
    private readonly List<FurnacePackageData> _furnaceDataList = [];

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
            _furnaceDataList.Add(furnaceData);
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

        _furnaceDataList.Add(furnaceData);
    }

    public void Handle(NetNode netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        foreach (var furnaceData in _furnaceDataList)
        {
            project.FindEntityById(furnaceData.EntityID, e =>
            {
                var furnace = e.FindComponent<ComponentFurnace>();
                if (furnace == null)
                {
                    return;
                }

                furnace.SmeltingProgress = furnaceData.SmeltingProgress;
                furnace.FireTimeRemaining = furnaceData.FireTimeRemaining;
                furnace.HeatLevel = furnaceData.HeatLevel;
            });
        }
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
            _furnaceDataList.Add(furnaceData);
        }
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(_furnaceDataList.Count);
        foreach (var furnace in _furnaceDataList)
        {
            writer.Write(furnace.EntityID);
            writer.Write(furnace.FireTimeRemaining);
            writer.Write(furnace.SmeltingProgress);
            writer.Write(furnace.HeatLevel);
        }
    }

    private struct FurnacePackageData
    {
        public float FireTimeRemaining;

        public float SmeltingProgress;

        public float HeatLevel;

        public int EntityID;
    }
}
