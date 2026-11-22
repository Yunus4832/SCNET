using Engine.Graphics;

using Game.Terrains.Lighting;

namespace Game.Managers;

public static class LightingManager
{
    public const float LightAmbient = 0.5f;

    public static readonly Vector3 DirectionToLight1 = new(0.12f, 0.25f, 0.34f);

    public static readonly Vector3 DirectionToLight2 = new(-0.12f, 0.25f, -0.34f);

    private static TerrainLightingTables _tables = new(0);

    public static TerrainLightingTables CaptureTables() => Volatile.Read(ref _tables);

    public static ReadOnlySpan<float> LightIntensityByLightValue => CaptureTables().Values;

    public static ReadOnlySpan<float> LightIntensityByLightValueAndFace => CaptureTables().Faces;

    public static void BindTerrainEnvironment(Shader shader, SubsystemTerrain terrain)
    {
        var daylight = CaptureSkyLightValue(terrain);
        shader.GetParameter("u_lightEnvironment").SetValue(
            new Vector2(
                daylight,
                0.1f * Configuration.ClientConfiguration.Preferences.Brightness
            )
        );
    }

    public static TerrainLightChannels? SampleSpatialLight(SubsystemTerrain terrain, int x, int y, int z)
    {
        if (y < 0) { return default(TerrainLightChannels); }

        if (y >= 256) { return new TerrainLightChannels(15, 0); }

        var chunk = terrain.Terrain.GetChunkAtCell(x, z, false);
        if (chunk == null || !terrain.Terrain.Lighting.TryGetPublishedSnapshot(chunk, out var snapshot))
        {
            return null;
        }

        return snapshot.Get(x & 15, y, z & 15);
    }

    public static float CaptureSkyLightValue(SubsystemTerrain terrain)
    {
        var time = terrain.SubsystemGameInfo.TimeOfDay;
        return TerrainLightEnvironment.InterpolateSkyLightValue(
            TerrainLightEnvironment.CalculateIntensity(time, time.TimeOfDay),
            TerrainLightEnvironment.CalculateMoonPhase(time.Day)
        );
    }

    public static float GetEffectiveLight(TerrainLightChannels channels, float daylight) =>
        Math.Max(channels.Block, Math.Max(0f, channels.Sky - (15f - daylight)));

    public static float GetBrightness(float light)
    {
        var values = CaptureTables().Values;
        var lower = (int)MathUtils.Floor(light);
        return MathUtils.Lerp(values[lower], values[Math.Min(lower + 1, 15)], light - lower);
    }

    public static float GetFaceBrightness(float light, int face)
    {
        var values = CaptureTables().Faces;
        var lower = (int)MathUtils.Floor(light);
        return MathUtils.Lerp(values[lower + 16 * face], values[Math.Min(lower + 1, 15) + 16 * face], light - lower);
    }

    public static float? SampleEffectiveLight(SubsystemTerrain terrain, int x, int y, int z)
    {
        var sample = SampleSpatialLight(terrain, x, y, z);
        return sample.HasValue ? GetEffectiveLight(sample.Value, CaptureSkyLightValue(terrain)) : null;
    }

    public static TerrainLightChannels? SampleNeighborLight(SubsystemTerrain terrain, Vector3 position)
    {
        var x = Terrain.ToCell(position.X);
        var y = Terrain.ToCell(position.Y);
        var z = Terrain.ToCell(position.Z);
        TerrainLightChannels? result = null;
        for (var face = 0; face < 6; face++)
        {
            var offset = CellFace.FaceToPoint3(face);
            var sample = SampleSpatialLight(terrain, x + offset.X, y + offset.Y, z + offset.Z);
            if (sample.HasValue) { result = result.GetValueOrDefault().Max(sample.Value); }
        }

        return result;
    }

    public static void Initialize()
    {
        Configuration.ClientConfiguration.BrightnessChanged += CalculateLightingTables;
        CalculateLightingTables();
    }

    public static float CalculateLighting(Vector3 normal)
    {
        return LightAmbient +
               MathUtils.Max(Vector3.Dot(normal, DirectionToLight1), 0f) +
               MathUtils.Max(Vector3.Dot(normal, DirectionToLight2), 0f);
    }

    public static float? CalculateSmoothLight(SubsystemTerrain subsystemTerrain, Vector3 p)
    {
        p -= new Vector3(0.5f);
        var floorX = (int)MathUtils.Floor(p.X);
        var floorY = (int)MathUtils.Floor(p.Y);
        var floorZ = (int)MathUtils.Floor(p.Z);
        var x = (int)MathUtils.Ceiling(p.X);
        var ceilY = (int)MathUtils.Ceiling(p.Y);
        var z = (int)MathUtils.Ceiling(p.Z);
        if (floorY < 0 || ceilY > 255)
        {
            return null;
        }

        var l000 = SampleSpatialLight(subsystemTerrain, floorX, floorY, floorZ);
        var l001 = SampleSpatialLight(subsystemTerrain, floorX, floorY, z);
        var l010 = SampleSpatialLight(subsystemTerrain, floorX, ceilY, floorZ);
        var l011 = SampleSpatialLight(subsystemTerrain, floorX, ceilY, z);
        var l100 = SampleSpatialLight(subsystemTerrain, x, floorY, floorZ);
        var l101 = SampleSpatialLight(subsystemTerrain, x, floorY, z);
        var l110 = SampleSpatialLight(subsystemTerrain, x, ceilY, floorZ);
        var l111 = SampleSpatialLight(subsystemTerrain, x, ceilY, z);
        if (!l000.HasValue ||
            !l001.HasValue ||
            !l010.HasValue ||
            !l011.HasValue ||
            !l100.HasValue ||
            !l101.HasValue ||
            !l110.HasValue ||
            !l111.HasValue) { return null; }

        var daylight = CaptureSkyLightValue(subsystemTerrain);
        var f = p.X - floorX;
        var f2 = p.Y - floorY;
        var f3 = p.Z - floorZ;
        var x2 = GetEffectiveLight(l000.Value, daylight);
        var x3 = GetEffectiveLight(l001.Value, daylight);
        var x4 = GetEffectiveLight(l010.Value, daylight);
        var x5 = GetEffectiveLight(l011.Value, daylight);
        var x6 = GetEffectiveLight(l100.Value, daylight);
        var x7 = GetEffectiveLight(l101.Value, daylight);
        var x8 = GetEffectiveLight(l110.Value, daylight);
        var x9 = GetEffectiveLight(l111.Value, daylight);
        var x10 = MathUtils.Lerp(x2, x6, f);
        var x11 = MathUtils.Lerp(x3, x7, f);
        var x12 = MathUtils.Lerp(x4, x8, f);
        var x13 = MathUtils.Lerp(x5, x9, f);
        var x14 = MathUtils.Lerp(x10, x12, f2);
        var x15 = MathUtils.Lerp(x11, x13, f2);
        var interpolatedLight = MathUtils.Lerp(x14, x15, f3);
        return GetBrightness(Math.Clamp(interpolatedLight, 0f, 15f));
    }

    private static void CalculateLightingTables()
    {
        Volatile.Write(
            ref _tables,
            new TerrainLightingTables(Configuration.ClientConfiguration.Preferences.Brightness)
        );
    }
}
