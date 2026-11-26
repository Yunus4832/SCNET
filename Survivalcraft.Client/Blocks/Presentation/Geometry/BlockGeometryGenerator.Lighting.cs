using Game.Terrains.Lighting;
using Game.Terrains.Rendering;

namespace Game.Blocks.Presentation.Geometry;

public partial class BlockGeometryGenerator
{
    private const int _cellLightSample = 7;

    private const int _fullBrightSample = 8;

    private const int _wireLightSample = 9;

    private const int _lightbulbSample = 10;

    private const int _heightFaceSample = 1 << 13;

    private readonly int[] _relightCornerKeys = new int[1024];

    private readonly TerrainLightChannels[] _relightCorners = new TerrainLightChannels[1024];

    private bool _recordLighting;

    private Point2 _lightingCenter;

    private Point2 _lastLightReadChunk;

    private int _lastLightReadSection = -1;

    private bool _lastLightReadInterior;

    internal void SetLightReadTarget(TerrainChunkPresentation.SliceWork? target)
    {
        if (target != null) { Array.Clear(_relightCornerKeys); }

        _lightReadTarget = target;
        _lastLightReadSection = -1;
        _lastLightReadInterior = false;
    }

    internal void SetCellLighting(TerrainGeometrySubset subset, int start, int x, int y, int z)
    {
        var response = GetLightResponse(x, y, z);
        var vertices = subset.Vertices;
        for (var i = start; i < vertices.Count; i++) { vertices.Array[i].Lighting = response; }

        if (_recordLighting)
        {
            subset.RecordLighting(start, vertices.Count - start, PackLightCell(x, y, z));
        }
    }

    private int PackLightCell(int x, int y, int z, int kind = _cellLightSample)
    {
        var localX = x - (_lightingCenter.X << 4) + 1;
        var localZ = z - (_lightingCenter.Y << 4) + 1;
        return (uint)localX < 32 && (uint)localZ < 32 && (uint)y <= 256
            ? kind | (localX << 4) | (localZ << 9) | (y << 14)
            : 0;
    }

    private int PackWireLightCell(int cell, int offsetX, int offsetY, int offsetZ)
    {
        if (cell == 0 || (uint)(offsetX + 1) >= 3 || (uint)(offsetY + 1) >= 3 || (uint)(offsetZ + 1) >= 3)
        {
            return 0;
        }

        var offset = (offsetX + 1) * 9 + (offsetY + 1) * 3 + offsetZ + 1;
        return (cell & ~15) | _wireLightSample | (offset << 23);
    }

    private static void RecordHeightFace(
        TerrainGeometrySubset subset,
        int start,
        int face,
        int y,
        bool fullHeight
    )
    {
        var sample = face + 1;
        if (!fullHeight) { sample |= _heightFaceSample | (y << 4); }

        subset.RecordLighting(start, 4, sample);
    }

    internal void RefreshLighting(TerrainGeometrySubset subset, TerrainVertexLightRun run, int start, int end)
    {
        var vertices = subset.Vertices.Array.AsSpan(start, end - start);
        var kind = run.Sample & 15;
        if (kind >= _cellLightSample)
        {
            var x = (_lightingCenter.X << 4) + ((run.Sample >> 4) & 31) - 1;
            var y = (run.Sample >> 14) & 511;
            var z = (_lightingCenter.Y << 4) + ((run.Sample >> 9) & 31) - 1;
            if (kind == _wireLightSample)
            {
                var offset = run.Sample >> 23;
                var owner = ReadLight(x, y, z);
                var neighbor = ReadLight(x + offset / 9 - 1, y + offset / 3 % 3 - 1, z + offset % 3 - 1);
                var ownerResponse = TerrainLightResponse.FromChannels(owner);
                var middle = TerrainLightResponse.FromSamples([owner, neighbor], [.5f, .5f]);
                for (var i = start; i < end; i++)
                {
                    // The six wire vertices alternate between the mounting cell and the blended edge.
                    var slot = (run.End - i) % 6;
                    vertices[i - start].Lighting = slot is 0 or 2 or 3 ? ownerResponse : middle;
                }

                return;
            }

            var response = kind == _fullBrightSample ? TerrainLightResponse.FullBright : GetLightResponse(x, y, z);
            var emissiveMaterial = kind == _lightbulbSample
                ? LightbulbBlockPresentation.GetEmissiveMaterial(
                    LightbulbBlock.GetLightIntensity(Terrain.ExtractData(ReadGeometryCell(x, y, z)))
                )
                : default;
            foreach (ref var vertex in vertices)
            {
                vertex.Lighting = response;
                vertex.emissiveMaterial = emissiveMaterial;
            }

            return;
        }

        var heightFace = (run.Sample & _heightFaceSample) != 0;
        var baseY = (run.Sample >> 4) & 511;
        var upperCorners = kind switch
        {
            1 or 3 => 0b1100,
            2 or 4 => 0b0110,
            5 => 0b1111,
            _ => 0
        };
        for (var i = start; i < end; i++)
        {
            ref var vertex = ref vertices[i - start];
            var x = (int)vertex.X;
            // Height faces are emitted as quads, and their geometry height can differ from the sampled corner.
            var y = heightFace ? baseY + ((upperCorners >> ((i - run.End) & 3)) & 1) : (int)vertex.Y;
            var z = (int)vertex.Z;
            var localX = x - (_lightingCenter.X << 4);
            var localZ = z - (_lightingCenter.Y << 4);
            var cacheable = _lightReadTarget != null && (uint)localX <= 16 && (uint)localZ <= 16 && (uint)y <= 256;
            var key = (((y * 17 + localX) * 17 + localZ) << 3) | kind;
            var slot = (int)((uint)key * 2654435761u >> 22);
            if (cacheable && _relightCornerKeys[slot] == key)
            {
                vertex.Lighting = TerrainLightResponse.FromChannels(_relightCorners[slot]);
                continue;
            }

            var channels = kind switch
            {
                1 => CalculateVertexLightFace0(x, y, z),
                2 => CalculateVertexLightFace1(x, y, z),
                3 => CalculateVertexLightFace2(x, y, z),
                4 => CalculateVertexLightFace3(x, y, z),
                5 => CalculateVertexLightFace4(x, y, z),
                6 => CalculateVertexLightFace5(x, y, z),
                _ => throw new InvalidOperationException("Terrain vertex has no reusable light sample.")
            };
            if (cacheable)
            {
                _relightCornerKeys[slot] = key;
                _relightCorners[slot] = channels;
            }

            vertex.Lighting = TerrainLightResponse.FromChannels(channels);
        }
    }
}
