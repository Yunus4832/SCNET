namespace Game.Network.Transport;

public sealed record OutboundPackage(IProtocolMessage Package, PackageAudience Audience, WorldRuntimeContext Runtime)
{
    internal NetworkMessageFanout? Fanout { get; init; }
}
