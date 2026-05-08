using Engine.Graphics;

namespace Game.Blocks;

public class JackOLanternBlock : Block
{
    public const int Index = 132;

    public BlockMesh[] BlockMeshesByData = new BlockMesh[4];

    public BoundingBox[] CollisionBoxes = new BoundingBox[1];

    public BlockMesh StandaloneBlockMesh = new();

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Pumpkins");
        var jackOLanternMesh = model.FindMesh("JackOLantern")!;
        var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
            jackOLanternMesh.ParentBone ??
            throw new InvalidOperationException("Required JackOLanternMesh.ParentBone is null")
        );
        for (var i = 0; i < 4; i++)
        {
            var radians = i * (float)Math.PI / 2f;
            var blockMesh = new BlockMesh();
            blockMesh.AppendModelMeshPart(jackOLanternMesh.MeshParts[0],
                boneAbsoluteTransform * Matrix.CreateRotationY(radians) * Matrix.CreateTranslation(0.5f, 0f, 0.5f),
                false, false, false, false, new Color(232, 232, 232));
            blockMesh.AppendModelMeshPart(jackOLanternMesh.MeshParts[0],
                boneAbsoluteTransform * Matrix.CreateRotationY(radians) * Matrix.CreateTranslation(0.5f, 0f, 0.5f),
                true, true, false, false, Color.White);
            BlockMeshesByData[i] = blockMesh;
        }

        StandaloneBlockMesh.AppendModelMeshPart(jackOLanternMesh.MeshParts[0],
            boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.23f, 0f), false, false, false, false,
            new Color(232, 232, 232));
        StandaloneBlockMesh.AppendModelMeshPart(jackOLanternMesh.MeshParts[0],
            boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.23f, 0f), true, true, false, false, Color.White);
        CollisionBoxes[0] = BlockMeshesByData[0].CalculateBoundingBox();
        base.Initialize();
    }

    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        ComponentMiner componentMiner,
        int value,
        TerrainRaycastResult raycastResult
    )
    {
        var forward = Matrix.CreateFromQuaternion(componentMiner.ComponentCreature.ComponentCreatureModel.EyeRotation)
            .Forward;
        var num = Vector3.Dot(forward, Vector3.UnitZ);
        var num2 = Vector3.Dot(forward, Vector3.UnitX);
        var num3 = Vector3.Dot(forward, -Vector3.UnitZ);
        var num4 = Vector3.Dot(forward, -Vector3.UnitX);
        var data = 0;
        if (num.CloseTo(MathUtils.Max(num, num2, num3, num4)))
        {
            data = 0;
        }
        else if (num2.CloseTo(MathUtils.Max(num, num2, num3, num4)))
        {
            data = 1;
        }
        else if (num3.CloseTo(MathUtils.Max(num, num2, num3, num4)))
        {
            data = 2;
        }
        else if (num4.CloseTo(MathUtils.Max(num, num2, num3, num4)))
        {
            data = 3;
        }

        BlockPlacementData result = default;
        result.Value = Terrain.ReplaceData(Terrain.ReplaceContents(0, 132), data);
        result.CellFace = raycastResult.CellFace;
        return result;
    }

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        return CollisionBoxes;
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
        if (num < BlockMeshesByData.Length)
        {
            generator.GenerateMeshVertices(this, x, y, z, BlockMeshesByData[num], Color.White, null,
                geometry.SubsetAlphaTest);
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
        BlocksManager.DrawMeshBlock(
            primitivesRenderer,
            StandaloneBlockMesh,
            color,
            2f * size,
            ref matrix,
            environmentData
        );
    }

    public override bool IsMovableByPiston(int value, int pistonFace, int y, out bool isEnd)
    {
        isEnd = false;
        return false;
    }
}
