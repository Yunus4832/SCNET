namespace Game.Network.Packages.Handlers;

public sealed class ComponentVitalStatPackageHandler : PackageHandlerBase<ComponentVitalStatPackage>
{
    public override void Handle(ComponentVitalStatPackage package, NetNode? netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        switch (package.PackageEventType)
        {
            case ComponentVitalStatPackage.EventType.SyncStat:
                project.FindEntityById(package.EntityId, entity =>
                {
                    var vitalStats = entity.FindComponent<ComponentVitalStats>();
                    if (vitalStats == null)
                    {
                        return;
                    }

                    if (isServer)
                    {
                        // 服务器只同步耐力
                        vitalStats.Stamina = package.Stamina;
                    }
                    else
                    {
                        // 客户端不同步耐力
                        vitalStats.Food = package.Food;
                        vitalStats.Sleep = package.Sleep;
                        vitalStats.Wetness = package.Wetness;
                        vitalStats.Temperature = package.Temperature;
                    }
                });
                break;
        }
    }
}
