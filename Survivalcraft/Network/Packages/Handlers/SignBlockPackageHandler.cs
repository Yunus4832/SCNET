using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public partial class SignBlockPackage
{
    internal void HandleCore(NetNode netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        if (SignData != null)
        {
            project.FindSubsystem<SubsystemSignBlockBehavior>(true)!.SetSignData(
                Point,
                SignData.Lines,
                SignData.Colors,
                SignData.Url
            );
        }

        if (isServer)
        {
            netNode.QueuePackage(this);
        }
    }
}

public sealed class SignBlockPackageHandler : PackageHandlerBase<SignBlockPackage>
{
    public override void Handle(SignBlockPackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{typeof(SignBlockPackage).Name}");
            return;
        }

        package.HandleCore(netNode, isServer);
    }
}
