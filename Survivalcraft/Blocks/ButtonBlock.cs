using Engine.Graphics;

namespace Game.Blocks;

public class ButtonBlock : MountedElectricElementBlock
{
    public const int Index = 142;

    public BlockMesh[] BlockMeshesByFace = new BlockMesh[6];

    public BoundingBox[][] CollisionBoxesByFace = new BoundingBox[6][];

    public BlockMesh StandaloneBlockMesh = new();

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Button");
        var buttonMesh = model.FindMesh("Button")!;
        var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
            buttonMesh.ParentBone ??
            throw new InvalidOperationException("Required ButtonMesh.ParentBone is null")
        );
        for (var i = 0; i < 6; i++)
        {
            var matrix = i >= 4
                ? i != 4
                    ? Matrix.CreateRotationX((float)Math.PI) * Matrix.CreateTranslation(0.5f, 1f, 0.5f)
                    : Matrix.CreateTranslation(0.5f, 0f, 0.5f)
                : Matrix.CreateRotationX((float)Math.PI / 2f) * Matrix.CreateTranslation(0f, 0f, -0.5f) *
                  Matrix.CreateRotationY(i * (float)Math.PI / 2f) * Matrix.CreateTranslation(0.5f, 0.5f, 0.5f);
            BlockMeshesByFace[i] = new BlockMesh();
            BlockMeshesByFace[i].AppendModelMeshPart(buttonMesh.MeshParts[0],
                boneAbsoluteTransform * matrix, false, false, false, false, Color.White);
            CollisionBoxesByFace[i] = [BlockMeshesByFace[i].CalculateBoundingBox()];
        }

        var matrix2 = Matrix.CreateRotationY(-(float)Math.PI / 2f) * Matrix.CreateRotationZ((float)Math.PI / 2f);
        StandaloneBlockMesh.AppendModelMeshPart(buttonMesh.MeshParts[0],
            boneAbsoluteTransform * matrix2, false, false, false, false, Color.White);
    }

    public static int GetVoltageLevel(int data)
    {
        return 15 - ((data >> 3) & 0xF);
    }

    public static int SetVoltageLevel(int data, int voltageLevel)
    {
        return (data & -121) | ((15 - (voltageLevel & 0xF)) << 3);
    }

    public override int GetFace(int value)
    {
        return Terrain.ExtractData(value) & 7;
    }

    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        ComponentMiner componentMiner,
        int value,
        TerrainRaycastResult raycastResult
    )
    {
        var result = default(BlockPlacementData);
        result.Value = Terrain.ReplaceData(value, raycastResult.CellFace.Face);
        var data = SetVoltageLevel(Terrain.ExtractData(result.Value), GetVoltageLevel(Terrain.ExtractData(value)));
        result.Value = Terrain.ReplaceData(value, data);
        result.CellFace = raycastResult.CellFace;
        return result;
    }

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        var face = GetFace(value);
        return face >= CollisionBoxesByFace.Length ? [] : CollisionBoxesByFace[face];
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
        var face = GetFace(value);
        if (face >= BlockMeshesByFace.Length)
        {
            return;
        }

        generator.GenerateMeshVertices(this, x, y, z, BlockMeshesByFace[face], Color.White, null,
            geometry.SubsetOpaque);
        generator.GenerateWireVertices(value, x, y, z, GetFace(value), 0.25f, Vector2.Zero, geometry.SubsetOpaque);
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
        BlocksManager.DrawMeshBlock(
            primitivesRenderer,
            StandaloneBlockMesh,
            color,
            2f * size,
            ref matrix,
            environmentData
        );
    }

    public override ElectricElement CreateElectricElement(
        SubsystemElectricity subsystemElectricity,
        int value,
        int x,
        int y,
        int z
    )
    {
        return new ButtonElectricElement(subsystemElectricity, new CellFace(x, y, z, GetFace(value)), value);
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
