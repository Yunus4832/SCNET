using Game.Modding.Blocks;
using Game.Terrains.Lighting;
using Game.Terrains.Rendering;
using Game.World.Presentation;

namespace Game.Blocks.Presentation.Geometry;

public partial class BlockGeometryGenerator
{
    private static readonly Vector2[] _textureCoordinates =
    [
        new(0.001f, 0.999f),
        new(0.999f, 0.999f),
        new(0.999f, 0.001f),
        new(0.001f, 0.001f),
        new(0.001f, 0.999f),
        new(0.999f, 0.999f),
        new(0.999f, 0.001f),
        new(0.001f, 0.001f)
    ];

    private readonly SubsystemElectricity _subsystemElectricity;

    private readonly Func<int, int, int, int> _readGeometryCell;

    private TerrainChunk?[]? _geometryNeighborhood;

    private Point2 _geometryCenter;

    public readonly SubsystemFurnitureBlockBehavior SubsystemFurnitureBlockBehavior;

    public readonly SubsystemMetersBlockBehavior SubsystemMetersBlockBehavior;

    public readonly SubsystemPalette SubsystemPalette;

    private readonly SubsystemTerrain _subsystemTerrain;

    public SubsystemTerrain SubsystemTerrain => _subsystemTerrain;

    public Point3 LightOrigin { get; set; }

    private readonly Dictionary<Point2, TerrainLightSnapshot?> _lightSnapshots = [];

    private TerrainLightSnapshot?[]? _nearbyLightSnapshots;

    private TerrainChunkPresentation.SliceWork? _lightReadTarget;

    private readonly record struct LitCell(int Value, TerrainLightChannels Light);

    public readonly Terrain Terrain;

    public BlockPresentationCatalog Presentations { get; }

    private readonly CornerLights[] _cornerLightsByFace = new CornerLights[6];

    private Point3 _cornerLightsPosition;

    private readonly Dictionary<(int Face, Vector3 Position), TerrainLightResponse> _modelResponses = [];

    private readonly DynamicArray<ElectricConnectionPath> _tmpConnectionPaths = [];

    private readonly bool[] _visibleSides = new bool[6];

    public BlockGeometryGenerator(
        Terrain terrain,
        SubsystemTerrain subsystemTerrain,
        SubsystemElectricity subsystemElectricity,
        SubsystemFurnitureBlockBehavior subsystemFurnitureBlockBehavior,
        SubsystemMetersBlockBehavior subsystemMetersBlockBehavior,
        SubsystemPalette subsystemPalette
    )
    {
        Terrain = terrain;
        _readGeometryCell = terrain.GetCellValueForGeometry;
        _subsystemTerrain = subsystemTerrain;
        Presentations = subsystemTerrain.Project.FindSubsystem<SubsystemBlockPresentation>(true)!.Catalog;
        _subsystemElectricity = subsystemElectricity;
        SubsystemFurnitureBlockBehavior = subsystemFurnitureBlockBehavior;
        SubsystemMetersBlockBehavior = subsystemMetersBlockBehavior;
        SubsystemPalette = subsystemPalette;
        ResetCache();
    }

    public void ResetCache()
    {
        _cornerLightsPosition = new Point3(2147483647);
        _lightSnapshots.Clear();
        _nearbyLightSnapshots = null;
        _modelResponses.Clear();
        _recordLighting = false;
    }

    internal void SetGeometryNeighborhood(Point2 center, TerrainChunk?[]? neighbors)
    {
        _geometryCenter = center;
        _geometryNeighborhood = neighbors;
    }

    private TerrainChunk? FindGeometryChunk(int x, int z)
    {
        if (_geometryNeighborhood is { } neighbors)
        {
            var dx = (x >> 4) - _geometryCenter.X + 1;
            var dz = (z >> 4) - _geometryCenter.Y + 1;
            if ((uint)dx < 3 && (uint)dz < 3) { return neighbors[dx * 3 + dz]; }
        }

        return Terrain.GetChunkAtCell(x, z, false);
    }

    private TerrainChunk GetGeometryChunk(int x, int z) => FindGeometryChunk(x, z) ?? Terrain.GetChunkAtCell(x, z)!;

    internal int ReadFluidNeighbor(int x, int y, int z, int fallback)
    {
        var chunk = FindGeometryChunk(x, z);
        return chunk is { HasContents: true }
            ? chunk.GetCellValueForGeometry(x & 15, y, z & 15)
            : fallback;
    }

    private int ReadGeometryCell(int x, int y, int z)
    {
        return Terrain.IsCellValid(x, y, z)
            ? FindGeometryChunk(x, z)?.GetCellValueForGeometry(x & 15, y, z & 15) ?? 0
            : 0;
    }

    internal void SetLightSnapshots(Point2 center, Game.Terrains.Lighting.TerrainLightSnapshot?[] snapshots)
    {
        _lightingCenter = center;
        // Cube recipes recover their integer sample positions from the emitted vertices.
        _recordLighting = center.X is >= -524288 and < 524288 && center.Y is >= -524288 and < 524288;
        _nearbyLightSnapshots = snapshots;
    }

    private static void SetupCornerVertex(
        float x,
        float y,
        float z,
        Color color,
        TerrainLightChannels light,
        int face,
        int textureSlot,
        int textureSlotCount,
        int corner,
        ref TerrainVertex vertex
    )
    {
        var faceBrightness = LightingManager.CalculateLighting(CellFace.FaceToVector3(face));
        var color2 = new Color(
            (byte)(color.R * faceBrightness),
            (byte)(color.G * faceBrightness),
            (byte)(color.B * faceBrightness),
            color.A
        );
        var tx = (_textureCoordinates[corner].X + textureSlot % textureSlotCount) / textureSlotCount;
        var ty = (_textureCoordinates[corner].Y + textureSlot / textureSlotCount) / textureSlotCount;
        SetupVertex(x, y, z, color2, tx, ty, ref vertex);
        vertex.Lighting = TerrainLightResponse.FromChannels(light);
    }

    public static void SetupLitCornerVertex(
        float x,
        float y,
        float z,
        Color color,
        int textureSlot,
        int corner,
        ref TerrainVertex vertex
    )
    {
        SetupLitCornerVertex(x, y, z, color, textureSlot, 16, corner, ref vertex);
    }

    private static void SetupLitCornerVertex(
        float x,
        float y,
        float z,
        Color color,
        int textureSlot,
        int textureSlotCount,
        int corner,
        ref TerrainVertex vertex
    )
    {
        var tx = (_textureCoordinates[corner].X + textureSlot % textureSlotCount) / textureSlotCount;
        var ty = (_textureCoordinates[corner].Y + textureSlot / textureSlotCount) / textureSlotCount;
        SetupVertex(x, y, z, color, tx, ty, ref vertex);
    }

    private static void SetupVertex(
        float x,
        float y,
        float z,
        Color color,
        float tx,
        float ty,
        ref TerrainVertex vertex
    )
    {
        vertex.X = x;
        vertex.Y = y;
        vertex.Z = z;
        vertex.Tx = (short)(tx * 32767f);
        vertex.Ty = (short)(ty * 32767f);
        vertex.Color = color;
        vertex.Lighting = TerrainLightResponse.FullBright;
    }

    public void GenerateCrossingFaceVertices(
        BlockPresentation block,
        int value,
        int x,
        int y,
        int z,
        Color color,
        int textureSlot,
        TerrainGeometrySubset subset
    )
    {
        var vertices = subset.Vertices;
        var indices = subset.Indices;
        var upwardBrightness = LightingManager.CalculateLighting(Vector3.UnitY);
        var color2 = new Color(
            (byte)(color.R * upwardBrightness),
            (byte)(color.G * upwardBrightness),
            (byte)(color.B * upwardBrightness),
            color.A
        );
        var count = vertices.Count;
        vertices.Count += 8;
        var textureSlotCount = block.GetTextureSlotCount(value);
        if ((x & 1) == 0)
        {
            SetupLitCornerVertex(x, y, z, color2, textureSlot, textureSlotCount, 0, ref vertices.Array[count]);
            SetupLitCornerVertex(
                x + 1,
                y,
                z + 1,
                color2,
                textureSlot,
                textureSlotCount,
                1,
                ref vertices.Array[count + 1]
            );
            SetupLitCornerVertex(
                x + 1,
                y + 1,
                z + 1,
                color2,
                textureSlot,
                textureSlotCount,
                2,
                ref vertices.Array[count + 2]
            );
            SetupLitCornerVertex(x, y + 1, z, color2, textureSlot, textureSlotCount, 3, ref vertices.Array[count + 3]);
        }
        else
        {
            SetupLitCornerVertex(x, y, z, color2, textureSlot, textureSlotCount, 1, ref vertices.Array[count]);
            SetupLitCornerVertex(
                x + 1,
                y,
                z + 1,
                color2,
                textureSlot,
                textureSlotCount,
                0,
                ref vertices.Array[count + 1]
            );
            SetupLitCornerVertex(
                x + 1,
                y + 1,
                z + 1,
                color2,
                textureSlot,
                textureSlotCount,
                3,
                ref vertices.Array[count + 2]
            );
            SetupLitCornerVertex(x, y + 1, z, color2, textureSlot, textureSlotCount, 2, ref vertices.Array[count + 3]);
        }

        if ((z & 1) == 0)
        {
            SetupLitCornerVertex(x, y, z + 1, color2, textureSlot, textureSlotCount, 0, ref vertices.Array[count + 4]);
            SetupLitCornerVertex(x + 1, y, z, color2, textureSlot, textureSlotCount, 1, ref vertices.Array[count + 5]);
            SetupLitCornerVertex(
                x + 1,
                y + 1,
                z,
                color2,
                textureSlot,
                textureSlotCount,
                2,
                ref vertices.Array[count + 6]
            );
            SetupLitCornerVertex(
                x,
                y + 1,
                z + 1,
                color2,
                textureSlot,
                textureSlotCount,
                3,
                ref vertices.Array[count + 7]
            );
        }
        else
        {
            SetupLitCornerVertex(x, y, z + 1, color2, textureSlot, textureSlotCount, 1, ref vertices.Array[count + 4]);
            SetupLitCornerVertex(x + 1, y, z, color2, textureSlot, textureSlotCount, 0, ref vertices.Array[count + 5]);
            SetupLitCornerVertex(
                x + 1,
                y + 1,
                z,
                color2,
                textureSlot,
                textureSlotCount,
                3,
                ref vertices.Array[count + 6]
            );
            SetupLitCornerVertex(
                x,
                y + 1,
                z + 1,
                color2,
                textureSlot,
                textureSlotCount,
                2,
                ref vertices.Array[count + 7]
            );
        }

        var count2 = indices.Count;
        indices.Count += 24;
        indices.Array[count2] = count;
        indices.Array[count2 + 1] = count + 1;
        indices.Array[count2 + 2] = count + 2;
        indices.Array[count2 + 3] = count + 2;
        indices.Array[count2 + 4] = count + 1;
        indices.Array[count2 + 5] = count;
        indices.Array[count2 + 6] = count + 2;
        indices.Array[count2 + 7] = count + 3;
        indices.Array[count2 + 8] = count;
        indices.Array[count2 + 9] = count;
        indices.Array[count2 + 10] = count + 3;
        indices.Array[count2 + 11] = count + 2;
        indices.Array[count2 + 12] = count + 4;
        indices.Array[count2 + 13] = count + 5;
        indices.Array[count2 + 14] = count + 6;
        indices.Array[count2 + 15] = count + 6;
        indices.Array[count2 + 16] = count + 5;
        indices.Array[count2 + 17] = count + 4;
        indices.Array[count2 + 18] = count + 6;
        indices.Array[count2 + 19] = count + 7;
        indices.Array[count2 + 20] = count + 4;
        indices.Array[count2 + 21] = count + 4;
        indices.Array[count2 + 22] = count + 7;
        indices.Array[count2 + 23] = count + 6;
        SetCellLighting(subset, count, x, y, z);
    }

