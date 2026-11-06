namespace Game.Network.Handling;

public class ChunkStateResetPackageHandler : PackageHandlerBase<ChunkStateResetPackage>
{
    public override void Handle(ChunkStateResetPackage package, PackageReceiveContext context)
    {
        if (context.IsServer || context.Project is not { } project)
        {
            return;
        }

        project.FindSubsystem<SubsystemSignBlockBehavior>(true)!.ClearChunkState(package.Chunk);
        project.FindSubsystem<SubsystemMemoryBankBlockBehavior>(true)!.ClearChunkState(package.Chunk);
        project.FindSubsystem<SubsystemTruthTableCircuitBlockBehavior>(true)!.ClearChunkState(package.Chunk);
        project.FindSubsystem<SubsystemFireBlockBehavior>(true)!.ClearChunkState(package.Chunk);
        project.FindSubsystem<SubsystemExplosivesBlockBehavior>(true)!.ClearReplicaChunkState(package.Chunk);
    }
}
