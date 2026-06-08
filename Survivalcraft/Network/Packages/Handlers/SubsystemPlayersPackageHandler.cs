using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public partial class SubsystemPlayersPackage
{
    internal void HandleCore(NetNode netNode, bool isServer)
    {
        lock (ComponentPlayerPackageList)
        {
            foreach (var package in ComponentPlayerPackageList)
            {
                PackageDispatcher.Handle(package, netNode, isServer);
            }
        }
    }
}

public sealed class SubsystemPlayersPackageHandler : PackageHandlerBase<SubsystemPlayersPackage>
{
    public override void Handle(SubsystemPlayersPackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{typeof(SubsystemPlayersPackage).Name}");
            return;
        }

        package.HandleCore(netNode, isServer);
    }
}
