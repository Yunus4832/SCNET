namespace Game.Network;

public readonly record struct PackageReceiveContext(NetNode? Node, bool IsServer, Client? Sender);
