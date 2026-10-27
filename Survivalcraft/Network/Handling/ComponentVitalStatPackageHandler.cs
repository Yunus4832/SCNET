using Game.Network.Transport;

namespace Game.Network.Handling;

public sealed class ComponentVitalStatPackageHandler : PackageHandlerBase<ComponentVitalStatPackage>
{
    public override void Handle(ComponentVitalStatPackage package, PackageReceiveContext context)
    {
        var isServer = context.IsServer;
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        switch (package)
        {
            case VitalStatePackage message1:
            {
                project.FindEntityById(
                    message1.EntityId,
                    entity =>
                    {
                        var vitalStats = entity.FindComponent<ComponentVitalStats>();
                        if (vitalStats == null)
                        {
                            return;
                        }

                        if (isServer)
                        {
                            // 服务器只同步耐力
                            vitalStats.Stamina = message1.Stamina;
                        }
                        else
                        {
                            // 客户端不同步耐力
                            vitalStats.Food = message1.Food;
                            vitalStats.Sleep = message1.Sleep;
                            vitalStats.Wetness = message1.Wetness;
                            vitalStats.Temperature = message1.Temperature;
                        }
                    }
                );
                break;
            }
        }
    }
}
