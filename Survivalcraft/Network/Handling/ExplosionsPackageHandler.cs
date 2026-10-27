using Game.Network.Transport;

namespace Game.Network.Handling;

public sealed class ExplosionsPackageHandler : PackageHandlerBase<ExplosionsPackage>
{
    internal static bool AcceptsDirection(bool isServer) => !isServer;

    public override void Handle(ExplosionsPackage package, PackageReceiveContext context)
    {
        if (GameManager.Project is null || !AcceptsDirection(context.IsServer))
        {
            return;
        }

        var project = GameManager.Project;
        var sub = project.FindSubsystem<SubsystemExplosions>(true)!;
        switch (package)
        {
            case ExplosionCellsPackage message1:
            {
                if ((ExplosionParticleSystem?)sub.ExplosionParticleSystem is null)
                {
                    break;
                }

                foreach (var j in message1.Cells.SelectMany(i => i.Value))
                {
                    sub.ExplosionParticleSystem.SetExplosionCell(j.Position, j.Pressure);
                }

                break;
            }
            case ExplosionSoundPackage message2:
            {
                sub.PlayExplosionSound(message2.Position, message2.Level, message2.Delay, true);
                break;
            }
        }
    }
}
