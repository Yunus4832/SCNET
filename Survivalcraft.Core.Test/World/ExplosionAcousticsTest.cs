using Engine.Core.Mathematics;

using Game.World;

namespace Survivalcraft.Core.Test.World;

public sealed class ExplosionAcousticsTest
{
    [Fact]
    public void SourceUsesPeakPressureAndStableCoordinatesRatherThanAnyListener()
    {
        var points = new[]
        {
            new KeyValuePair<Point3, float>(new Point3(900, 40, 2), 10),
            new KeyValuePair<Point3, float>(new Point3(-700, 80, 9), 50),
            new KeyValuePair<Point3, float>(new Point3(-700, 79, 10), 50),
            new KeyValuePair<Point3, float>(new Point3(-700, 79, 8), 50)
        };
        Assert.Equal(new Point3(-700, 79, 8), ExplosionAcoustics.SelectOrigin(points));
        Assert.Equal(ExplosionAcoustics.SelectOrigin(points), ExplosionAcoustics.SelectOrigin(points.Reverse()));
        Assert.Null(ExplosionAcoustics.SelectOrigin([]));
        Assert.Null(ExplosionAcoustics.SelectOrigin([new KeyValuePair<Point3, float>(Point3.Zero, 0)]));
    }

    [Theory]
    [InlineData(0f, 0f, 0f)]
    [InlineData(1f, 20f, 30f)]
    [InlineData(100f, 20f, 30f)]
    [InlineData(101f, 22f, 35f)]
    [InlineData(4001f, 24f, 40f)]
    [InlineData(20001f, 26f, 50f)]
    [InlineData(100001f, 30f, 70f)]
    [InlineData(1000001f, 40f, 100f)]
    public void AcousticAndGameplayThresholdsPreserveTheExistingScale(
        float pressure,
        float minimumDistance,
        float noiseRadius
    )
    {
        var profile = ExplosionAcoustics.ForPressure(pressure);
        Assert.Equal(minimumDistance, profile.MinimumDistance);
        Assert.Equal(noiseRadius, profile.NoiseRadius);
    }
}
