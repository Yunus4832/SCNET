namespace Game.Network.Transport;

public readonly record struct PackageReceiveContext(NetNode? Node, bool IsServer, Client? Sender);
