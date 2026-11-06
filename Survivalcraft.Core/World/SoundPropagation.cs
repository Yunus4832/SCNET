namespace Game.World;

public static class SoundPropagation
{
    public const float UnscaledMinAudibleVolume = 0.05f;

    public static float CalculateAudibleRadius(float volume, float minDistance)
    {
        return minDistance * (1f + volume / UnscaledMinAudibleVolume) / 2f;
    }
}
