using Game.Network.Transport;

namespace Game.Network.Handling;

public sealed class ExplosionsPackageHandler : PackageHandlerBase<ExplosionsPackage>
{
    internal static bool AcceptsDirection(bool isServer) => !isServer;

    public override void Handle(ExplosionsPackage package, PackageReceiveContext context)
    {
        if (context.Project is not { } project ||
            context.Runtime.IsClosed ||
            !context.Runtime.HasPresentation ||
            !AcceptsDirection(context.IsServer)) { return; }

        var presentation = project.FindSubsystem<SubsystemExplosionPresentation>(true)!;
        switch (package)
        {
            case ExplosionCellsPackage cells:
                presentation.ApplyCells(cells.Cells.SelectMany(pair => pair.Value));
                break;
            case ExplosionSoundPackage sound:
                presentation.PlaySound(sound.Position, sound.Level, sound.Pitch);
                break;
        }
    }
}
