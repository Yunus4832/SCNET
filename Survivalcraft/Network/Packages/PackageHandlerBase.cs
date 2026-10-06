namespace Game.Network.Packages;

public abstract class PackageHandlerBase<TPackage> : IPackageHandler<TPackage> where TPackage : IPackage
{
    public Type PackageType => typeof(TPackage);

    public abstract void Handle(TPackage package, PackageReceiveContext context);

    void IPackageHandler.Handle(IPackage package, PackageReceiveContext context)
    {
        Handle((TPackage)package, context);
    }
}
