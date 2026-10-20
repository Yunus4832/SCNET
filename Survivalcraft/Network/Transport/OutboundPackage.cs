namespace Game.Network.Transport;

public sealed record OutboundPackage(IProtocolMessage Package, PackageAudience Audience)
{
    internal NetworkMessageFanout? Fanout { get; init; }
}
