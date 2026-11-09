namespace Game.TerrainSerializers;

/// <summary>Deterministic read-only world environment queries, without chunk generation capabilities.</summary>
public interface ITerrainEnvironment
{
    int OceanLevel { get; }

    float CalculateOceanShoreDistance(float x, float z);

    float CalculateHeight(float x, float z);

    int CalculateTemperature(float x, float z);

    int CalculateHumidity(float x, float z);

    float CalculateMountainRangeFactor(float x, float z);
}
