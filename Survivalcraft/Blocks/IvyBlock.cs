using Engine.Graphics;

namespace Game.Blocks;

public class IvyBlock : Block
{
    public const int Index = 197;

    public readonly BoundingBox[][] BoundingBoxes = new BoundingBox[][]
    {
        [new BoundingBox(new Vector3(0f, 0f, 0.9375f), new Vector3(1f, 1f, 1f))],
        [new BoundingBox(new Vector3(0.9375f, 0f, 0f), new Vector3(1f, 1f, 1f))],
        [new BoundingBox(new Vector3(0f, 0f, 0f), new Vector3(1f, 1f, 0.0625f))],
        [new BoundingBox(new Vector3(0f, 0f, 0f), new Vector3(0.0625f, 1f, 1f))]
    };

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        var face = GetFace(Terrain.ExtractData(value));
        return face is >= 0 and < 4 ? BoundingBoxes[face] : base.GetCustomCollisionBoxes(terrain, value);
    }

    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        ComponentMiner componentMiner,
        int value,
        TerrainRaycastResult raycastResult
    )
    {
        BlockPlacementData result = default;
        if (raycastResult.CellFace.Face >= 4)
        {
            return result;
        }

        result.CellFace = raycastResult.CellFace;
        result.Value = Terrain.MakeBlockValue(197, 0, SetFace(0, CellFace.OppositeFace(raycastResult.CellFace.Face)));

        return result;
    }

    public override void GenerateTerrainVertices(
        BlockGeometryGenerator generator,
        TerrainGeometry geometry,
        int value,
        int x,
        int y,
        int z
    )
    {
        var subsetAlphaTest = geometry.SubsetAlphaTest;
        var vertices = subsetAlphaTest.Vertices;
        var indices = subsetAlphaTest.Indices;
        var count = vertices.Count;
        var data = Terrain.ExtractData(value);
        var num = Terrain.ExtractLight(value);
        var face = GetFace(data);
        var s = LightingManager.LightIntensityByLightValueAndFace[num + 16 * CellFace.OppositeFace(face)];
        var color = BlockColorsMap.IvyColorsMap.Lookup(generator.Terrain, x, y, z) * s;
        color.A = byte.MaxValue;
        switch (face)
        {
            case 0:
                vertices.Count += 4;
                BlockGeometryGenerator.SetupLitCornerVertex(x, y, z + 1, color, TextureSlot, 0,
                    ref vertices.Array[count]);
                BlockGeometryGenerator.SetupLitCornerVertex(x + 1, y, z + 1, color, TextureSlot, 1,
                    ref vertices.Array[count + 1]);
                BlockGeometryGenerator.SetupLitCornerVertex(x + 1, y + 1, z + 1, color, TextureSlot, 2,
                    ref vertices.Array[count + 2]);
                BlockGeometryGenerator.SetupLitCornerVertex(x, y + 1, z + 1, color, TextureSlot, 3,
                    ref vertices.Array[count + 3]);
                indices.Add(count);
                indices.Add(count + 1);
                indices.Add(count + 2);
                indices.Add(count + 2);
                indices.Add(count + 1);
                indices.Add(count);
                indices.Add(count + 2);
                indices.Add(count + 3);
                indices.Add(count);
                indices.Add(count);
                indices.Add(count + 3);
                indices.Add(count + 2);
                break;
            case 1:
                vertices.Count += 4;
                BlockGeometryGenerator.SetupLitCornerVertex(x + 1, y, z, color, TextureSlot, 0,
                    ref vertices.Array[count]);
                BlockGeometryGenerator.SetupLitCornerVertex(x + 1, y + 1, z, color, TextureSlot, 3,
                    ref vertices.Array[count + 1]);
                BlockGeometryGenerator.SetupLitCornerVertex(x + 1, y + 1, z + 1, color, TextureSlot, 2,
                    ref vertices.Array[count + 2]);
                BlockGeometryGenerator.SetupLitCornerVertex(x + 1, y, z + 1, color, TextureSlot, 1,
                    ref vertices.Array[count + 3]);
                indices.Add(count);
                indices.Add(count + 1);
                indices.Add(count + 2);
                indices.Add(count + 2);
                indices.Add(count + 1);
                indices.Add(count);
                indices.Add(count + 2);
                indices.Add(count + 3);
                indices.Add(count);
                indices.Add(count);
                indices.Add(count + 3);
                indices.Add(count + 2);
                break;
            case 2:
                vertices.Count += 4;
                BlockGeometryGenerator.SetupLitCornerVertex(x, y, z, color, TextureSlot, 0,
                    ref vertices.Array[count]);
                BlockGeometryGenerator.SetupLitCornerVertex(x + 1, y, z, color, TextureSlot, 1,
                    ref vertices.Array[count + 1]);
                BlockGeometryGenerator.SetupLitCornerVertex(x + 1, y + 1, z, color, TextureSlot, 2,
                    ref vertices.Array[count + 2]);
                BlockGeometryGenerator.SetupLitCornerVertex(x, y + 1, z, color, TextureSlot, 3,
                    ref vertices.Array[count + 3]);
                indices.Add(count);
                indices.Add(count + 2);
                indices.Add(count + 1);
                indices.Add(count + 1);
                indices.Add(count + 2);
                indices.Add(count);
                indices.Add(count + 2);
                indices.Add(count);
                indices.Add(count + 3);
                indices.Add(count + 3);
                indices.Add(count);
                indices.Add(count + 2);
                break;
            case 3:
                vertices.Count += 4;
                BlockGeometryGenerator.SetupLitCornerVertex(x, y, z, color, TextureSlot, 0,
                    ref vertices.Array[count]);
                BlockGeometryGenerator.SetupLitCornerVertex(x, y + 1, z, color, TextureSlot, 3,
                    ref vertices.Array[count + 1]);
                BlockGeometryGenerator.SetupLitCornerVertex(x, y + 1, z + 1, color, TextureSlot, 2,
                    ref vertices.Array[count + 2]);
                BlockGeometryGenerator.SetupLitCornerVertex(x, y, z + 1, color, TextureSlot, 1,
                    ref vertices.Array[count + 3]);
                indices.Add(count);
                indices.Add(count + 2);
                indices.Add(count + 1);
                indices.Add(count + 1);
                indices.Add(count + 2);
                indices.Add(count);
                indices.Add(count + 2);
                indices.Add(count);
                indices.Add(count + 3);
                indices.Add(count + 3);
                indices.Add(count);
                indices.Add(count + 2);
                break;
        }
    }

    public override void DrawBlock(
        PrimitivesRenderer3D primitivesRenderer,
        int value,
        Color color,
        float size,
        ref Matrix matrix,
        DrawBlockEnvironmentData environmentData
    )
    {
        color *= BlockColorsMap.IvyColorsMap.Lookup(environmentData.Temperature, environmentData.Humidity);
        BlocksManager.DrawFlatOrImageExtrusionBlock(
            primitivesRenderer,
            value,
            size,
            ref matrix,
            null,
            color,
            false,
            environmentData
        );
    }

    public override BlockDebrisParticleSystem CreateDebrisParticleSystem(
        SubsystemTerrain subsystemTerrain,
        Vector3 position,
        int value,
        float strength
    )
    {
        var color = BlockColorsMap.IvyColorsMap.Lookup(subsystemTerrain.Terrain, Terrain.ToCell(position.X),
            Terrain.ToCell(position.Y), Terrain.ToCell(position.Z));
        return new BlockDebrisParticleSystem(subsystemTerrain, position, strength, DestructionDebrisScale, color,
            TextureSlot);
    }

    public static int GetFace(int data)
    {
        return data & 3;
    }

    public static int SetFace(int data, int face)
    {
        return (data & -4) | (face & 3);
    }

    public static bool IsGrowthStopCell(int x, int y, int z)
    {
        return MathUtils.Hash((uint)(x + y * 451 + z * 77437)) % 5u == 0;
    }
}
