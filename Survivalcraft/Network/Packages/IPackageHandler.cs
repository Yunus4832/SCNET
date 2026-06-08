namespace Game.Network.Packages;

public interface IPackageHandler
{
    Type PackageType { get; }

    void Handle(IPackage package, NetNode? netNode, bool isServer);
}

public interface IPackageHandler<in TPackage> : IPackageHandler where TPackage : IPackage
{
    void Handle(TPackage package, NetNode? netNode, bool isServer);
}

