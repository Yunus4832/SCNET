namespace Game.Terrains.Generation;

/// <summary>提供确定性的只读世界环境查询，不生成区块。</summary>
public interface ITerrainEnvironment
{
    int OceanLevel { get; }

    float CalculateOceanShoreDistance(float x, float z);

    float CalculateHeight(float x, float z);

    int CalculateTemperature(float x, float z);

    int CalculateHumidity(float x, float z);

    float CalculateMountainRangeFactor(float x, float z);
}
