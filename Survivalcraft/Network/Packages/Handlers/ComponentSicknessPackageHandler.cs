using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public partial class ComponentSicknessPackage
{
    internal void HandleCore(NetNode netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        project.FindEntityById(EntityId, entity =>
        {
            var sickness = entity.FindComponent<ComponentSickness>();
            if (sickness == null)
            {
                return;
            }

            sickness.SicknessDuration = SicknessDuration;
            if (SicknessDuration > 0f)
            {
                sickness.NauseaEffect();
            }
        });
    }
}

public sealed class ComponentSicknessPackageHandler : PackageHandlerBase<ComponentSicknessPackage>
{
    public override void Handle(ComponentSicknessPackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{typeof(ComponentSicknessPackage).Name}");
            return;
        }

        package.HandleCore(netNode, isServer);
    }
}
