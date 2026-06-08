using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public partial class ComponentFurnacePackage
{
    internal void HandleCore(NetNode netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        foreach (var furnaceData in FurnaceDataList)
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
}

public sealed class ComponentFurnacePackageHandler : PackageHandlerBase<ComponentFurnacePackage>
{
    public override void Handle(ComponentFurnacePackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{typeof(ComponentFurnacePackage).Name}");
            return;
        }

        package.HandleCore(netNode, isServer);
    }
}
