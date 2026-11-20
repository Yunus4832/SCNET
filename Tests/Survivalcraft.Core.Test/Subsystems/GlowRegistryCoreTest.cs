using Engine.Core.Mathematics;

using Game.Rendering;
using Game.Subsystems;

namespace Survivalcraft.Core.Test.Subsystems;

public sealed class GlowRegistryCoreTest
{
    [Fact]
    public void RegistryExposesTheActualCpuFactsAndDisposePreventsNewPoints()
    {
        var registry = new SubsystemGlow();
        var point = registry.AddGlowPoint();
        point.Color = Color.Red;
        point.Position = new Vector3(1, 2, 3);
        point.Type = GlowPointType.Square;
        Assert.Same(point, Assert.Single(registry.Points));
        Assert.Equal(Color.Red, Assert.Single(registry.Points).Color);
        registry.RemoveGlowPoint(point);
        registry.RemoveGlowPoint(point);
        Assert.Empty(registry.Points);
        registry.AddGlowPoint();
        registry.Dispose();
        registry.Dispose();
        Assert.Empty(registry.Points);
        Assert.Throws<ObjectDisposedException>(() => registry.AddGlowPoint());
    }
}
