namespace Game.Network.Packages.Handlers;

public sealed class ComponentFurnacePackageHandler : PackageHandlerBase<ComponentFurnacePackage>
{
    public override void Handle(ComponentFurnacePackage package, NetNode? netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        foreach (var furnaceData in package.FurnaceDataList)
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
