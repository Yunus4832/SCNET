namespace Game.Managers;

public static class LightingManager
{
    public const float LightAmbient = 0.5f;

    public static readonly Vector3 DirectionToLight1 = new(0.12f, 0.25f, 0.34f);

    public static readonly Vector3 DirectionToLight2 = new(-0.12f, 0.25f, -0.34f);

    public static readonly float[] LightIntensityByLightValue = new float[16];

    public static readonly float[] LightIntensityByLightValueAndFace = new float[96];

    public static void Initialize()
    {
        SettingsManager.BrightnessChanged += CalculateLightingTables;
        CalculateLightingTables();
    }

    public static float CalculateLighting(Vector3 normal)
    {
        return LightAmbient + MathUtils.Max(Vector3.Dot(normal, DirectionToLight1), 0f) +
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
        var terrain = subsystemTerrain.Terrain;
        if (num2 < 0 || num4 > 255)
        {
            return null;
        }

        var chunkAtCell = terrain.GetChunkAtCell(num, num3, false);
        var chunkAtCell2 = terrain.GetChunkAtCell(x, num3, false);
        var chunkAtCell3 = terrain.GetChunkAtCell(num, z, false);
        var chunkAtCell4 = terrain.GetChunkAtCell(x, z, false);
        if (chunkAtCell is not { State: >= TerrainChunkState.InvalidVertices1 } ||
            chunkAtCell2 is not { State: >= TerrainChunkState.InvalidVertices1 } ||
            chunkAtCell3 is not { State: >= TerrainChunkState.InvalidVertices1 } ||
            chunkAtCell4 is not { State: >= TerrainChunkState.InvalidVertices1 })
        {
            return null;
        }

        var f = p.X - num;
        var f2 = p.Y - num2;
        var f3 = p.Z - num3;
        float x2 = terrain.GetCellLightFast(num, num2, num3);
        float x3 = terrain.GetCellLightFast(num, num2, z);
        float x4 = terrain.GetCellLightFast(num, num4, num3);
        float x5 = terrain.GetCellLightFast(num, num4, z);
        float x6 = terrain.GetCellLightFast(x, num2, num3);
        float x7 = terrain.GetCellLightFast(x, num2, z);
        float x8 = terrain.GetCellLightFast(x, num4, num3);
        float x9 = terrain.GetCellLightFast(x, num4, z);
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
        var brightness = SettingsManager.Current.Brightness;
        var x = MathUtils.Lerp(0f, 0.1f, brightness);
        for (var i = 0; i < 16; i++)
        {
            LightIntensityByLightValue[i] = MathUtils.Saturate(MathUtils.Lerp(x, 1f, MathUtils.Pow(i / 15f, 1.25f)));
        }

        for (var j = 0; j < 6; j++)
        {
            var num = CalculateLighting(CellFace.FaceToVector3(j));
            for (var k = 0; k < 16; k++)
            {
                LightIntensityByLightValueAndFace[k + j * 16] = LightIntensityByLightValue[k] * num;
            }
        }
    }
}
