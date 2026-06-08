namespace Game.Network.Packages.Handlers;

public sealed class ExplosionsPackageHandler : PackageHandlerBase<ExplosionsPackage>
{
    public override void Handle(ExplosionsPackage package, NetNode? netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        var sub = project.FindSubsystem<SubsystemExplosions>(true)!;
        switch (package.Type)
        {
            case ExplosionsPackage.EventType.Cell:
                if ((ExplosionParticleSystem?)sub.ExplosionParticleSystem is null)
                {
                    break;
                }

                foreach (var j in package.Cells.SelectMany(i => i.Value))
                {
                    sub.ExplosionParticleSystem.SetExplosionCell(j.Item1, j.Item2);
                }

                break;
            case ExplosionsPackage.EventType.Sound:
                sub.PlayExplosionSound(package.Position, package.Level, package.Delay, true);
                break;
        }
    }
}
