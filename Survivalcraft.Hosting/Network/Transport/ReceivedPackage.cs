namespace Game.Network.Transport;

public readonly record struct ReceivedPackage(IProtocolMessage Package, PackageReceiveContext Context);
