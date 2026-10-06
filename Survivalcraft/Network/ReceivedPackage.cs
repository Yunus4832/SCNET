using Game.Network.Packages;

namespace Game.Network;

public readonly record struct ReceivedPackage(IPackage Package, PackageReceiveContext Context);
