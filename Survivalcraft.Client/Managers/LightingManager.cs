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
        var environment = terrain.CaptureLightEnvironment();
        shader.GetParameter("u_lightEnvironment").SetValue(
            new Vector2(
                environment.SkyLightValue,
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

    public static int? SampleEffectiveLight(SubsystemTerrain terrain, int x, int y, int z)
    {
        return SampleSpatialLight(terrain, x, y, z)?.GetEffectiveLight(terrain.CaptureLightEnvironment());
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
        var num = (int)MathUtils.Floor(p.X);
        var num2 = (int)MathUtils.Floor(p.Y);
        var num3 = (int)MathUtils.Floor(p.Z);
        var x = (int)MathUtils.Ceiling(p.X);
        var num4 = (int)MathUtils.Ceiling(p.Y);
        var z = (int)MathUtils.Ceiling(p.Z);
        if (num2 < 0 || num4 > 255)
        {
            return null;
        }

        var l000 = SampleSpatialLight(subsystemTerrain, num, num2, num3);
        var l001 = SampleSpatialLight(subsystemTerrain, num, num2, z);
        var l010 = SampleSpatialLight(subsystemTerrain, num, num4, num3);
        var l011 = SampleSpatialLight(subsystemTerrain, num, num4, z);
        var l100 = SampleSpatialLight(subsystemTerrain, x, num2, num3);
        var l101 = SampleSpatialLight(subsystemTerrain, x, num2, z);
        var l110 = SampleSpatialLight(subsystemTerrain, x, num4, num3);
        var l111 = SampleSpatialLight(subsystemTerrain, x, num4, z);
        if (!l000.HasValue ||
            !l001.HasValue ||
            !l010.HasValue ||
            !l011.HasValue ||
            !l100.HasValue ||
            !l101.HasValue ||
            !l110.HasValue ||
            !l111.HasValue) { return null; }

        var environment = subsystemTerrain.CaptureLightEnvironment();
        var f = p.X - num;
        var f2 = p.Y - num2;
        var f3 = p.Z - num3;
        float x2 = l000.Value.GetEffectiveLight(environment);
        float x3 = l001.Value.GetEffectiveLight(environment);
        float x4 = l010.Value.GetEffectiveLight(environment);
        float x5 = l011.Value.GetEffectiveLight(environment);
        float x6 = l100.Value.GetEffectiveLight(environment);
        float x7 = l101.Value.GetEffectiveLight(environment);
        float x8 = l110.Value.GetEffectiveLight(environment);
        float x9 = l111.Value.GetEffectiveLight(environment);
        var x10 = MathUtils.Lerp(x2, x6, f);
        var x11 = MathUtils.Lerp(x3, x7, f);
        var x12 = MathUtils.Lerp(x4, x8, f);
        var x13 = MathUtils.Lerp(x5, x9, f);
        var x14 = MathUtils.Lerp(x10, x12, f2);
        var x15 = MathUtils.Lerp(x11, x13, f2);
        var num5 = MathUtils.Lerp(x14, x15, f3);
        var num6 = (int)MathUtils.Floor(num5);
        var num7 = (int)MathUtils.Ceiling(num5);
        var f4 = num5 - num6;
        return MathUtils.Lerp(LightIntensityByLightValue[num6], LightIntensityByLightValue[num7], f4);
    }

    private static void CalculateLightingTables()
    {
        Volatile.Write(
            ref _tables,
            new TerrainLightingTables(Configuration.ClientConfiguration.Preferences.Brightness)
        );
    }
}
