namespace Game.Network.Packages;

public interface IPackageHandler
{
    Type PackageType { get; }

    void Handle(IPackage package, PackageReceiveContext context);
}

public interface IPackageHandler<in TPackage> : IPackageHandler where TPackage : IPackage
{
    void Handle(TPackage package, PackageReceiveContext context);
}
