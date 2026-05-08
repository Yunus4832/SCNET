using Engine.Graphics;

namespace Game.Blocks;

public class TargetBlock : MountedElectricElementBlock
{
    public const int Index = 199;

    public BoundingBox[][] BoundingBoxes = new BoundingBox[][]
    {
        [new BoundingBox(new Vector3(0f, 0f, 0f), new Vector3(1f, 1f, 0.0625f))],
        [new BoundingBox(new Vector3(0f, 0f, 0f), new Vector3(0.0625f, 1f, 1f))],
        [new BoundingBox(new Vector3(0f, 0f, 0.9375f), new Vector3(1f, 1f, 1f))],
        [new BoundingBox(new Vector3(0.9375f, 0f, 0f), new Vector3(1f, 1f, 1f))]
    };

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        var mountingFace = GetMountingFace(Terrain.ExtractData(value));
        return mountingFace is >= 0 and < 4
            ? BoundingBoxes[mountingFace]
            : base.GetCustomCollisionBoxes(terrain, value);
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
        result.Value = Terrain.MakeBlockValue(199, 0, SetMountingFace(0, raycastResult.CellFace.Face));

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
        var mountingFace = GetMountingFace(data);
        var s = LightingManager.LightIntensityByLightValueAndFace[num + 16 * mountingFace];
        var color = Color.White * s;
        switch (mountingFace)
        {
            case 2:
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
            case 3:
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
            case 0:
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
            case 1:
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

    public static int GetMountingFace(int data)
    {
        return data & 3;
    }

    public static int SetMountingFace(int data, int face)
    {
        return (data & -4) | (face & 3);
    }

    public override int GetFace(int value)
    {
        return GetMountingFace(Terrain.ExtractData(value));
    }

    public override ElectricElement CreateElectricElement(
        SubsystemElectricity subsystemElectricity,
        int value,
        int x,
        int y,
        int z
    )
    {
        return new TargetElectricElement(subsystemElectricity, new CellFace(x, y, z, GetFace(value)));
    }

    public override ElectricConnectorType? GetConnectorType(
        SubsystemTerrain terrain,
        int value,
        int face,
        int connectorFace,
        int x,
        int y,
        int z
    )
    {
        var face2 = GetFace(value);
        if (face == face2 && SubsystemElectricity.GetConnectorDirection(face2, 0, connectorFace).HasValue)
        {
            return ElectricConnectorType.Output;
        }

        return null;
    }
}
