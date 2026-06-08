namespace Game.Network.Packages.Handlers;

public sealed class SubsystemPlayersPackageHandler : PackageHandlerBase<SubsystemPlayersPackage>
{
    public override void Handle(SubsystemPlayersPackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{nameof(SubsystemPlayersPackage)}");
            return;
        }

        lock (package.ComponentPlayerPackageList)
        {
            foreach (var componentPlayerPackage in package.ComponentPlayerPackageList)
            {
                PackageDispatcher.Handle(componentPlayerPackage, netNode, isServer);
            }
        }
    }
}
