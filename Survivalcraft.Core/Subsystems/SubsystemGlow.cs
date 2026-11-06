using EntitySystem.Core;

namespace Game.Subsystems;

/// <summary>Mutable CPU glow facts shared by electric simulation and client-only producers.</summary>
public sealed class SubsystemGlow : Subsystem
{
    private readonly Dictionary<GlowPoint, bool> _glowPoints = new(ReferenceEqualityComparer.Instance);

    private bool _disposed;

    public IReadOnlyCollection<GlowPoint> Points => _glowPoints.Keys;

    public GlowPoint AddGlowPoint()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var point = new GlowPoint();
        _glowPoints.Add(point, true);
        return point;
    }

    public void RemoveGlowPoint(GlowPoint point) => _glowPoints.Remove(point);

    public override void Dispose()
    {
        _disposed = true;
        _glowPoints.Clear();
    }
}
