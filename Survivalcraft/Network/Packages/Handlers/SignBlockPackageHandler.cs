namespace Game.Network.Packages.Handlers;

public sealed class SignBlockPackageHandler : PackageHandlerBase<SignBlockPackage>
{
    public override void Handle(SignBlockPackage package, NetNode? netNode, bool isServer)
    {
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
        if (package.SignData != null)
        {
            project.FindSubsystem<SubsystemSignBlockBehavior>(true)!.SetSignData(
                package.Point,
                package.SignData.Lines,
                package.SignData.Colors,
                package.SignData.Url
            );
        }

        if (isServer)
        {
            netNode.QueuePackage(package);
        }
    }
}
