using Game.Network.Transport;

namespace Game.Network.Handling;

public interface IPackageHandler
{
    Type PackageType { get; }

    void Handle(IProtocolMessage package, PackageReceiveContext context);
}

public interface IPackageHandler<in TPackage> : IPackageHandler where TPackage : IProtocolMessage
{
    void Handle(TPackage package, PackageReceiveContext context);
}