    public void GenerateCubeVertices(
        BlockPresentation block,
        int value,
        int x,
        int y,
        int z,
        Color color,
        TerrainGeometrySubset[] subsetsByFace
    )
    {
        var chunkAtCell = GetGeometryChunk(x, z);
        var chunkAtCell2 = GetGeometryChunk(x, z + 1);
        var chunkAtCell3 = GetGeometryChunk(x + 1, z);
        var chunkAtCell4 = GetGeometryChunk(x, z - 1);
        var chunkAtCell5 = GetGeometryChunk(x - 1, z);
        var cellValueFast = chunkAtCell2.GetCellValueForGeometry(x & 0xF, y, (z + 1) & 0xF);
        var textureSlotCount = block.GetTextureSlotCount(value);
        if (block.ShouldGenerateFace(_subsystemTerrain, 0, value, cellValueFast))
        {
            var vertices = subsetsByFace[0].Vertices;
            var indices = subsetsByFace[0].Indices;
            var faceTextureSlot = block.GetFaceTextureSlot(0, value);
            var count = vertices.Count;
            vertices.Count += 4;
            if (_recordLighting) { subsetsByFace[0].RecordLighting(count, 4, 1); }

            SetupCubeVertexFace0(
                x,
                y,
                z + 1,
                1f,
                0,
                faceTextureSlot,
                textureSlotCount,
                color,
                ref vertices.Array[count]
            ); //A
            SetupCubeVertexFace0(
                x + 1,
                y,
                z + 1,
                1f,
                1,
                faceTextureSlot,
                textureSlotCount,
                color,
                ref vertices.Array[count + 1]
            ); //B
            SetupCubeVertexFace0(
                x + 1,
                y + 1,
                z + 1,
                1f,
                2,
                faceTextureSlot,
                textureSlotCount,
                color,
                ref vertices.Array[count + 2]
            ); //C
            SetupCubeVertexFace0(
                x,
                y + 1,
                z + 1,
                1f,
                3,
                faceTextureSlot,
                textureSlotCount,
                color,
                ref vertices.Array[count + 3]
            ); //D
            var count2 = indices.Count;
            indices.Count += 6;
            indices.Array[count2] = count; //A
            indices.Array[count2 + 1] = count + 2; //C
            indices.Array[count2 + 2] = count + 1; //B
            indices.Array[count2 + 3] = count + 2; //C
            indices.Array[count2 + 4] = count; //A
            indices.Array[count2 + 5] = count + 3; //D
        }

        cellValueFast = chunkAtCell3.GetCellValueForGeometry((x + 1) & 0xF, y, z & 0xF);
        if (block.ShouldGenerateFace(_subsystemTerrain, 1, value, cellValueFast))
        {
            var vertices2 = subsetsByFace[1].Vertices;
            var indices2 = subsetsByFace[1].Indices;
            var faceTextureSlot2 = block.GetFaceTextureSlot(1, value);
            var count3 = vertices2.Count;
            vertices2.Count += 4;
            if (_recordLighting) { subsetsByFace[1].RecordLighting(count3, 4, 2); }

            SetupCubeVertexFace1(
                x + 1,
                y,
                z,
                1f,
                1,
                faceTextureSlot2,
                textureSlotCount,
                color,
                ref vertices2.Array[count3]
            );
            SetupCubeVertexFace1(
                x + 1,
                y + 1,
                z,
                1f,
                2,
                faceTextureSlot2,
                textureSlotCount,
                color,
                ref vertices2.Array[count3 + 1]
            );
            SetupCubeVertexFace1(
                x + 1,
                y + 1,
                z + 1,
                1f,
                3,
                faceTextureSlot2,
                textureSlotCount,
                color,
                ref vertices2.Array[count3 + 2]
            );
            SetupCubeVertexFace1(
                x + 1,
                y,
                z + 1,
                1f,
                0,
                faceTextureSlot2,
                textureSlotCount,
                color,
                ref vertices2.Array[count3 + 3]
            );
            var count4 = indices2.Count;
            indices2.Count += 6;
            indices2.Array[count4] = count3;
            indices2.Array[count4 + 1] = count3 + 2;
            indices2.Array[count4 + 2] = count3 + 1;
            indices2.Array[count4 + 3] = count3 + 2;
            indices2.Array[count4 + 4] = count3;
            indices2.Array[count4 + 5] = count3 + 3;
        }

        cellValueFast = chunkAtCell4.GetCellValueForGeometry(x & 0xF, y, (z - 1) & 0xF);
        if (block.ShouldGenerateFace(_subsystemTerrain, 2, value, cellValueFast))
        {
            var vertices3 = subsetsByFace[2].Vertices;
            var indices3 = subsetsByFace[2].Indices;
            var faceTextureSlot3 = block.GetFaceTextureSlot(2, value);
            var count5 = vertices3.Count;
            vertices3.Count += 4;
            if (_recordLighting) { subsetsByFace[2].RecordLighting(count5, 4, 3); }

            SetupCubeVertexFace2(
                x,
                y,
                z,
                1f,
                1,
                faceTextureSlot3,
                textureSlotCount,
                color,
                ref vertices3.Array[count5]
            );
            SetupCubeVertexFace2(
                x + 1,
                y,
                z,
                1f,
                0,
                faceTextureSlot3,
                textureSlotCount,
                color,
                ref vertices3.Array[count5 + 1]
            );
            SetupCubeVertexFace2(
                x + 1,
                y + 1,
                z,
                1f,
                3,
                faceTextureSlot3,
                textureSlotCount,
                color,
                ref vertices3.Array[count5 + 2]
            );
            SetupCubeVertexFace2(
                x,
                y + 1,
                z,
                1f,
                2,
                faceTextureSlot3,
                textureSlotCount,
                color,
                ref vertices3.Array[count5 + 3]
            );
            var count6 = indices3.Count;
            indices3.Count += 6;
            indices3.Array[count6] = count5;
            indices3.Array[count6 + 1] = count5 + 1;
            indices3.Array[count6 + 2] = count5 + 2;
            indices3.Array[count6 + 3] = count5 + 2;
            indices3.Array[count6 + 4] = count5 + 3;
            indices3.Array[count6 + 5] = count5;
        }

        cellValueFast = chunkAtCell5.GetCellValueForGeometry((x - 1) & 0xF, y, z & 0xF);
        if (block.ShouldGenerateFace(_subsystemTerrain, 3, value, cellValueFast))
        {
            var vertices4 = subsetsByFace[3].Vertices;
            var indices4 = subsetsByFace[3].Indices;
            var faceTextureSlot4 = block.GetFaceTextureSlot(3, value);
            var count7 = vertices4.Count;
            vertices4.Count += 4;
            if (_recordLighting) { subsetsByFace[3].RecordLighting(count7, 4, 4); }

            SetupCubeVertexFace3(
                x,
                y,
                z,
                1f,
                0,
                faceTextureSlot4,
                textureSlotCount,
                color,
                ref vertices4.Array[count7]
            );
            SetupCubeVertexFace3(
                x,
                y + 1,
                z,
                1f,
                3,
                faceTextureSlot4,
                textureSlotCount,
                color,
                ref vertices4.Array[count7 + 1]
            );
            SetupCubeVertexFace3(
                x,
                y + 1,
                z + 1,
                1f,
                2,
                faceTextureSlot4,
                textureSlotCount,
                color,
                ref vertices4.Array[count7 + 2]
            );
            SetupCubeVertexFace3(
                x,
                y,
                z + 1,
                1f,
                1,
                faceTextureSlot4,
                textureSlotCount,
                color,
                ref vertices4.Array[count7 + 3]
            );
            var count8 = indices4.Count;
            indices4.Count += 6;
            indices4.Array[count8] = count7;
            indices4.Array[count8 + 1] = count7 + 1;
            indices4.Array[count8 + 2] = count7 + 2;
            indices4.Array[count8 + 3] = count7 + 2;
            indices4.Array[count8 + 4] = count7 + 3;
            indices4.Array[count8 + 5] = count7;
        }

        cellValueFast = chunkAtCell.GetCellValueForGeometry(x & 0xF, y + 1, z & 0xF);
        if (block.ShouldGenerateFace(_subsystemTerrain, 4, value, cellValueFast))
        {
            var vertices5 = subsetsByFace[4].Vertices;
            var indices5 = subsetsByFace[4].Indices;
            var faceTextureSlot5 = block.GetFaceTextureSlot(4, value);
            var count9 = vertices5.Count;
            vertices5.Count += 4;
            if (_recordLighting) { subsetsByFace[4].RecordLighting(count9, 4, 5); }

            SetupCubeVertexFace4(
                x,
                y + 1,
                z,
                1f,
                3,
                faceTextureSlot5,
                textureSlotCount,
                color,
                ref vertices5.Array[count9]
            );
            SetupCubeVertexFace4(
                x + 1,
                y + 1,
                z,
                1f,
                2,
                faceTextureSlot5,
                textureSlotCount,
                color,
                ref vertices5.Array[count9 + 1]
            );
            SetupCubeVertexFace4(
                x + 1,
                y + 1,
                z + 1,
                1f,
                1,
                faceTextureSlot5,
                textureSlotCount,
                color,
                ref vertices5.Array[count9 + 2]
            );
            SetupCubeVertexFace4(
                x,
                y + 1,
                z + 1,
                1f,
                0,
                faceTextureSlot5,
                textureSlotCount,
                color,
                ref vertices5.Array[count9 + 3]
            );
            var count10 = indices5.Count;
            indices5.Count += 6;
            indices5.Array[count10] = count9;
            indices5.Array[count10 + 1] = count9 + 1;
            indices5.Array[count10 + 2] = count9 + 2;
            indices5.Array[count10 + 3] = count9 + 2;
            indices5.Array[count10 + 4] = count9 + 3;
            indices5.Array[count10 + 5] = count9;
        }

        cellValueFast = chunkAtCell.GetCellValueForGeometry(x & 0xF, y - 1, z & 0xF);
        if (block.ShouldGenerateFace(_subsystemTerrain, 5, value, cellValueFast))
        {
            var vertices6 = subsetsByFace[5].Vertices;
            var indices6 = subsetsByFace[5].Indices;
            var faceTextureSlot6 = block.GetFaceTextureSlot(5, value);
            var count11 = vertices6.Count;
            vertices6.Count += 4;
            if (_recordLighting) { subsetsByFace[5].RecordLighting(count11, 4, 6); }

            SetupCubeVertexFace5(
                x,
                y,
                z,
                1f,
                0,
                faceTextureSlot6,
                textureSlotCount,
                color,
                ref vertices6.Array[count11]
            );
            SetupCubeVertexFace5(
                x + 1,
                y,
                z,
                1f,
                1,
                faceTextureSlot6,
                textureSlotCount,
                color,
                ref vertices6.Array[count11 + 1]
            );
            SetupCubeVertexFace5(
                x + 1,
                y,
                z + 1,
                1f,
                2,
                faceTextureSlot6,
                textureSlotCount,
                color,
                ref vertices6.Array[count11 + 2]
            );
            SetupCubeVertexFace5(
                x,
                y,
                z + 1,
                1f,
                3,
                faceTextureSlot6,
                textureSlotCount,
                color,
                ref vertices6.Array[count11 + 3]
            );
            var count12 = indices6.Count;
            indices6.Count += 6;
            indices6.Array[count12] = count11;
            indices6.Array[count12 + 1] = count11 + 2;
            indices6.Array[count12 + 2] = count11 + 1;
            indices6.Array[count12 + 3] = count11 + 2;
            indices6.Array[count12 + 4] = count11;
            indices6.Array[count12 + 5] = count11 + 3;
        }
    }

