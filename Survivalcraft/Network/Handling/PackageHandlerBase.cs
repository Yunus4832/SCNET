using Game.Network.Transport;

namespace Game.Network.Handling;

public abstract class PackageHandlerBase<TPackage> : IPackageHandler<TPackage> where TPackage : IProtocolMessage
{
    public Type PackageType => typeof(TPackage);

    public abstract void Handle(TPackage package, PackageReceiveContext context);

    void IPackageHandler.Handle(IProtocolMessage package, PackageReceiveContext context)
    {
        Handle((TPackage)package, context);
    }
}
