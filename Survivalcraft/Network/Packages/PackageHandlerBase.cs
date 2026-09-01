namespace Game.Network.Packages;

public abstract class PackageHandlerBase<TPackage> : IPackageHandler<TPackage> where TPackage : IPackage
{
    public Type PackageType => typeof(TPackage);

    public abstract void Handle(TPackage package, NetNode? netNode, bool isServer);

    void IPackageHandler.Handle(IPackage package, NetNode? netNode, bool isServer)
    {
        Handle((TPackage)package, netNode, isServer);
    }
}
