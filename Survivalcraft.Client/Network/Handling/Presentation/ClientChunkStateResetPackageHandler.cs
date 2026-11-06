using Game.Network.Transport;

namespace Game.Network.Handling.Presentation;

public sealed class ClientChunkStateResetPackageHandler : ChunkStateResetPackageHandler
{
    public override void Handle(ChunkStateResetPackage package, PackageReceiveContext context)
    {
        base.Handle(package, context);
        if (context.IsServer || !context.Runtime.HasPresentation || context.Project is not { } project) { return; }

        project.FindSubsystem<SubsystemSoundGeneratorPresentation>(true)!.ClearChunkEffects(package.Chunk);
    }
}
