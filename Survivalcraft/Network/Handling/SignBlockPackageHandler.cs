using Game.Network.Transport;

namespace Game.Network.Handling;

public sealed class SignBlockPackageHandler : PackageHandlerBase<SignBlockPackage>
{
    public override void Handle(SignBlockPackage package, PackageReceiveContext context)
    {
        var netNode = context.Node;
        var isServer = context.IsServer;
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{nameof(SignBlockPackage)}");
            return;
        }

        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        if (package is not { Lines.Count: 4, Colors.Count: 4 })
        {
            return;
        }

        if (isServer &&
            (context.Sender is null ||
             !project.FindSubsystem<SubsystemNetworkInterest>(true)!
                 .IsPositionRelevant(context.Sender, new Vector2(package.Point.X, package.Point.Z))))
        {
            return;
        }

        var value = project.FindSubsystem<SubsystemTerrain>(true)!
            .Terrain.GetCellValue(package.Point.X, package.Point.Y, package.Point.Z);
        if (isServer &&
            CurrentModRuntime.Value!.RequireWorldCatalog().Blocks[Terrain.ExtractContents(value)] is not SignBlock)
        {
            return;
        }

        project.FindSubsystem<SubsystemSignBlockBehavior>(true)!.SetSignData(
            package.Point,
            package.Lines.ToArray(),
            package.Colors.ToArray(),
            package.Url
        );

        if (isServer)
        {
            NetworkSender.SendToChunkObservers(
                project,
                new Point2(package.Point.X >> 4, package.Point.Z >> 4),
                package
            );
        }
    }
}
