using System.Runtime.CompilerServices;

namespace Game.Network.Transport;

internal sealed class OutboundPackageIdentityComparer : IEqualityComparer<OutboundPackage>
{
    public static OutboundPackageIdentityComparer Instance { get; } = new();

    public bool Equals(OutboundPackage? x, OutboundPackage? y)
    {
        return ReferenceEquals(x, y) ||
               (x != null &&
                y != null &&
                ReferenceEquals(x.Runtime, y.Runtime) &&
                ReferenceEquals(x.Package, y.Package) &&
                Equals(x.Audience, y.Audience) &&
                ReferenceEquals(x.Fanout, y.Fanout));
    }

    public int GetHashCode(OutboundPackage obj)
    {
        return HashCode.Combine(
            RuntimeHelpers.GetHashCode(obj.Runtime),
            RuntimeHelpers.GetHashCode(obj.Package),
            obj.Audience,
            obj.Fanout == null ? 0 : RuntimeHelpers.GetHashCode(obj.Fanout)
        );
    }
}
