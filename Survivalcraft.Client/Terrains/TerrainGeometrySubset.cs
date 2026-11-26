using Game.Terrains.Rendering;

namespace Game.Terrains;

public class TerrainGeometrySubset
{
    public readonly DynamicArray<int> Indices = [];

    public DynamicArray<TerrainVertex> Vertices = [];

    internal List<TerrainBlockIndexRange>? RemovableBlocks { get; private set; }

    internal void RecordRemovableBlock(int firstIndex, int cellIndex)
    {
        if (Indices.Count > firstIndex)
        {
            (RemovableBlocks ??= []).Add(new TerrainBlockIndexRange(firstIndex, Indices.Count, cellIndex));
        }
    }

    internal void ClearGeometry()
    {
        Vertices.Clear();
        Indices.Clear();
        RemovableBlocks?.Clear();
        ClearLighting();
    }

    private TerrainVertexLightRun _firstLightRun;

    private DynamicArray<TerrainVertexLightRun>? _additionalLightRuns;

    internal int LightingRunCount => _firstLightRun.End > 0 ? 1 + (_additionalLightRuns?.Count ?? 0) : 0;

    internal TerrainVertexLightRun GetLightingRun(int index) =>
        index == 0 ? _firstLightRun : _additionalLightRuns![index - 1];

    internal bool HasCompleteLighting
    {
        get
        {
            var count = LightingRunCount;
            return count != 0 && GetLightingRun(count - 1).End == Vertices.Count;
        }
    }

    internal void ClearLighting()
    {
        _firstLightRun = default;
        _additionalLightRuns?.Clear();
    }

    internal void RecordLighting(int start, int count, int sample)
    {
        if (count == 0 || _firstLightRun.End < 0) { return; }

        var last = _additionalLightRuns is { Count: > 0 } runs ? runs[runs.Count - 1] : _firstLightRun;
        if (sample == 0 || last.End != start)
        {
            // A gap or an unsupported generator requires rebuilding the entire subset.
            _firstLightRun = new TerrainVertexLightRun(-1, 0);
            _additionalLightRuns?.Clear();
            return;
        }

        var run = new TerrainVertexLightRun(start + count, sample);
        if (last.End == 0) { _firstLightRun = run; }
        else if (last.Sample == sample)
        {
            if (_additionalLightRuns is { Count: > 0 } existing) { existing[existing.Count - 1] = run; }
            else { _firstLightRun = run; }
        }
        else { (_additionalLightRuns ??= []).Add(run); }
    }

    public TerrainGeometrySubset()
    {
    }

    public TerrainGeometrySubset(DynamicArray<TerrainVertex> vertices, DynamicArray<int> indices)
    {
        Vertices = vertices;
        Indices = indices;
    }

    public void Dispose()
    {
    }
}

/// <summary>记录可单独跳过的方块模型在索引缓冲区中的范围。</summary>
internal readonly record struct TerrainBlockIndexRange(int Start, int End, int CellIndex);
