using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public partial class ExplosionsPackage
{
    internal void HandleCore(NetNode netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        var sub = project.FindSubsystem<SubsystemExplosions>(true)!;
        switch (Type)
        {
            case EventType.Cell:
                if (sub.ExplosionParticleSystem == null)
                {
                    break;
                }

                foreach (var i in _cells)
                {
                    foreach (var j in i.Value)
                    {
                        sub.ExplosionParticleSystem.SetExplosionCell(j.Item1, j.Item2);
                    }
                }

                break;
            case EventType.Sound:
                sub.PlayExplosionSound(Position, Level, Delay, true);
                break;
        }
    }
}

public sealed class ExplosionsPackageHandler : PackageHandlerBase<ExplosionsPackage>
{
    public override void Handle(ExplosionsPackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{typeof(ExplosionsPackage).Name}");
            return;
        }

        package.HandleCore(netNode, isServer);
    }
}
