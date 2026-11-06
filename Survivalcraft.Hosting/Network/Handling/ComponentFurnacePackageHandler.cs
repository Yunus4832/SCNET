namespace Game.Network.Handling;

public sealed class ComponentFurnacePackageHandler : PackageHandlerBase<ComponentFurnacePackage>
{
    public override void Handle(ComponentFurnacePackage package, PackageReceiveContext context)
    {
        if (context.Project is null || context.IsServer)
        {
            return;
        }

        var project = context.Project;
        foreach (var furnaceData in package.FurnaceDataList)
        {
            project.FindEntityById(
                furnaceData.EntityID,
                e =>
                {
                    var furnace = e.FindComponent<ComponentFurnace>();
                    if (furnace == null)
                    {
                        return;
                    }

                    furnace.SmeltingProgress = furnaceData.SmeltingProgress;
                    furnace.FireTimeRemaining = furnaceData.FireTimeRemaining;
                    furnace.HeatLevel = furnaceData.HeatLevel;
                }
            );
        }
    }
}
