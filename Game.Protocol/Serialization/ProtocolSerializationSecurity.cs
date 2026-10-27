using Engine.Core.Mathematics;

using MessagePack;

namespace Game.Protocol.Serialization;

internal sealed class ProtocolSerializationSecurity()
    : MessagePackSecurity(UntrustedData.WithMaximumObjectGraphDepth(64))
{
    protected override IEqualityComparer<T> GetHashCollisionResistantEqualityComparer<T>()
    {
        if (typeof(T) == typeof(Point2)) { return (IEqualityComparer<T>)(object)Point2Comparer.Instance; }

        if (typeof(T) == typeof(Point3)) { return (IEqualityComparer<T>)(object)Point3Comparer.Instance; }

        return base.GetHashCollisionResistantEqualityComparer<T>();
    }

    private sealed class Point2Comparer : IEqualityComparer<Point2>
    {
        public static readonly Point2Comparer Instance = new();

        public bool Equals(Point2 x, Point2 y) => x == y;
        public int GetHashCode(Point2 value) => HashCode.Combine(value.X, value.Y);
    }

    private sealed class Point3Comparer : IEqualityComparer<Point3>
    {
        public static readonly Point3Comparer Instance = new();

        public bool Equals(Point3 x, Point3 y) => x == y;
        public int GetHashCode(Point3 value) => HashCode.Combine(value.X, value.Y, value.Z);
    }
}