    public void GenerateCubeVertices(
        BlockPresentation block,
        int value,
        int x,
        int y,
        int z,
        float height11,
        float height21,
        float height22,
        float height12,
        Color sideColor,
        Color topColor11,
        Color topColor21,
        Color topColor22,
        Color topColor12,
        int overrideTopTextureSlot,
        TerrainGeometrySubset[] subsetsByFace,
        int? unknownSide = null,
        bool topOnly = false
    )
    {
        var cellValueFast = topOnly ? value : ReadSide(x, z + 1);
        var textureSlotCount = block.GetTextureSlotCount(value);
        var fullHeight = height11 == 1f && height21 == 1f && height22 == 1f && height12 == 1f;
        if (!topOnly && block.ShouldGenerateFace(_subsystemTerrain, 0, value, cellValueFast))
        {
            var vertices = subsetsByFace[0].Vertices;
            var indices = subsetsByFace[0].Indices;
            var faceTextureSlot = block.GetFaceTextureSlot(0, value);
            var count = vertices.Count;
            vertices.Count += 4;
            if (_recordLighting) { RecordHeightFace(subsetsByFace[0], count, 0, y, fullHeight); }

            SetupCubeVertexFace0(
                x,
                y,
                z + 1,
                1f,
                0,
                faceTextureSlot,
                textureSlotCount,
                sideColor,
                ref vertices.Array[count]
            );
            SetupCubeVertexFace0(
                x + 1,
                y,
                z + 1,
                1f,
                1,
                faceTextureSlot,
                textureSlotCount,
                sideColor,
                ref vertices.Array[count + 1]
            );
            SetupCubeVertexFace0(
                x + 1,
                y + 1,
                z + 1,
                height22,
                2,
                faceTextureSlot,
                textureSlotCount,
                sideColor,
                ref vertices.Array[count + 2]
            );
            SetupCubeVertexFace0(
                x,
                y + 1,
                z + 1,
                height12,
                3,
                faceTextureSlot,
                textureSlotCount,
                sideColor,
                ref vertices.Array[count + 3]
            );
            var count2 = indices.Count;
            indices.Count += 6;
            indices.Array[count2] = count;
            indices.Array[count2 + 1] = count + 2;
            indices.Array[count2 + 2] = count + 1;
            indices.Array[count2 + 3] = count + 2;
            indices.Array[count2 + 4] = count;
            indices.Array[count2 + 5] = count + 3;
        }

        cellValueFast = topOnly ? value : ReadSide(x + 1, z);
        if (!topOnly && block.ShouldGenerateFace(_subsystemTerrain, 1, value, cellValueFast))
        {
            var vertices2 = subsetsByFace[1].Vertices;
            var indices2 = subsetsByFace[1].Indices;
            var faceTextureSlot2 = block.GetFaceTextureSlot(1, value);
            var count3 = vertices2.Count;
            vertices2.Count += 4;
            if (_recordLighting) { RecordHeightFace(subsetsByFace[1], count3, 1, y, fullHeight); }

            SetupCubeVertexFace1(
                x + 1,
                y,
                z,
                1f,
                1,
                faceTextureSlot2,
                textureSlotCount,
                sideColor,
                ref vertices2.Array[count3]
            );
            SetupCubeVertexFace1(
                x + 1,
                y + 1,
                z,
                height21,
                2,
                faceTextureSlot2,
                textureSlotCount,
                sideColor,
                ref vertices2.Array[count3 + 1]
            );
            SetupCubeVertexFace1(
                x + 1,
                y + 1,
                z + 1,
                height22,
                3,
                faceTextureSlot2,
                textureSlotCount,
                sideColor,
                ref vertices2.Array[count3 + 2]
            );
            SetupCubeVertexFace1(
                x + 1,
                y,
                z + 1,
                1f,
                0,
                faceTextureSlot2,
                textureSlotCount,
                sideColor,
                ref vertices2.Array[count3 + 3]
            );
            var count4 = indices2.Count;
            indices2.Count += 6;
            indices2.Array[count4] = count3;
            indices2.Array[count4 + 1] = count3 + 2;
            indices2.Array[count4 + 2] = count3 + 1;
            indices2.Array[count4 + 3] = count3 + 2;
            indices2.Array[count4 + 4] = count3;
            indices2.Array[count4 + 5] = count3 + 3;
        }

        cellValueFast = topOnly ? value : ReadSide(x, z - 1);
        if (!topOnly && block.ShouldGenerateFace(_subsystemTerrain, 2, value, cellValueFast))
        {
            var vertices3 = subsetsByFace[2].Vertices;
            var indices3 = subsetsByFace[2].Indices;
            var faceTextureSlot3 = block.GetFaceTextureSlot(2, value);
            var count5 = vertices3.Count;
            vertices3.Count += 4;
            if (_recordLighting) { RecordHeightFace(subsetsByFace[2], count5, 2, y, fullHeight); }

            SetupCubeVertexFace2(
                x,
                y,
                z,
                1f,
                1,
                faceTextureSlot3,
                textureSlotCount,
                sideColor,
                ref vertices3.Array[count5]
            );
            SetupCubeVertexFace2(
                x + 1,
                y,
                z,
                1f,
                0,
                faceTextureSlot3,
                textureSlotCount,
                sideColor,
                ref vertices3.Array[count5 + 1]
            );
            SetupCubeVertexFace2(
                x + 1,
                y + 1,
                z,
                height21,
                3,
                faceTextureSlot3,
                textureSlotCount,
                sideColor,
                ref vertices3.Array[count5 + 2]
            );
            SetupCubeVertexFace2(
                x,
                y + 1,
                z,
                height11,
                2,
                faceTextureSlot3,
                textureSlotCount,
                sideColor,
                ref vertices3.Array[count5 + 3]
            );
            var count6 = indices3.Count;
            indices3.Count += 6;
            indices3.Array[count6] = count5;
            indices3.Array[count6 + 1] = count5 + 1;
            indices3.Array[count6 + 2] = count5 + 2;
            indices3.Array[count6 + 3] = count5 + 2;
            indices3.Array[count6 + 4] = count5 + 3;
            indices3.Array[count6 + 5] = count5;
        }

        cellValueFast = topOnly ? value : ReadSide(x - 1, z);
        if (!topOnly && block.ShouldGenerateFace(_subsystemTerrain, 3, value, cellValueFast))
        {
            var vertices4 = subsetsByFace[3].Vertices;
            var indices4 = subsetsByFace[3].Indices;
            var faceTextureSlot4 = block.GetFaceTextureSlot(3, value);
            var count7 = vertices4.Count;
            vertices4.Count += 4;
            if (_recordLighting) { RecordHeightFace(subsetsByFace[3], count7, 3, y, fullHeight); }

            SetupCubeVertexFace3(
                x,
                y,
                z,
                1f,
                0,
                faceTextureSlot4,
                textureSlotCount,
                sideColor,
                ref vertices4.Array[count7]
            );
            SetupCubeVertexFace3(
                x,
                y + 1,
                z,
                height11,
                3,
                faceTextureSlot4,
                textureSlotCount,
                sideColor,
                ref vertices4.Array[count7 + 1]
            );
            SetupCubeVertexFace3(
                x,
                y + 1,
                z + 1,
                height12,
                2,
                faceTextureSlot4,
                textureSlotCount,
                sideColor,
                ref vertices4.Array[count7 + 2]
            );
            SetupCubeVertexFace3(
                x,
                y,
                z + 1,
                1f,
                1,
                faceTextureSlot4,
                textureSlotCount,
                sideColor,
                ref vertices4.Array[count7 + 3]
            );
            var count8 = indices4.Count;
            indices4.Count += 6;
            indices4.Array[count8] = count7;
            indices4.Array[count8 + 1] = count7 + 1;
            indices4.Array[count8 + 2] = count7 + 2;
            indices4.Array[count8 + 3] = count7 + 2;
            indices4.Array[count8 + 4] = count7 + 3;
            indices4.Array[count8 + 5] = count7;
        }

        cellValueFast = ReadGeometryCell(x, y + 1, z);
        if (block.ShouldGenerateFace(_subsystemTerrain, 4, value, cellValueFast) ||
            height11 < 1f ||
            height12 < 1f ||
            height21 < 1f ||
            height22 < 1f)
        {
            var vertices5 = subsetsByFace[4].Vertices;
            var indices5 = subsetsByFace[4].Indices;
            var textureSlot = overrideTopTextureSlot >= 0 ? overrideTopTextureSlot : block.GetFaceTextureSlot(4, value);
            var count9 = vertices5.Count;
            vertices5.Count += 4;
            if (_recordLighting) { RecordHeightFace(subsetsByFace[4], count9, 4, y, fullHeight); }

            SetupCubeVertexFace4(
                x,
                y + 1,
                z,
                height11,
                3,
                textureSlot,
                textureSlotCount,
                topColor11,
                ref vertices5.Array[count9]
            );
            SetupCubeVertexFace4(
                x + 1,
                y + 1,
                z,
                height21,
                2,
                textureSlot,
                textureSlotCount,
                topColor21,
                ref vertices5.Array[count9 + 1]
            );
            SetupCubeVertexFace4(
                x + 1,
                y + 1,
                z + 1,
                height22,
                1,
                textureSlot,
                textureSlotCount,
                topColor22,
                ref vertices5.Array[count9 + 2]
            );
            SetupCubeVertexFace4(
                x,
                y + 1,
                z + 1,
                height12,
                0,
                textureSlot,
                textureSlotCount,
                topColor12,
                ref vertices5.Array[count9 + 3]
            );
            var count10 = indices5.Count;
            indices5.Count += 6;
            indices5.Array[count10] = count9;
            indices5.Array[count10 + 1] = count9 + 1;
            indices5.Array[count10 + 2] = count9 + 2;
            indices5.Array[count10 + 3] = count9 + 2;
            indices5.Array[count10 + 4] = count9 + 3;
            indices5.Array[count10 + 5] = count9;
        }

        cellValueFast = topOnly ? value : ReadGeometryCell(x, y - 1, z);
        if (!topOnly && block.ShouldGenerateFace(_subsystemTerrain, 5, value, cellValueFast))
        {
            var vertices6 = subsetsByFace[5].Vertices;
            var indices6 = subsetsByFace[5].Indices;
            var faceTextureSlot5 = block.GetFaceTextureSlot(5, value);
            var count11 = vertices6.Count;
            vertices6.Count += 4;
            if (_recordLighting) { RecordHeightFace(subsetsByFace[5], count11, 5, y, fullHeight); }

            SetupCubeVertexFace5(
                x,
                y,
                z,
                1f,
                0,
                faceTextureSlot5,
                textureSlotCount,
                sideColor,
                ref vertices6.Array[count11]
            );
            SetupCubeVertexFace5(
                x + 1,
                y,
                z,
                1f,
                1,
                faceTextureSlot5,
                textureSlotCount,
                sideColor,
                ref vertices6.Array[count11 + 1]
            );
            SetupCubeVertexFace5(
                x + 1,
                y,
                z + 1,
                1f,
                2,
                faceTextureSlot5,
                textureSlotCount,
                sideColor,
                ref vertices6.Array[count11 + 2]
            );
            SetupCubeVertexFace5(
                x,
                y,
                z + 1,
                1f,
                3,
                faceTextureSlot5,
                textureSlotCount,
                sideColor,
                ref vertices6.Array[count11 + 3]
            );
            var count12 = indices6.Count;
            indices6.Count += 6;
            indices6.Array[count12] = count11;
            indices6.Array[count12 + 1] = count11 + 2;
            indices6.Array[count12 + 2] = count11 + 1;
            indices6.Array[count12 + 3] = count11 + 2;
            indices6.Array[count12 + 4] = count11;
            indices6.Array[count12 + 5] = count11 + 3;
        }

        int ReadSide(int neighborX, int neighborZ)
        {
            return unknownSide is { } fallback
                ? ReadFluidNeighbor(neighborX, y, neighborZ, fallback)
                : ReadGeometryCell(neighborX, y, neighborZ);
        }
    }

