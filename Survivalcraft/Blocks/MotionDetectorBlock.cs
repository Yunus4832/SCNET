using Engine.Graphics;

namespace Game.Blocks;

public class MotionDetectorBlock : MountedElectricElementBlock
{
    public const int Index = 179;

    public BlockMesh[] BlockMeshesByData = new BlockMesh[6];

    public BoundingBox[][] CollisionBoxesByData = new BoundingBox[6][];

    public BlockMesh StandaloneBlockMesh = new();

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/MotionDetector");
        var motionDetectorMesh = model.FindMesh("MotionDetector")!;
        var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
            motionDetectorMesh.ParentBone ??
            throw new InvalidOperationException("Required MotionDetectorMesh.ParentBone is null")
        );
        for (var i = 0; i < 6; i++)
        {
            var num = i;
            var m = i >= 4
                ? i != 4
                    ? Matrix.CreateRotationX((float)Math.PI) * Matrix.CreateTranslation(0.5f, 1f, 0.5f)
                    : Matrix.CreateTranslation(0.5f, 0f, 0.5f)
                : Matrix.CreateRotationX((float)Math.PI / 2f) * Matrix.CreateTranslation(0f, 0f, -0.5f) *
                  Matrix.CreateRotationY(i * (float)Math.PI / 2f) * Matrix.CreateTranslation(0.5f, 0.5f, 0.5f);
            BlockMeshesByData[num] = new BlockMesh();
            BlockMeshesByData[num].AppendModelMeshPart(motionDetectorMesh.MeshParts[0],
                boneAbsoluteTransform * m, false, false, false, false, Color.White);
            CollisionBoxesByData[num] = [BlockMeshesByData[num].CalculateBoundingBox()];
        }

        var m2 = Matrix.CreateRotationY(-(float)Math.PI / 2f) * Matrix.CreateRotationZ((float)Math.PI / 2f);
        StandaloneBlockMesh.AppendModelMeshPart(motionDetectorMesh.MeshParts[0],
            boneAbsoluteTransform * m2, false, false, false, false, Color.White);
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
        BlockPlacementData result = default;
        result.Value = Terrain.ReplaceData(value, raycastResult.CellFace.Face);
        result.CellFace = raycastResult.CellFace;
        return result;
    }

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        var num = Terrain.ExtractData(value);
        return num >= CollisionBoxesByData.Length ? [] : CollisionBoxesByData[num];
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
        var num = Terrain.ExtractData(value);
        if (num >= BlockMeshesByData.Length)
        {
            return;
        }

        generator.GenerateMeshVertices(this, x, y, z, BlockMeshesByData[num], Color.White, null,
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
        return new MotionDetectorElectricElement(subsystemElectricity, new CellFace(x, y, z, GetFace(value)));
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
