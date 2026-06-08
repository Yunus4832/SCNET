namespace Game.Network.Packages.Handlers;

public sealed class ComponentFluPackageHandler : PackageHandlerBase<ComponentFluPackage>
{
    public override void Handle(ComponentFluPackage package, NetNode? netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        switch (package.PackageEventType)
        {
            case ComponentFluPackage.EventType.SyncStat:
                project.FindEntityById(package.EntityId, entity =>
                {
                    var flu = entity.FindComponent<ComponentFlu>();
                    if (flu == null)
                    {
                        return;
                    }

                    flu.FluOnset = package.FluOnset;
                    flu.SneezeDuration = package.SneezeDuration;
                    flu.CoughDuration = package.CoughDuration;
                    flu.FluDuration = package.FluDuration;
                });
                break;
            case ComponentFluPackage.EventType.FluEffect:
                project.FindEntityById(package.EntityId, entity =>
                {
                    var flu = entity.FindComponent<ComponentFlu>();
                    if (flu == null)
                    {
                        return;
                    }

                    flu.FluOnset = package.FluOnset;
                    flu.SneezeDuration = package.SneezeDuration;
                    flu.CoughDuration = package.CoughDuration;
                    flu.FluDuration = package.FluDuration;
                    flu.FluEffect();
                });
                break;
            case ComponentFluPackage.EventType.StartFlu:
                project.FindEntityById(package.EntityId, entity =>
                {
                    var flu = entity.FindComponent<ComponentFlu>();
                    if (flu == null)
                    {
                        return;
                    }

                    flu.FluOnset = package.FluOnset;
                    flu.SneezeDuration = package.SneezeDuration;
                    flu.CoughDuration = package.CoughDuration;
                    flu.FluDuration = package.FluDuration;
                    flu.StartFlu();
                });
                break;
            case ComponentFluPackage.EventType.Sneeze:
                project.FindEntityById(package.EntityId, entity =>
                {
                    var flu = entity.FindComponent<ComponentFlu>();
                    if (flu == null)
                    {
                        return;
                    }

                    flu.FluOnset = package.FluOnset;
                    flu.SneezeDuration = package.SneezeDuration;
                    flu.CoughDuration = package.CoughDuration;
                    flu.FluDuration = package.FluDuration;
                    flu.Sneeze();
                });
                break;
        }
    }
}