    public void GenerateCubeVertices(
        BlockPresentation block,
        int value,
        int x,
        int y,
        int z,
        int rotationX,
        int rotationY,
        int rotationZ,
        Color color,
        TerrainGeometrySubset[] subsetsByFace
    )
    {
        var chunkAtCell = GetGeometryChunk(x, z);
        var chunkAtCell2 = GetGeometryChunk(x, z + 1);
        var chunkAtCell3 = GetGeometryChunk(x + 1, z);
        var chunkAtCell4 = GetGeometryChunk(x, z - 1);
        var chunkAtCell5 = GetGeometryChunk(x - 1, z);
        var cellValueFast = chunkAtCell2.GetCellValueForGeometry(x & 0xF, y, (z + 1) & 0xF);
        var textureSlotCount = block.GetTextureSlotCount(value);
        if (block.ShouldGenerateFace(_subsystemTerrain, 0, value, cellValueFast))
        {
            var vertices = subsetsByFace[0].Vertices;
            var indices = subsetsByFace[0].Indices;
            var faceTextureSlot = block.GetFaceTextureSlot(0, value);
            var count = vertices.Count;
            vertices.Count += 4;
            if (_recordLighting) { subsetsByFace[0].RecordLighting(count, 4, 1); }

            SetupCubeVertexFace0(
                x,
                y,
                z + 1,
                1f,
                rotationZ,
                faceTextureSlot,
                textureSlotCount,
                color,
                ref vertices.Array[count]
            );
            SetupCubeVertexFace0(
                x + 1,
                y,
                z + 1,
                1f,
                1 + rotationZ,
                faceTextureSlot,
                textureSlotCount,
                color,
                ref vertices.Array[count + 1]
            );
            SetupCubeVertexFace0(
                x + 1,
                y + 1,
                z + 1,
                1f,
                2 + rotationZ,
                faceTextureSlot,
                textureSlotCount,
                color,
                ref vertices.Array[count + 2]
            );
            SetupCubeVertexFace0(
                x,
                y + 1,
                z + 1,
                1f,
                3 + rotationZ,
                faceTextureSlot,
                textureSlotCount,
                color,
                ref vertices.Array[count + 3]
            );
            var count2 = indices.Count;
            indices.Count += 6;
            indices.Array[count2] = count;
            indices.Array[count2 + 1] = count + 2;
            indices.Array[count2 + 2] = count + 1;
            indices.Array[count2 + 3] = count + 2;
            indices.Array[count2 + 4] = count;
            indices.Array[count2 + 5] = count + 3;
        }

        cellValueFast = chunkAtCell3.GetCellValueForGeometry((x + 1) & 0xF, y, z & 0xF);
        if (block.ShouldGenerateFace(_subsystemTerrain, 1, value, cellValueFast))
        {
            var vertices2 = subsetsByFace[1].Vertices;
            var indices2 = subsetsByFace[1].Indices;
            var faceTextureSlot2 = block.GetFaceTextureSlot(1, value);
            var count3 = vertices2.Count;
            vertices2.Count += 4;
            if (_recordLighting) { subsetsByFace[1].RecordLighting(count3, 4, 2); }

            SetupCubeVertexFace1(
                x + 1,
                y,
                z,
                1f,
                1 + rotationX,
                faceTextureSlot2,
                textureSlotCount,
                color,
                ref vertices2.Array[count3]
            );
            SetupCubeVertexFace1(
                x + 1,
                y + 1,
                z,
                1f,
                2 + rotationX,
                faceTextureSlot2,
                textureSlotCount,
                color,
                ref vertices2.Array[count3 + 1]
            );
            SetupCubeVertexFace1(
                x + 1,
                y + 1,
                z + 1,
                1f,
                3 + rotationX,
                faceTextureSlot2,
                textureSlotCount,
                color,
                ref vertices2.Array[count3 + 2]
            );
            SetupCubeVertexFace1(
                x + 1,
                y,
                z + 1,
                1f,
                rotationX,
                faceTextureSlot2,
                textureSlotCount,
                color,
                ref vertices2.Array[count3 + 3]
            );
            var count4 = indices2.Count;
            indices2.Count += 6;
            indices2.Array[count4] = count3;
            indices2.Array[count4 + 1] = count3 + 2;
            indices2.Array[count4 + 2] = count3 + 1;
            indices2.Array[count4 + 3] = count3 + 2;
            indices2.Array[count4 + 4] = count3;
            indices2.Array[count4 + 5] = count3 + 3;
        }

        cellValueFast = chunkAtCell4.GetCellValueForGeometry(x & 0xF, y, (z - 1) & 0xF);
        if (block.ShouldGenerateFace(_subsystemTerrain, 2, value, cellValueFast))
        {
            var vertices3 = subsetsByFace[2].Vertices;
            var indices3 = subsetsByFace[2].Indices;
            var faceTextureSlot3 = block.GetFaceTextureSlot(2, value);
            var count5 = vertices3.Count;
            vertices3.Count += 4;
            if (_recordLighting) { subsetsByFace[2].RecordLighting(count5, 4, 3); }

            SetupCubeVertexFace2(
                x,
                y,
                z,
                1f,
                1 + rotationZ,
                faceTextureSlot3,
                textureSlotCount,
                color,
                ref vertices3.Array[count5]
            );
            SetupCubeVertexFace2(
                x + 1,
                y,
                z,
                1f,
                rotationZ,
                faceTextureSlot3,
                textureSlotCount,
                color,
                ref vertices3.Array[count5 + 1]
            );
            SetupCubeVertexFace2(
                x + 1,
                y + 1,
                z,
                1f,
                3 + rotationZ,
                faceTextureSlot3,
                textureSlotCount,
                color,
                ref vertices3.Array[count5 + 2]
            );
            SetupCubeVertexFace2(
                x,
                y + 1,
                z,
                1f,
                2 + rotationZ,
                faceTextureSlot3,
                textureSlotCount,
                color,
                ref vertices3.Array[count5 + 3]
            );
            var count6 = indices3.Count;
            indices3.Count += 6;
            indices3.Array[count6] = count5;
            indices3.Array[count6 + 1] = count5 + 1;
            indices3.Array[count6 + 2] = count5 + 2;
            indices3.Array[count6 + 3] = count5 + 2;
            indices3.Array[count6 + 4] = count5 + 3;
            indices3.Array[count6 + 5] = count5;
        }

        cellValueFast = chunkAtCell5.GetCellValueForGeometry((x - 1) & 0xF, y, z & 0xF);
        if (block.ShouldGenerateFace(_subsystemTerrain, 3, value, cellValueFast))
        {
            var vertices4 = subsetsByFace[3].Vertices;
            var indices4 = subsetsByFace[3].Indices;
            var faceTextureSlot4 = block.GetFaceTextureSlot(3, value);
            var count7 = vertices4.Count;
            vertices4.Count += 4;
            if (_recordLighting) { subsetsByFace[3].RecordLighting(count7, 4, 4); }

            SetupCubeVertexFace3(
                x,
                y,
                z,
                1f,
                rotationX,
                faceTextureSlot4,
                textureSlotCount,
                color,
                ref vertices4.Array[count7]
            );
            SetupCubeVertexFace3(
                x,
                y + 1,
                z,
                1f,
                3 + rotationX,
                faceTextureSlot4,
                textureSlotCount,
                color,
                ref vertices4.Array[count7 + 1]
            );
            SetupCubeVertexFace3(
                x,
                y + 1,
                z + 1,
                1f,
                2 + rotationX,
                faceTextureSlot4,
                textureSlotCount,
                color,
                ref vertices4.Array[count7 + 2]
            );
            SetupCubeVertexFace3(
                x,
                y,
                z + 1,
                1f,
                1 + rotationX,
                faceTextureSlot4,
                textureSlotCount,
                color,
                ref vertices4.Array[count7 + 3]
            );
            var count8 = indices4.Count;
            indices4.Count += 6;
            indices4.Array[count8] = count7;
            indices4.Array[count8 + 1] = count7 + 1;
            indices4.Array[count8 + 2] = count7 + 2;
            indices4.Array[count8 + 3] = count7 + 2;
            indices4.Array[count8 + 4] = count7 + 3;
            indices4.Array[count8 + 5] = count7;
        }

        cellValueFast = chunkAtCell.GetCellValueForGeometry(x & 0xF, y + 1, z & 0xF);
        if (block.ShouldGenerateFace(_subsystemTerrain, 4, value, cellValueFast))
        {
            var vertices5 = subsetsByFace[4].Vertices;
            var indices5 = subsetsByFace[4].Indices;
            var faceTextureSlot5 = block.GetFaceTextureSlot(4, value);
            var count9 = vertices5.Count;
            vertices5.Count += 4;
            if (_recordLighting) { subsetsByFace[4].RecordLighting(count9, 4, 5); }

            SetupCubeVertexFace4(
                x,
                y + 1,
                z,
                1f,
                3 + rotationY,
                faceTextureSlot5,
                textureSlotCount,
                color,
                ref vertices5.Array[count9]
            );
            SetupCubeVertexFace4(
                x + 1,
                y + 1,
                z,
                1f,
                2 + rotationY,
                faceTextureSlot5,
                textureSlotCount,
                color,
                ref vertices5.Array[count9 + 1]
            );
            SetupCubeVertexFace4(
                x + 1,
                y + 1,
                z + 1,
                1f,
                1 + rotationY,
                faceTextureSlot5,
                textureSlotCount,
                color,
                ref vertices5.Array[count9 + 2]
            );
            SetupCubeVertexFace4(
                x,
                y + 1,
                z + 1,
                1f,
                rotationY,
                faceTextureSlot5,
                textureSlotCount,
                color,
                ref vertices5.Array[count9 + 3]
            );
            var count10 = indices5.Count;
            indices5.Count += 6;
            indices5.Array[count10] = count9;
            indices5.Array[count10 + 1] = count9 + 1;
            indices5.Array[count10 + 2] = count9 + 2;
            indices5.Array[count10 + 3] = count9 + 2;
            indices5.Array[count10 + 4] = count9 + 3;
            indices5.Array[count10 + 5] = count9;
        }

        cellValueFast = chunkAtCell.GetCellValueForGeometry(x & 0xF, y - 1, z & 0xF);
        if (block.ShouldGenerateFace(_subsystemTerrain, 5, value, cellValueFast))
        {
            var vertices6 = subsetsByFace[5].Vertices;
            var indices6 = subsetsByFace[5].Indices;
            var faceTextureSlot6 = block.GetFaceTextureSlot(5, value);
            var count11 = vertices6.Count;
            vertices6.Count += 4;
            if (_recordLighting) { subsetsByFace[5].RecordLighting(count11, 4, 6); }

            SetupCubeVertexFace5(
                x,
                y,
                z,
                1f,
                rotationY,
                faceTextureSlot6,
                textureSlotCount,
                color,
                ref vertices6.Array[count11]
            );
            SetupCubeVertexFace5(
                x + 1,
                y,
                z,
                1f,
                1 + rotationY,
                faceTextureSlot6,
                textureSlotCount,
                color,
                ref vertices6.Array[count11 + 1]
            );
            SetupCubeVertexFace5(
                x + 1,
                y,
                z + 1,
                1f,
                2 + rotationY,
                faceTextureSlot6,
                textureSlotCount,
                color,
                ref vertices6.Array[count11 + 2]
            );
            SetupCubeVertexFace5(
                x,
                y,
                z + 1,
                1f,
                3 + rotationY,
                faceTextureSlot6,
                textureSlotCount,
                color,
                ref vertices6.Array[count11 + 3]
            );
            var count12 = indices6.Count;
            indices6.Count += 6;
            indices6.Array[count12] = count11;
            indices6.Array[count12 + 1] = count11 + 2;
            indices6.Array[count12 + 2] = count11 + 1;
            indices6.Array[count12 + 3] = count11 + 2;
            indices6.Array[count12 + 4] = count11;
            indices6.Array[count12 + 5] = count11 + 3;
        }
    }

