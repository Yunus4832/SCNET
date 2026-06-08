using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public partial class ComponentFluPackage
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
                    var flu = entity.FindComponent<ComponentFlu>();
                    if (flu == null)
                    {
                        return;
                    }

                    flu.FluOnset = FluOnset;
                    flu.SneezeDuration = SneezeDuration;
                    flu.CoughDuration = CoughDuration;
                    flu.FluDuration = FluDuration;
                });
                break;
            case EventType.FluEffect:
                project.FindEntityById(EntityId, entity =>
                {
                    var flu = entity.FindComponent<ComponentFlu>();
                    if (flu == null)
                    {
                        return;
                    }

                    flu.FluOnset = FluOnset;
                    flu.SneezeDuration = SneezeDuration;
                    flu.CoughDuration = CoughDuration;
                    flu.FluDuration = FluDuration;
                    flu.FluEffect();
                });
                break;
            case EventType.StartFlu:
                project.FindEntityById(EntityId, entity =>
                {
                    var flu = entity.FindComponent<ComponentFlu>();
                    if (flu == null)
                    {
                        return;
                    }

                    flu.FluOnset = FluOnset;
                    flu.SneezeDuration = SneezeDuration;
                    flu.CoughDuration = CoughDuration;
                    flu.FluDuration = FluDuration;
                    flu.StartFlu();
                });
                break;
            case EventType.Sneeze:
                project.FindEntityById(EntityId, entity =>
                {
                    var flu = entity.FindComponent<ComponentFlu>();
                    if (flu == null)
                    {
                        return;
                    }

                    flu.FluOnset = FluOnset;
                    flu.SneezeDuration = SneezeDuration;
                    flu.CoughDuration = CoughDuration;
                    flu.FluDuration = FluDuration;
                    flu.Sneeze();
                });
                break;
        }
    }
}

public sealed class ComponentFluPackageHandler : PackageHandlerBase<ComponentFluPackage>
{
    public override void Handle(ComponentFluPackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{typeof(ComponentFluPackage).Name}");
            return;
        }

        package.HandleCore(netNode, isServer);
    }
}
