namespace Game.Network.Handling;

public sealed class TerrainLightSectionsPackageHandler : PackageHandlerBase<TerrainLightSectionsPackage>
{
    public override void Handle(TerrainLightSectionsPackage package, PackageReceiveContext context)
    {
        if (context.IsServer || context.Project is null) { return; }

        var terrain = context.Project.FindSubsystem<SubsystemTerrain>(true)!;
        if (terrain.ReplicaLighting?.Receive(package) == true) { terrain.WorkExecutor.Wake(); }
    }
}