    public void GenerateMeshVertices(
        BlockPresentation block,
        int x,
        int y,
        int z,
        BlockMesh blockMesh,
        Color color,
        Matrix? matrix,
        TerrainGeometrySubset subset
    )
    {
        var vertices = subset.Vertices;
        var indices = subset.Indices;
        var count = vertices.Count;
        var response = TerrainLightResponse.FromChannels(ReadLight(x, y, z));
        var sample = _recordLighting ? PackLightCell(x, y, z) : 0;
        vertices.Count += blockMesh.Vertices.Count;
        for (var i = 0; i < blockMesh.Vertices.Count; i++)
        {
            var blockMeshVertex = blockMesh.Vertices.Array[i];
            var vector = blockMeshVertex.Position;
            if (matrix.HasValue)
            {
                vector = Vector3.Transform(blockMeshVertex.Position, matrix.Value);
            }

            var color2 = new Color(
                (byte)(color.R * blockMeshVertex.Color.R / 255),
                (byte)(color.G * blockMeshVertex.Color.G / 255),
                (byte)(color.B * blockMeshVertex.Color.B / 255)
            );

            SetupVertex(
                x + vector.X,
                y + vector.Y,
                z + vector.Z,
                color2,
                blockMeshVertex.TextureCoordinates.X,
                blockMeshVertex.TextureCoordinates.Y,
                ref vertices.Array[count + i]
            );
            vertices.Array[count + i].Lighting =
                blockMeshVertex.IsEmissive ? TerrainLightResponse.FullBright : response;
            if (_recordLighting)
            {
                subset.RecordLighting(count + i, 1, blockMeshVertex.IsEmissive ? _fullBrightSample : sample);
            }
        }

        if (blockMesh.Sides.Count != 0)
        {
            for (var j = 0; j < 6; j++)
            {
                var point = CellFace.FaceToPoint3(j);
                var cellValueFastChunkExists =
                    ReadGeometryCell(x + point.X, y + point.Y, z + point.Z);
                _visibleSides[j] = _subsystemTerrain.Catalog
                    .Blocks[Terrain.ExtractContents(cellValueFastChunkExists)]
                    .IsFaceTransparent(_subsystemTerrain, CellFace.OppositeFace(j), cellValueFastChunkExists);
            }

            for (var k = 0; k < blockMesh.Indices.Count / 3; k++)
            {
                var meshSide = blockMesh.Sides == null ? -1 : blockMesh.Sides.Array[k];
                if (meshSide >= 0 && !_visibleSides[meshSide])
                {
                    continue;
                }

                indices.Add(blockMesh.Indices.Array[3 * k] + count);
                indices.Add(blockMesh.Indices.Array[3 * k + 1] + count);
                indices.Add(blockMesh.Indices.Array[3 * k + 2] + count);
            }
        }
        else
        {
            for (var l = 0; l < blockMesh.Indices.Count; l++)
            {
                indices.Add(blockMesh.Indices.Array[l] + count);
            }
        }
    }

    public void GenerateShadedMeshVertices(
        BlockPresentation block,
        int x,
        int y,
        int z,
        BlockMesh? blockMesh,
        Color color,
        Matrix? matrix,
        int[] facesMap,
        TerrainGeometrySubset subset
    )
    {
        CalculateCornerLights(x, y, z);
        var vertices = subset.Vertices;
        var indices = subset.Indices;
        var count = vertices.Count;
        if (blockMesh is null)
        {
            return;
        }

        vertices.Count += blockMesh.Vertices.Count;
        for (var i = 0; i < blockMesh.Vertices.Count; i++)
        {
            var blockMeshVertex = blockMesh.Vertices.Array[i];
            var vector = blockMeshVertex.Position;
            if (matrix.HasValue)
            {
                vector = Vector3.Transform(vector, matrix.Value);
            }

            var color2 = new Color(
                (byte)(color.R * blockMeshVertex.Color.R / 255),
                (byte)(color.G * blockMeshVertex.Color.G / 255),
                (byte)(color.B * blockMeshVertex.Color.B / 255)
            );
            var response = blockMeshVertex.IsEmissive
                ? TerrainLightResponse.FullBright
                : InterpolateCornerLights(
                    facesMap.Length != 0 ? facesMap[blockMeshVertex.Face] : blockMeshVertex.Face,
                    vector
                );

            SetupVertex(
                x + vector.X,
                y + vector.Y,
                z + vector.Z,
                color2,
                blockMeshVertex.TextureCoordinates.X,
                blockMeshVertex.TextureCoordinates.Y,
                ref vertices.Array[count + i]
            );
            vertices.Array[count + i].Lighting = response;
        }

        if (blockMesh.Sides.Count != 0)
        {
            for (var j = 0; j < 6; j++)
            {
                var point = CellFace.FaceToPoint3(j);
                var cellValueFastChunkExists =
                    ReadGeometryCell(x + point.X, y + point.Y, z + point.Z);
                _visibleSides[j] = _subsystemTerrain.Catalog
                    .Blocks[Terrain.ExtractContents(cellValueFastChunkExists)]
                    .IsFaceTransparent(_subsystemTerrain, CellFace.OppositeFace(j), cellValueFastChunkExists);
            }

            for (var k = 0; k < blockMesh.Indices.Count / 3; k++)
            {
                var meshSide = blockMesh.Sides == null ? -1 : blockMesh.Sides.Array[k];
                if (meshSide < 0 || _visibleSides[facesMap.Length != 0 ? facesMap[meshSide] : meshSide])
                {
                    indices.Add(blockMesh.Indices.Array[3 * k] + count);
                    indices.Add(blockMesh.Indices.Array[3 * k + 1] + count);
                    indices.Add(blockMesh.Indices.Array[3 * k + 2] + count);
                }
            }
        }
        else
        {
            for (var l = 0; l < blockMesh.Indices.Count; l++)
            {
                indices.Add(blockMesh.Indices.Array[l] + count);
            }
        }
    }

