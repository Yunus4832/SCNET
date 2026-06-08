using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public partial class ComponentVitalStatPackage
{
    internal void HandleCore(NetNode netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        switch (PackageEventType)
        {
            case EventType.SyncStat:
                project.FindEntityById(EntityId, entity =>
                {
                    var vitalStats = entity.FindComponent<ComponentVitalStats>();
                    if (vitalStats == null)
                    {
                        return;
                    }

                    if (isServer)
                    {
                        //服务器只同步耐力
                        vitalStats.Stamina = Stamina;
                    }
                    else
                    {
                        //客户端不同步耐力
                        vitalStats.Food = Food;
                        vitalStats.Sleep = Sleep;
                        vitalStats.Wetness = Wetness;
                        vitalStats.Temperature = Temperature;
                    }
                });
                break;
        }
    }
}

public sealed class ComponentVitalStatPackageHandler : PackageHandlerBase<ComponentVitalStatPackage>
{
    public override void Handle(ComponentVitalStatPackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{typeof(ComponentVitalStatPackage).Name}");
            return;
        }

        package.HandleCore(netNode, isServer);
    }
}
