using Game.Managers;
using Game.Subsystems;

namespace Survivalcraft.Test.Network;

public sealed class SpatialSoundRadiusTest
{
    [Theory]
    [InlineData(1f, 2f, 21f)]
    [InlineData(1f, 6f, 63f)]
    [InlineData(0.5f, 5f, 27.5f)]
    public void RadiusMatchesUnscaledPlaybackThreshold(float volume, float minDistance, float expected)
    {
        var radius = SubsystemAudio.CalculateAudibleRadius(volume, minDistance);

        Assert.Equal(expected, radius);
        Assert.True(float.IsFinite(radius));
        Assert.Equal(AudioManager.UnscaledMinAudibleVolume,
            volume * new SubsystemAudio().CalculateVolume(radius, minDistance), 5);
    }
}