    public void GenerateWireVertices(
        int value,
        int x,
        int y,
        int z,
        int mountingFace,
        float centerBoxSize,
        Vector2 centerOffset,
        TerrainGeometrySubset subset
    )
    {
        var color = WireBlock.WireColor;
        var ownerContents = Terrain.ExtractContents(value);
        if (ownerContents == 133)
        {
            var color2 = WireBlock.GetColor(Terrain.ExtractData(value));
            if (color2.HasValue)
            {
                color = PalettePresentation.GetColor(SubsystemPalette, color2);
            }
        }

        var ownerLight = ReadLight(x, y, z);
        var ownerSample = _recordLighting ? PackLightCell(x, y, z) : 0;
        var ownerResponse = TerrainLightResponse.FromChannels(ownerLight);
        var mountCenter = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) - 0.5f * CellFace.FaceToVector3(mountingFace);
        var mountNormal = CellFace.FaceToVector3(mountingFace);
        var uvOrigin = new Vector2(0.9376f, 0.0001f);
        var uvScale = new Vector2(0.03125f, 0.00550781237f);
        var point = CellFace.FaceToPoint3(mountingFace);
        var cellContents = Terrain.ExtractContents(_readGeometryCell(x - point.X, y - point.Y, z - point.Z));
        var jitterOnSurface = cellContents == 2 ||
                              cellContents == 7 ||
                              cellContents == 8 ||
                              cellContents == 6 ||
                              cellContents == 62 ||
                              cellContents == 72;
        var mountUp = CellFace.FaceToVector3(
            SubsystemElectricity.GetConnectorFace(mountingFace, ElectricConnectorDirection.Top)
        );
        var centerDisplacement =
            CellFace.FaceToVector3(
                SubsystemElectricity.GetConnectorFace(mountingFace, ElectricConnectorDirection.Left)
            ) *
            centerOffset.X +
            mountUp * centerOffset.Y;
        var connectedFaceMask = 0;
        _tmpConnectionPaths.Clear();
        _subsystemElectricity.GetAllConnectedNeighbors(x, y, z, mountingFace, _tmpConnectionPaths, _readGeometryCell);
        foreach (var tmpConnectionPath in _tmpConnectionPaths)
        {
            if ((connectedFaceMask & (1 << tmpConnectionPath.ConnectorFace)) == 0)
            {
                var connectorDirection =
                    SubsystemElectricity.GetConnectorDirection(mountingFace, 0, tmpConnectionPath.ConnectorFace);
                if (!(centerOffset == Vector2.Zero) || connectorDirection != ElectricConnectorDirection.In)
                {
                    connectedFaceMask |= 1 << tmpConnectionPath.ConnectorFace;
                    var color3 = color;
                    if (ownerContents != 133)
                    {
                        var cellValue = _readGeometryCell(
                            x + tmpConnectionPath.NeighborOffsetX,
                            y + tmpConnectionPath.NeighborOffsetY,
                            z + tmpConnectionPath.NeighborOffsetZ
                        );
                        if (Terrain.ExtractContents(cellValue) == 133)
                        {
                            var color4 = WireBlock.GetColor(Terrain.ExtractData(cellValue));
                            if (color4.HasValue)
                            {
                                color3 = PalettePresentation.GetColor(SubsystemPalette, color4);
                            }
                        }
                    }

                    var wireDirection = connectorDirection != ElectricConnectorDirection.In
                        ? CellFace.FaceToVector3(tmpConnectionPath.ConnectorFace)
                        : -Vector3.Normalize(centerDisplacement);
                    var wireSide = Vector3.Cross(mountNormal, wireDirection);
                    var startDistance = centerBoxSize >= 0f
                        ? MathUtils.Max(0.03125f, centerBoxSize / 2f)
                        : centerBoxSize / 2f;
                    var endDistance = connectorDirection == ElectricConnectorDirection.In ? 0.03125f : 0.5f;
                    var ridgeEndDistance = connectorDirection == ElectricConnectorDirection.In
                        ? 0f
                        : tmpConnectionPath.ConnectorFace == tmpConnectionPath.NeighborFace
                            ? endDistance + 0.03125f
                            : tmpConnectionPath.ConnectorFace != CellFace.OppositeFace(tmpConnectionPath.NeighborFace)
                                ? endDistance
                                : endDistance - 0.03125f;
                    var startLeft = mountCenter -
                                    wireSide * 0.03125f +
                                    wireDirection * startDistance +
                                    centerDisplacement;
                    var endLeft = mountCenter - wireSide * 0.03125f + wireDirection * endDistance;
                    var endRight = mountCenter + wireSide * 0.03125f + wireDirection * endDistance;
                    var startRight = mountCenter +
                                     wireSide * 0.03125f +
                                     wireDirection * startDistance +
                                     centerDisplacement;
                    var startRidge = mountCenter +
                                     mountNormal * 0.03125f +
                                     wireDirection * (centerBoxSize / 2f) +
                                     centerDisplacement;
                    var endRidge = mountCenter + mountNormal * 0.03125f + wireDirection * ridgeEndDistance;
                    if (jitterOnSurface && centerBoxSize == 0f)
                    {
                        var startJitter = 0.25f * GetRandomWireOffset(0.5f * (startLeft + startRight), mountNormal);
                        startLeft += startJitter;
                        startRight += startJitter;
                        startRidge += startJitter;
                    }

                    var startLeftUv = uvOrigin + uvScale * new Vector2(MathUtils.Max(0.0625f, centerBoxSize), 0f);
                    var endLeftUv = uvOrigin + uvScale * new Vector2(endDistance * 2f, 0f);
                    var endRightUv = uvOrigin + uvScale * new Vector2(endDistance * 2f, 1f);
                    var startRightUv = uvOrigin + uvScale * new Vector2(MathUtils.Max(0.0625f, centerBoxSize), 1f);
                    var startRidgeUv = uvOrigin + uvScale * new Vector2(centerBoxSize, 0.5f);
                    var endRidgeUv = uvOrigin + uvScale * new Vector2(ridgeEndDistance * 2f, 0.5f);
                    var neighborLight = ReadLight(
                        x + tmpConnectionPath.NeighborOffsetX,
                        y + tmpConnectionPath.NeighborOffsetY,
                        z + tmpConnectionPath.NeighborOffsetZ
                    );
                    ReadOnlySpan<TerrainLightChannels> wireSamples = [ownerLight, neighborLight];
                    ReadOnlySpan<float> wireWeights = [0.5f, 0.5f];
                    var middleResponse = TerrainLightResponse.FromSamples(wireSamples, wireWeights);
                    var leftBrightness = LightingManager.CalculateLighting(-wireSide);
                    var rightBrightness = LightingManager.CalculateLighting(wireSide);
                    var ridgeBrightness = LightingManager.CalculateLighting(mountNormal);
                    var startLeftBrightness = leftBrightness;
                    var endLeftBrightness = leftBrightness;
                    var endRightBrightness = rightBrightness;
                    var startRightBrightness = rightBrightness;
                    var startRidgeBrightness = ridgeBrightness;
                    var endRidgeBrightness = ridgeBrightness;
                    var color5 = new Color(
                        (byte)(color3.R * startLeftBrightness),
                        (byte)(color3.G * startLeftBrightness),
                        (byte)(color3.B * startLeftBrightness)
                    );
                    var color6 = new Color(
                        (byte)(color3.R * endLeftBrightness),
                        (byte)(color3.G * endLeftBrightness),
                        (byte)(color3.B * endLeftBrightness)
                    );
                    var color7 = new Color(
                        (byte)(color3.R * endRightBrightness),
                        (byte)(color3.G * endRightBrightness),
                        (byte)(color3.B * endRightBrightness)
                    );
                    var color8 = new Color(
                        (byte)(color3.R * startRightBrightness),
                        (byte)(color3.G * startRightBrightness),
                        (byte)(color3.B * startRightBrightness)
                    );
                    var color9 = new Color(
                        (byte)(color3.R * startRidgeBrightness),
                        (byte)(color3.G * startRidgeBrightness),
                        (byte)(color3.B * startRidgeBrightness)
                    );
                    var color10 = new Color(
                        (byte)(color3.R * endRidgeBrightness),
                        (byte)(color3.G * endRidgeBrightness),
                        (byte)(color3.B * endRidgeBrightness)
                    );
                    var count = subset.Vertices.Count;
                    subset.Vertices.Count += 6;
                    if (_recordLighting)
                    {
                        var sample = PackWireLightCell(
                            ownerSample,
                            tmpConnectionPath.NeighborOffsetX,
                            tmpConnectionPath.NeighborOffsetY,
                            tmpConnectionPath.NeighborOffsetZ
                        );
                        subset.RecordLighting(count, 6, sample);
                    }

                    var array = subset.Vertices.Array;
                    SetupVertex(
                        startLeft.X,
                        startLeft.Y,
                        startLeft.Z,
                        color5,
                        startLeftUv.X,
                        startLeftUv.Y,
                        ref array[count]
                    );
                    SetupVertex(
                        endLeft.X,
                        endLeft.Y,
                        endLeft.Z,
                        color6,
                        endLeftUv.X,
                        endLeftUv.Y,
                        ref array[count + 1]
                    );
                    SetupVertex(
                        endRight.X,
                        endRight.Y,
                        endRight.Z,
                        color7,
                        endRightUv.X,
                        endRightUv.Y,
                        ref array[count + 2]
                    );
                    SetupVertex(
                        startRight.X,
                        startRight.Y,
                        startRight.Z,
                        color8,
                        startRightUv.X,
                        startRightUv.Y,
                        ref array[count + 3]
                    );
                    SetupVertex(
                        startRidge.X,
                        startRidge.Y,
                        startRidge.Z,
                        color9,
                        startRidgeUv.X,
                        startRidgeUv.Y,
                        ref array[count + 4]
                    );
                    SetupVertex(
                        endRidge.X,
                        endRidge.Y,
                        endRidge.Z,
                        color10,
                        endRidgeUv.X,
                        endRidgeUv.Y,
                        ref array[count + 5]
                    );
                    array[count].Lighting = ownerResponse;
                    array[count + 1].Lighting = middleResponse;
                    array[count + 2].Lighting = middleResponse;
                    array[count + 3].Lighting = ownerResponse;
                    array[count + 4].Lighting = ownerResponse;
                    array[count + 5].Lighting = middleResponse;
                    var count2 = subset.Indices.Count;
                    subset.Indices.Count += connectorDirection == ElectricConnectorDirection.In ? 15 : 12;
                    var array2 = subset.Indices.Array;
                    array2[count2] = count;
                    array2[count2 + 1] = count + 5;
                    array2[count2 + 2] = count + 1;
                    array2[count2 + 3] = count + 5;
                    array2[count2 + 4] = count;
                    array2[count2 + 5] = count + 4;
                    array2[count2 + 6] = count + 4;
                    array2[count2 + 7] = count + 2;
                    array2[count2 + 8] = count + 5;
                    array2[count2 + 9] = count + 2;
                    array2[count2 + 10] = count + 4;
                    array2[count2 + 11] = count + 3;
                    if (connectorDirection == ElectricConnectorDirection.In)
                    {
                        array2[count2 + 12] = count + 2;
                        array2[count2 + 13] = count + 1;
                        array2[count2 + 14] = count + 5;
                    }
                }
            }
        }

        if (centerBoxSize != 0f || (connectedFaceMask == 0 && ownerContents != 133))
        {
            return;
        }

