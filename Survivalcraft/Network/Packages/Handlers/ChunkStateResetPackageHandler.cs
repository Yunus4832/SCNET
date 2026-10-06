namespace Game.Network.Packages.Handlers;

public sealed class ChunkStateResetPackageHandler : PackageHandlerBase<ChunkStateResetPackage>
{
    public override void Handle(ChunkStateResetPackage package, PackageReceiveContext context)
    {
        if (context.IsServer || GameManager.Project is not { } project)
        {
            return;
        }

        project.FindSubsystem<SubsystemSignBlockBehavior>(true)!.ClearChunkState(package.Chunk);
        project.FindSubsystem<SubsystemMemoryBankBlockBehavior>(true)!.ClearChunkState(package.Chunk);
        project.FindSubsystem<SubsystemTruthTableCircuitBlockBehavior>(true)!.ClearChunkState(package.Chunk);
        project.FindSubsystem<SubsystemFireBlockBehavior>(true)!.ClearChunkState(package.Chunk);
    }
}
