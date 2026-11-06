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

        if (context.Project is null)
        {
            return;
        }

        var project = context.Project;
        if (package is not { Lines.Count: 4, Colors.Count: 4 })
        {
            return;
        }

        var signs = project.FindSubsystem<SubsystemSignBlockBehavior>(true)!;
        if (isServer && !signs.CanEditSign(package.Point, context.Sender))
        {
            return;
        }

        signs.SetSignData(
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
