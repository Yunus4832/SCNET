namespace Game.Rendering;

/// <summary>Immutable, camera-relative parameters for independent atmosphere and coverage layers.</summary>
internal readonly record struct ViewAtmosphere(
    Vector2 DistanceStartDensity,
    Vector3 WeatherBottomTopDensity,
    float VerticalDistanceScale,
    Vector2 VisibilityStartEnd
)
{
    public float GetDistanceOpacity(Vector3 offset)
    {
        offset.Y *= VerticalDistanceScale;
        return MathUtils.Saturate((offset.Length() - DistanceStartDensity.X) * DistanceStartDensity.Y);
    }

    public float GetBoundaryOpacity(Vector2 offset)
    {
        return VisibilityStartEnd.Y <= 0f
            ? 1f
            : MathUtils.SmoothStep(VisibilityStartEnd.X, VisibilityStartEnd.Y, offset.Length());
    }

    public float GetWeatherOpacity(Vector3 offset)
    {
        var height = WeatherBottomTopDensity.Y - WeatherBottomTopDensity.X;
        if (height <= 0f || WeatherBottomTopDensity.Z <= 0f) { return 0f; }

        float average;
        if (MathF.Abs(offset.Y) < 0.001f)
        {
            var t = MathUtils.Saturate((offset.Y * 0.5f - WeatherBottomTopDensity.X) / height);
            average = 6f * t * (1f - t);
        }
        else
        {
            average = (WeatherIntegral(offset.Y) - WeatherIntegral(0f)) / offset.Y;
        }

        return MathUtils.Saturate(average * WeatherBottomTopDensity.Z * offset.Length());
    }

    public float GetOpacity(Vector3 offset)
    {
        var atmosphere = MathF.Max(GetDistanceOpacity(offset), GetBoundaryOpacity(offset.XZ));
        return 1f - (1f - atmosphere) * (1f - GetWeatherOpacity(offset));
    }

    private float WeatherIntegral(float y)
    {
        var height = WeatherBottomTopDensity.Y - WeatherBottomTopDensity.X;
        var t = MathUtils.Saturate((y - WeatherBottomTopDensity.X) / height);
        return height * t * t * (3f - 2f * t);
    }
}
