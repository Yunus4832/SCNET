namespace Game.Terrains.Generation;

public partial class TerrainContentsGenerator
{
    public Vector3 FindCoarseSpawnPosition()
    {
        var bestPosition = Vector2.Zero;
        var bestScore = float.MinValue;
        for (var alongShoreOffset = 0; alongShoreOffset < 1500; alongShoreOffset += 5)
        {
            for (var inlandOffset = 0; inlandOffset <= 10; inlandOffset += 5)
            {
                for (var shoreAxis = 0; shoreAxis < 2; shoreAxis++)
                {
                    float z;
                    float x;
                    if (shoreAxis == 0)
                    {
                        z = oceanCorner.Y + alongShoreOffset;
                        x = CalculateOceanShoreX(z) + inlandOffset;
                    }
                    else
                    {
                        x = oceanCorner.X + alongShoreOffset;
                        z = CalculateOceanShoreZ(x) + inlandOffset;
                    }

                    var score = ScoreSpawnPosition(Terrain.ToCell(x), Terrain.ToCell(z));
                    if (!(score > bestScore))
                    {
                        continue;
                    }

                    bestPosition = new Vector2(x, z);
                    bestScore = score;
                }
            }
        }

        return new Vector3(bestPosition.X, CalculateHeight(bestPosition.X, bestPosition.Y), bestPosition.Y);
    }

    public float CalculateForestDensity(float x, float z)
    {
        var point = Terrain.ToChunk(new Vector2(x, z));
        var hasDensityBoost = MathUtils.Hash((uint)(point.X + 1000 * point.Y)) % 1000 < 300;
        return MathUtils.Saturate(
            (SimplexNoise.OctavedNoise(point.X, point.Y, 0.1f, 2, 2f, 0.5f) - 0.25f) / 0.2f +
            (hasDensityBoost ? 0.6f : 0f)
        );
    }

    public float ScoreSpawnPosition(int x, int z)
    {
        var temperature = CalculateTemperature(x, z);
        var humidity = CalculateHumidity(x, z);
        var mountainFactor = CalculateMountainRangeFactor(x, z);
        var height = CalculateHeight(x, z);
        var heightMinusXMinusZ = CalculateHeight(x - 8, z - 8);
        var heightMinusXPlusZ = CalculateHeight(x - 8, z + 8);
        var heightPlusXMinusZ = CalculateHeight(x + 8, z - 8);
        var heightPlusXPlusZ = CalculateHeight(x + 8, z + 8);
        var lowerHeight = MathUtils.Min(
            height,
            MathUtils.Max(heightMinusXMinusZ, heightMinusXPlusZ, heightPlusXMinusZ, heightPlusXPlusZ)
        );
        var heightDifference = MathUtils.Max(
                                   height,
                                   MathUtils.Max(
                                       heightMinusXMinusZ,
                                       heightMinusXPlusZ,
                                       heightPlusXMinusZ,
                                       heightPlusXPlusZ
                                   )
                               ) -
                               lowerHeight;
        var forestDensity = 0f;
        var sampleCount = 0;
        for (var offsetX = -4; offsetX <= 4; offsetX += 2)
        {
            for (var offsetZ = -4; offsetZ <= 4; offsetZ += 2)
            {
                forestDensity += CalculateForestDensity(x + offsetX * 16, z + offsetZ * 16);
                sampleCount++;
            }
        }

        forestDensity /= sampleCount;
        var distanceFromOrigin = MathUtils.Max(MathUtils.Abs(x), MathUtils.Abs(z));
        var score = 0f;
        score -= 0.001f * DistanceFromRange(distanceFromOrigin, 0f, 400f);
        switch (_subsystemTerrain.SubsystemGameInfo.WorldSettings.StartingPositionMode)
        {
            case StartingPositionMode.Easy:
                score -= DistanceFromRange(temperature, 10f, 15f);
                score -= DistanceFromRange(humidity, 5f, 15f);
                score -= 2f * DistanceFromRange(height, 67f, 72f);
                score -= 2f * DistanceFromRange(heightDifference, 0f, 4f);
                score -= 30f * DistanceFromRange(mountainFactor, 0f, 0.75f);
                return score - 30f * DistanceFromRange(forestDensity, 0.75f, 1f);
            case StartingPositionMode.Medium:
                score -= DistanceFromRange(temperature, 3f, 4f);
                score -= 2f * DistanceFromRange(height, 67f, 76f);
                score -= 2f * DistanceFromRange(heightDifference, 0f, 6f);
                score -= 30f * DistanceFromRange(mountainFactor, 0f, 0.8f);
                return score - 30f * DistanceFromRange(forestDensity, 0.5f, 1f);
            default:
                score -= DistanceFromRange(temperature, 0f, 0f);
                return score - 2f * DistanceFromRange(height, 67f, 80f);
        }
    }

    public static float DistanceFromRange(float x, float min, float max)
    {
        if (x < min)
        {
            return min - x;
        }

        if (x > max)
        {
            return x - max;
        }

        return 0f;
    }
}
