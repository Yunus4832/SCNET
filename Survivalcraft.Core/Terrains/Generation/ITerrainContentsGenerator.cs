namespace Game.Terrains.Generation;

/// <summary>由世界选用的区块生成器，按顺序分阶段填充区块内容。</summary>
public interface ITerrainContentsGenerator
{
    Vector3 FindCoarseSpawnPosition();

    void GenerateChunkContentsPass1(TerrainChunk chunk);

    void GenerateChunkContentsPass2(TerrainChunk chunk);

    void GenerateChunkContentsPass3(TerrainChunk chunk);

    /// <summary>第四阶段的准备完成后应用最终装饰。</summary>
    void GenerateChunkContentsPass4(TerrainChunk chunk);

    /// <summary>先捕获独立持有的输入，再在地形访问锁外准备不可变的生成数据。</summary>
    Task PrepareChunkContentsPass4Async(TerrainChunk chunk, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>成功将前三阶段的基础数据交给区块后返回 true，调用方可继续第四阶段。默认返回 false，由调用方依次执行前三阶段。</summary>
    bool TryTakeSeedGeneratedChunkBasis(TerrainChunk chunk)
    {
        return false;
    }
}
