using Game.Network.Packages;

namespace Game.Network;

public sealed record OutboundPackage(IPackage Package, PackageAudience Audience)
{
    internal NetworkMessageFanout? Fanout { get; init; }
}
