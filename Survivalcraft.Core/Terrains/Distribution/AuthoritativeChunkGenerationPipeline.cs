using Game.TerrainSerializers;

namespace Game.Terrains.Distribution;

/// <summary>
///     Owns persistence restore and procedural generation of authoritative chunk contents.
///     Lighting and geometry are deliberately outside this service.
/// </summary>
public sealed class AuthoritativeChunkGenerationPipeline(
    ITerrainContentsGenerator generator,
    Func<TerrainChunk, bool> restorePendingSave,
    Func<TerrainChunk, bool> load,
    Func<TerrainChunk, bool>? contentGenerated = null
)
{
    private readonly Func<TerrainChunk, bool> _contentGenerated = contentGenerated ?? (_ => true);

    private readonly ITerrainContentsGenerator _generator =
        generator ?? throw new ArgumentNullException(nameof(generator));

    private readonly Func<TerrainChunk, bool> _load = load ?? throw new ArgumentNullException(nameof(load));

    private readonly Func<TerrainChunk, bool> _restorePendingSave =
        restorePendingSave ?? throw new ArgumentNullException(nameof(restorePendingSave));

    public bool TryAdvance(TerrainChunk chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        switch (chunk.WorkerState)
        {
            case TerrainChunkState.NotLoaded:
                if (_restorePendingSave(chunk) || _load(chunk))
                {
                    chunk.SetWorkerState(TerrainChunkState.InvalidLight);
                    chunk.IsLoaded = true;
                }
                else
                {
                    chunk.SetWorkerState(TerrainChunkState.InvalidContents1);
                }

                return true;

            case TerrainChunkState.InvalidContents1:
                if (_generator.TryTakeSeedGeneratedChunkBasis(chunk))
                {
                    chunk.SetWorkerState(TerrainChunkState.InvalidContents4);
                }
                else
                {
                    _generator.GenerateChunkContentsPass1(chunk);
                    chunk.SetWorkerState(TerrainChunkState.InvalidContents2);
                }

                return true;

            case TerrainChunkState.InvalidContents2:
                _generator.GenerateChunkContentsPass2(chunk);
                chunk.SetWorkerState(TerrainChunkState.InvalidContents3);
                return true;

            case TerrainChunkState.InvalidContents3:
                _generator.GenerateChunkContentsPass3(chunk);
                chunk.SetWorkerState(TerrainChunkState.InvalidContents4);
                return true;

            case TerrainChunkState.InvalidContents4:
                _generator.GenerateChunkContentsPass4(chunk);
                if (!_contentGenerated(chunk)) { return false; }

                chunk.SetWorkerState(TerrainChunkState.InvalidLight);
                chunk.IsLoaded = true;
                return true;

            default:
                return false;
        }
    }
}
