using Game.Terrains.Lighting;

namespace Game.Terrains.Rendering;

/// <summary>依赖一个或多个重叠光照事务的已发布网格资源。</summary>
internal sealed class TerrainLightUploadGroup
{
    internal HashSet<TerrainChunk> Members { get; } = [];

    internal HashSet<HashSet<TerrainChunk>> Edits { get; } = new(ReferenceEqualityComparer.Instance);

    internal Dictionary<TerrainLightPresentationBatch, TerrainLightPresentationBatch.Source[]> Sources { get; } = [];

    internal static bool IsAffected(
        TerrainChunk chunk,
        TerrainChunkPresentation presentation,
        TerrainLightPresentationBatch.Source source
    )
    {
        return chunk.Coords == source.Allocation.Coords && chunk.AllocationGeneration == source.Allocation.Generation ||
               presentation.SamplesLighting(chunk, source);
    }

    internal bool Matches(TerrainLightUploadGroup other)
    {
        return Members.SetEquals(other.Members) &&
               Edits.SetEquals(other.Edits) &&
               Sources.Count == other.Sources.Count &&
               Sources.All(pair => other.Sources.TryGetValue(pair.Key, out var sources) &&
                                   pair.Value.AsSpan().SequenceEqual(sources)
               );
    }

    internal void Include(TerrainLightUploadGroup other)
    {
        Members.UnionWith(other.Members);
        Edits.UnionWith(other.Edits);
        foreach (var (batch, sources) in other.Sources) { Sources.Add(batch, sources); }
    }
}