        for (var i = 0; i < 6; i++)
        {
            if (i != mountingFace && i != CellFace.OppositeFace(mountingFace) && (connectedFaceMask & (1 << i)) == 0)
            {
                var capDirection = CellFace.FaceToVector3(i);
                var capSide = Vector3.Cross(mountNormal, capDirection);
                var capLeft = mountCenter - capSide * 0.03125f + capDirection * 0.03125f;
                var capRight = mountCenter + capSide * 0.03125f + capDirection * 0.03125f;
                var capRidge = mountCenter + mountNormal * 0.03125f;
                if (jitterOnSurface)
                {
                    var capJitter = 0.25f * GetRandomWireOffset(0.5f * (capLeft + capRight), mountNormal);
                    capLeft += capJitter;
                    capRight += capJitter;
                    capRidge += capJitter;
                }

                var capLeftUv = uvOrigin + uvScale * new Vector2(0.0625f, 0f);
                var capRightUv = uvOrigin + uvScale * new Vector2(0.0625f, 1f);
                var capRidgeUv = uvOrigin + uvScale * new Vector2(0f, 0.5f);
                var capBrightness = LightingManager.CalculateLighting(capDirection);
                var capRidgeBrightness = LightingManager.CalculateLighting(mountNormal);
                var color11 = new Color(
                    (byte)(color.R * capBrightness),
                    (byte)(color.G * capBrightness),
                    (byte)(color.B * capBrightness)
                );
                var color12 = new Color(
                    (byte)(color.R * capRidgeBrightness),
                    (byte)(color.G * capRidgeBrightness),
                    (byte)(color.B * capRidgeBrightness)
                );
                var count3 = subset.Vertices.Count;
                subset.Vertices.Count += 3;
                if (_recordLighting) { subset.RecordLighting(count3, 3, ownerSample); }

                var array3 = subset.Vertices.Array;
                SetupVertex(capLeft.X, capLeft.Y, capLeft.Z, color11, capLeftUv.X, capLeftUv.Y, ref array3[count3]);
                SetupVertex(
                    capRight.X,
                    capRight.Y,
                    capRight.Z,
                    color11,
                    capRightUv.X,
                    capRightUv.Y,
                    ref array3[count3 + 1]
                );
                SetupVertex(
                    capRidge.X,
                    capRidge.Y,
                    capRidge.Z,
                    color12,
                    capRidgeUv.X,
                    capRidgeUv.Y,
                    ref array3[count3 + 2]
                );
                array3[count3].Lighting = ownerResponse;
                array3[count3 + 1].Lighting = ownerResponse;
                array3[count3 + 2].Lighting = ownerResponse;
                var count4 = subset.Indices.Count;
                subset.Indices.Count += 3;
                var array4 = subset.Indices.Array;
                array4[count4] = count3;
                array4[count4 + 1] = count3 + 2;
                array4[count4 + 2] = count3 + 1;
            }
        }
    }

    private void CalculateCubeVertexLight(LitCell cell, ref TerrainLightChannels light, ref int shadow)
    {
        var contents = Terrain.ExtractContents(cell.Value);
        if (contents == 0)
        {
            light = light.Max(cell.Light);
            return;
        }

        light = light.Max(cell.Light);
        shadow += Presentations.Get(contents).GetShadowStrength(cell.Value);
    }

    private static TerrainLightChannels CombineLightAndShadow(TerrainLightChannels light, int shadow)
    {
        return light.Attenuate(MathUtils.Max(shadow / 7, 0));
    }

    private TerrainLightChannels CalculateVertexLightFace0(int x, int y, int z)
    {
        var light = default(TerrainLightChannels);
        var shadow = 0;
        var chunkAtCell = GetGeometryChunk(x - 1, z);
        var westIndex = TerrainChunk.CalculateCellIndex((x - 1) & 0xF, y, z & 0xF);
        var cellValueFast = ReadLitCell(chunkAtCell, westIndex - 1);
        var cellValueFast2 = ReadLitCell(chunkAtCell, westIndex);
        CalculateCubeVertexLight(cellValueFast, ref light, ref shadow);
        CalculateCubeVertexLight(cellValueFast2, ref light, ref shadow);
        var chunkAtCell2 = GetGeometryChunk(x, z);
        var centerIndex = TerrainChunk.CalculateCellIndex(x & 0xF, y, z & 0xF);
        var cellValueFast3 = ReadLitCell(chunkAtCell2, centerIndex - 1);
        var cellValueFast4 = ReadLitCell(chunkAtCell2, centerIndex);
        CalculateCubeVertexLight(cellValueFast3, ref light, ref shadow);
        CalculateCubeVertexLight(cellValueFast4, ref light, ref shadow);
        return CombineLightAndShadow(light, shadow);
    }

    private TerrainLightChannels CalculateVertexLightFace1(int x, int y, int z)
    {
        var light = default(TerrainLightChannels);
        var shadow = 0;
        var chunkAtCell = GetGeometryChunk(x, z - 1);
        var negativeZIndex = TerrainChunk.CalculateCellIndex(x & 0xF, y, (z - 1) & 0xF);
        var cellValueFast = ReadLitCell(chunkAtCell, negativeZIndex - 1);
        var cellValueFast2 = ReadLitCell(chunkAtCell, negativeZIndex);
        CalculateCubeVertexLight(cellValueFast, ref light, ref shadow);
        CalculateCubeVertexLight(cellValueFast2, ref light, ref shadow);
        var chunkAtCell2 = GetGeometryChunk(x, z);
        var centerIndex = TerrainChunk.CalculateCellIndex(x & 0xF, y, z & 0xF);
        var cellValueFast3 = ReadLitCell(chunkAtCell2, centerIndex - 1);
        var cellValueFast4 = ReadLitCell(chunkAtCell2, centerIndex);
        CalculateCubeVertexLight(cellValueFast3, ref light, ref shadow);
        CalculateCubeVertexLight(cellValueFast4, ref light, ref shadow);
        return CombineLightAndShadow(light, shadow);
    }

    private TerrainLightChannels CalculateVertexLightFace2(int x, int y, int z)
    {
        var light = default(TerrainLightChannels);
        var shadow = 0;
        var chunkAtCell = GetGeometryChunk(x - 1, z - 1);
        var negativeXZIndex = TerrainChunk.CalculateCellIndex((x - 1) & 0xF, y, (z - 1) & 0xF);
        var cellValueFast = ReadLitCell(chunkAtCell, negativeXZIndex - 1);
        var cellValueFast2 = ReadLitCell(chunkAtCell, negativeXZIndex);
        CalculateCubeVertexLight(cellValueFast, ref light, ref shadow);
        CalculateCubeVertexLight(cellValueFast2, ref light, ref shadow);
        var chunkAtCell2 = GetGeometryChunk(x, z - 1);
        var negativeZIndex = TerrainChunk.CalculateCellIndex(x & 0xF, y, (z - 1) & 0xF);
        var cellValueFast3 = ReadLitCell(chunkAtCell2, negativeZIndex - 1);
        var cellValueFast4 = ReadLitCell(chunkAtCell2, negativeZIndex);
        CalculateCubeVertexLight(cellValueFast3, ref light, ref shadow);
        CalculateCubeVertexLight(cellValueFast4, ref light, ref shadow);
        return CombineLightAndShadow(light, shadow);
    }

    private TerrainLightChannels CalculateVertexLightFace3(int x, int y, int z)
    {
        var light = default(TerrainLightChannels);
        var shadow = 0;
        var chunkAtCell = GetGeometryChunk(x - 1, z - 1);
        var negativeXZIndex = TerrainChunk.CalculateCellIndex((x - 1) & 0xF, y, (z - 1) & 0xF);
        var cellValueFast = ReadLitCell(chunkAtCell, negativeXZIndex - 1);
        var cellValueFast2 = ReadLitCell(chunkAtCell, negativeXZIndex);
        CalculateCubeVertexLight(cellValueFast, ref light, ref shadow);
        CalculateCubeVertexLight(cellValueFast2, ref light, ref shadow);
        var chunkAtCell2 = GetGeometryChunk(x - 1, z);
        var negativeXIndex = TerrainChunk.CalculateCellIndex((x - 1) & 0xF, y, z & 0xF);
        var cellValueFast3 = ReadLitCell(chunkAtCell2, negativeXIndex - 1);
        var cellValueFast4 = ReadLitCell(chunkAtCell2, negativeXIndex);
        CalculateCubeVertexLight(cellValueFast3, ref light, ref shadow);
        CalculateCubeVertexLight(cellValueFast4, ref light, ref shadow);
        return CombineLightAndShadow(light, shadow);
    }

    private TerrainLightChannels CalculateVertexLightFace4(int x, int y, int z)
    {
        var light = default(TerrainLightChannels);
        var shadow = 0;
        CalculateCubeVertexLight(ReadLitCell(x - 1, y, z - 1), ref light, ref shadow);
        CalculateCubeVertexLight(ReadLitCell(x, y, z - 1), ref light, ref shadow);
        CalculateCubeVertexLight(ReadLitCell(x - 1, y, z), ref light, ref shadow);
        CalculateCubeVertexLight(ReadLitCell(x, y, z), ref light, ref shadow);
        return CombineLightAndShadow(light, shadow);
    }

    private TerrainLightChannels CalculateVertexLightFace5(int x, int y, int z)
    {
        var light = default(TerrainLightChannels);
        var shadow = 0;
        CalculateCubeVertexLight(ReadLitCell(x - 1, y - 1, z - 1), ref light, ref shadow);
        CalculateCubeVertexLight(ReadLitCell(x, y - 1, z - 1), ref light, ref shadow);
        CalculateCubeVertexLight(ReadLitCell(x - 1, y - 1, z), ref light, ref shadow);
        CalculateCubeVertexLight(ReadLitCell(x, y - 1, z), ref light, ref shadow);
        return CombineLightAndShadow(light, shadow);
    }

    private void SetupCubeVertexFace0(
        int x,
        int y,
        int z,
        float height,
        int corner,
        int textureSlot,
        int textureSlotCount,
        Color color,
        ref TerrainVertex vertex
    )
    {
        var y2 = y + height - 1f;
        var light = CalculateVertexLightFace0(x, y, z);
        SetupCornerVertex(x, y2, z, color, light, 0, textureSlot, textureSlotCount, corner, ref vertex);
    }

    private void SetupCubeVertexFace1(
        int x,
        int y,
        int z,
        float height,
        int corner,
        int textureSlot,
        int textureSlotCount,
        Color color,
        ref TerrainVertex vertex
    )
    {
        var y2 = y + height - 1f;
        var light = CalculateVertexLightFace1(x, y, z);
        SetupCornerVertex(x, y2, z, color, light, 1, textureSlot, textureSlotCount, corner, ref vertex);
    }

    private void SetupCubeVertexFace2(
        int x,
        int y,
        int z,
        float height,
        int corner,
        int textureSlot,
        int textureSlotCount,
        Color color,
        ref TerrainVertex vertex
    )
    {
        var y2 = y + height - 1f;
        var light = CalculateVertexLightFace2(x, y, z);
        SetupCornerVertex(x, y2, z, color, light, 2, textureSlot, textureSlotCount, corner, ref vertex);
    }

    private void SetupCubeVertexFace3(
        int x,
        int y,
        int z,
        float height,
        int corner,
        int textureSlot,
        int textureSlotCount,
        Color color,
        ref TerrainVertex vertex
    )
    {
        var y2 = y + height - 1f;
        var light = CalculateVertexLightFace3(x, y, z);
        SetupCornerVertex(x, y2, z, color, light, 3, textureSlot, textureSlotCount, corner, ref vertex);
    }

    private void SetupCubeVertexFace4(
        int x,
        int y,
        int z,
        float height,
        int corner,
        int textureSlot,
        int textureSlotCount,
        Color color,
        ref TerrainVertex vertex
    )
    {
        var y2 = y + height - 1f;
        var light = CalculateVertexLightFace4(x, y, z);
        SetupCornerVertex(x, y2, z, color, light, 4, textureSlot, textureSlotCount, corner, ref vertex);
    }

    private void SetupCubeVertexFace5(
        int x,
        int y,
        int z,
        float height,
        int corner,
        int textureSlot,
        int textureSlotCount,
        Color color,
        ref TerrainVertex vertex
    )
    {
        var y2 = y + height - 1f;
        var light = CalculateVertexLightFace5(x, y, z);
        SetupCornerVertex(x, y2, z, color, light, 5, textureSlot, textureSlotCount, corner, ref vertex);
    }

    private static Vector3 GetRandomWireOffset(Vector3 position, Vector3 normal)
    {
        var hashCode = Vector3.Round(2f * position).GetHashCode();
        var result = default(Vector3);
        result.X = normal.X == 0f ? (float)(double)(MathUtils.Hash((uint)hashCode) % 255u) / 255f - 0.5f : 0f;
        result.Y = normal.Y == 0f ? (float)(double)(MathUtils.Hash((uint)(hashCode + 1)) % 255u) / 255f - 0.5f : 0f;
        result.Z = normal.Z == 0f ? (float)(double)(MathUtils.Hash((uint)(hashCode + 2)) % 255u) / 255f - 0.5f : 0f;
        return result;
    }

    private void CalculateCornerLights(int x, int y, int z)
    {
        if (!(_cornerLightsPosition == new Point3(x, y, z)))
        {
            _cornerLightsPosition = new Point3(x, y, z);
            _modelResponses.Clear();
            _cornerLightsByFace[0].L000 =
                CalculateVertexLightFace0(x, y, z);
            _cornerLightsByFace[0].L001 =
                CalculateVertexLightFace0(x, y, z + 1);
            _cornerLightsByFace[0].L010 =
                CalculateVertexLightFace0(x, y + 1, z);
            _cornerLightsByFace[0].L011 =
                CalculateVertexLightFace0(x, y + 1, z + 1);
            _cornerLightsByFace[0].L100 =
                CalculateVertexLightFace0(x + 1, y, z);
            _cornerLightsByFace[0].L101 =
                CalculateVertexLightFace0(x + 1, y, z + 1);
            _cornerLightsByFace[0].L110 =
                CalculateVertexLightFace0(x + 1, y + 1, z);
            _cornerLightsByFace[0].L111 =
                CalculateVertexLightFace0(x + 1, y + 1, z + 1);
            _cornerLightsByFace[1].L000 =
                CalculateVertexLightFace1(x, y, z);
            _cornerLightsByFace[1].L001 =
                CalculateVertexLightFace1(x, y, z + 1);
            _cornerLightsByFace[1].L010 =
                CalculateVertexLightFace1(x, y + 1, z);
            _cornerLightsByFace[1].L011 =
                CalculateVertexLightFace1(x, y + 1, z + 1);
            _cornerLightsByFace[1].L100 =
                CalculateVertexLightFace1(x + 1, y, z);
            _cornerLightsByFace[1].L101 =
                CalculateVertexLightFace1(x + 1, y, z + 1);
            _cornerLightsByFace[1].L110 =
                CalculateVertexLightFace1(x + 1, y + 1, z);
            _cornerLightsByFace[1].L111 =
                CalculateVertexLightFace1(x + 1, y + 1, z + 1);
            _cornerLightsByFace[2].L000 =
                CalculateVertexLightFace2(x, y, z);
            _cornerLightsByFace[2].L001 =
                CalculateVertexLightFace2(x, y, z + 1);
            _cornerLightsByFace[2].L010 =
                CalculateVertexLightFace2(x, y + 1, z);
            _cornerLightsByFace[2].L011 =
                CalculateVertexLightFace2(x, y + 1, z + 1);
            _cornerLightsByFace[2].L100 =
                CalculateVertexLightFace2(x + 1, y, z);
            _cornerLightsByFace[2].L101 =
                CalculateVertexLightFace2(x + 1, y, z + 1);
            _cornerLightsByFace[2].L110 =
                CalculateVertexLightFace2(x + 1, y + 1, z);
            _cornerLightsByFace[2].L111 =
                CalculateVertexLightFace2(x + 1, y + 1, z + 1);
            _cornerLightsByFace[3].L000 =
                CalculateVertexLightFace3(x, y, z);
            _cornerLightsByFace[3].L001 =
                CalculateVertexLightFace3(x, y, z + 1);
            _cornerLightsByFace[3].L010 =
                CalculateVertexLightFace3(x, y + 1, z);
            _cornerLightsByFace[3].L011 =
                CalculateVertexLightFace3(x, y + 1, z + 1);
            _cornerLightsByFace[3].L100 =
                CalculateVertexLightFace3(x + 1, y, z);
            _cornerLightsByFace[3].L101 =
                CalculateVertexLightFace3(x + 1, y, z + 1);
            _cornerLightsByFace[3].L110 =
                CalculateVertexLightFace3(x + 1, y + 1, z);
            _cornerLightsByFace[3].L111 =
                CalculateVertexLightFace3(x + 1, y + 1, z + 1);
            _cornerLightsByFace[4].L000 =
                CalculateVertexLightFace4(x, y, z);
            _cornerLightsByFace[4].L001 =
                CalculateVertexLightFace4(x, y, z + 1);
            _cornerLightsByFace[4].L010 =
                CalculateVertexLightFace4(x, y + 1, z);
            _cornerLightsByFace[4].L011 =
                CalculateVertexLightFace4(x, y + 1, z + 1);
            _cornerLightsByFace[4].L100 =
                CalculateVertexLightFace4(x + 1, y, z);
            _cornerLightsByFace[4].L101 =
                CalculateVertexLightFace4(x + 1, y, z + 1);
            _cornerLightsByFace[4].L110 =
                CalculateVertexLightFace4(x + 1, y + 1, z);
            _cornerLightsByFace[4].L111 =
                CalculateVertexLightFace4(x + 1, y + 1, z + 1);
            _cornerLightsByFace[5].L000 =
                CalculateVertexLightFace5(x, y, z);
            _cornerLightsByFace[5].L001 =
                CalculateVertexLightFace5(x, y, z + 1);
            _cornerLightsByFace[5].L010 =
                CalculateVertexLightFace5(x, y + 1, z);
            _cornerLightsByFace[5].L011 =
                CalculateVertexLightFace5(x, y + 1, z + 1);
            _cornerLightsByFace[5].L100 =
                CalculateVertexLightFace5(x + 1, y, z);
            _cornerLightsByFace[5].L101 =
                CalculateVertexLightFace5(x + 1, y, z + 1);
            _cornerLightsByFace[5].L110 =
                CalculateVertexLightFace5(x + 1, y + 1, z);
            _cornerLightsByFace[5].L111 =
                CalculateVertexLightFace5(x + 1, y + 1, z + 1);
        }
    }

    private TerrainLightResponse InterpolateCornerLights(int face, Vector3 position)
    {
        var key = (face, position);
        if (_modelResponses.TryGetValue(key, out var cached)) { return cached; }

        var corners = _cornerLightsByFace[face];
        ReadOnlySpan<TerrainLightChannels> samples =
        [
            corners.L000, corners.L001, corners.L010, corners.L011, corners.L100, corners.L101, corners.L110,
            corners.L111
        ];
        var x = position.X;
        var y = position.Y;
        var z = position.Z;
        ReadOnlySpan<float> weights =
        [
            (1 - x) * (1 - y) * (1 - z), (1 - x) * (1 - y) * z,
            (1 - x) * y * (1 - z), (1 - x) * y * z,
            x * (1 - y) * (1 - z), x * (1 - y) * z,
            x * y * (1 - z), x * y * z
        ];
        var response = TerrainLightResponse.FromSamples(samples, weights);
        _modelResponses[key] = response;
        return response;
    }

    public TerrainLightResponse GetLightResponse(int x, int y, int z)
    {
        return TerrainLightResponse.FromChannels(ReadLight(x, y, z));
    }

    private TerrainLightChannels ReadLight(int x, int y, int z)
    {
        x += LightOrigin.X;
        y += LightOrigin.Y;
        z += LightOrigin.Z;
        if (y < 0) { return default; }

        if (y >= 256) { return new TerrainLightChannels(15, 0); }

        var coordinates = Terrain.ToChunk(x, z);
        var section = y >> 4;
        if (_lightReadTarget is { } target)
        {
            var offset = coordinates - _lightingCenter;
            var interior = offset.X != 0 && (x & 15) != (offset.X > 0 ? 0 : 15) ||
                           offset.Y != 0 && (z & 15) != (offset.Y > 0 ? 0 : 15);
            if (_lastLightReadSection != section ||
                _lastLightReadChunk != coordinates ||
                interior && !_lastLightReadInterior)
            {
                target.LightReads.Add(offset, section, interior);
                _lastLightReadSection = section;
                _lastLightReadChunk = coordinates;
                _lastLightReadInterior = interior;
            }
        }

        if (_nearbyLightSnapshots is { } nearby)
        {
            var dx = coordinates.X - _lightingCenter.X + 1;
            var dz = coordinates.Y - _lightingCenter.Y + 1;
            if ((uint)dx < 3 && (uint)dz < 3) { return nearby[dx * 3 + dz]?.Get(x & 15, y, z & 15) ?? default; }
        }

        if (!_lightSnapshots.TryGetValue(coordinates, out var snapshot))
        {
            var world = _subsystemTerrain.Terrain;
            var chunk = world.GetChunkAtCoords(coordinates.X, coordinates.Y);
            if (chunk != null) { world.Lighting.TryGetPublishedSnapshot(chunk, out snapshot); }

            _lightSnapshots[coordinates] = snapshot;
        }

        return snapshot?.Get(x & 15, y, z & 15) ?? default;
    }

    private LitCell ReadLitCell(TerrainChunk chunk, int index)
    {
        return new LitCell(
            chunk.GetCellValueForGeometry(index),
            ReadLight(chunk.Origin.X + ((index >> 8) & 15), index & 255, chunk.Origin.Y + (index >> 12))
        );
    }

    private LitCell ReadLitCell(int x, int y, int z)
    {
        return new LitCell(ReadGeometryCell(x, y, z), ReadLight(x, y, z));
    }

    private struct CornerLights
    {
        public TerrainLightChannels L000;

        public TerrainLightChannels L001;

        public TerrainLightChannels L010;

        public TerrainLightChannels L011;

        public TerrainLightChannels L100;

        public TerrainLightChannels L101;

        public TerrainLightChannels L110;

        public TerrainLightChannels L111;
    }
}
