using Engine.Graphics;

namespace Game.Blocks;

public abstract class RotatableMountedElectricElementBlock(
    string modelName,
    string meshName,
    float centerBoxSize
) : MountedElectricElementBlock
{
    public BlockMesh[] BlockMeshes = new BlockMesh[24];

    public float CenterBoxSize = centerBoxSize;

    public BoundingBox[][] CollisionBoxes = new BoundingBox[24][];

    public string MeshName = meshName;

    public string ModelName = modelName;

    public BlockMesh StandaloneBlockMesh = new();

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>(ModelName);
        var modelMesh = model.FindMesh(MeshName)!;
        var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
            modelMesh.ParentBone ??
            throw new InvalidOperationException("Required ModelMesh.ParentBone is null")
        );
        for (var i = 0; i < 6; i++)
        {
            float radians;
            bool flag;
            switch (i)
            {
                case < 4:
                    radians = i * (float)Math.PI / 2f;
                    flag = false;
                    break;
                case 4:
                    radians = -(float)Math.PI / 2f;
                    flag = true;
                    break;
                default:
                    radians = (float)Math.PI / 2f;
                    flag = true;
                    break;
            }

            for (var j = 0; j < 4; j++)
            {
                var radians2 = -j * (float)Math.PI / 2f;
                var num = (i << 2) + j;
                var m = Matrix.CreateRotationX((float)Math.PI / 2f) * Matrix.CreateRotationZ(radians2) *
                        Matrix.CreateTranslation(0f, 0f, -0.5f) *
                        (flag ? Matrix.CreateRotationX(radians) : Matrix.CreateRotationY(radians)) *
                        Matrix.CreateTranslation(0.5f, 0.5f, 0.5f);
                BlockMeshes[num] = new BlockMesh();
                BlockMeshes[num].AppendModelMeshPart(modelMesh.MeshParts[0],
                    boneAbsoluteTransform * m, false, false, false, false, Color.White);
                CollisionBoxes[num] = [BlockMeshes[num].CalculateBoundingBox()];
            }
        }

        var m2 = Matrix.CreateRotationY(-(float)Math.PI / 2f) * Matrix.CreateRotationZ((float)Math.PI / 2f);
        StandaloneBlockMesh.AppendModelMeshPart(modelMesh.MeshParts[0], boneAbsoluteTransform * m2,
            false, false, false, false, Color.White);
    }

    public override int GetFace(int value)
    {
        return (Terrain.ExtractData(value) >> 2) & 7;
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

    public override void GenerateTerrainVertices(
        BlockGeometryGenerator generator,
        TerrainGeometry geometry,
        int value,
        int x,
        int y,
        int z
    )
    {
        var num = Terrain.ExtractData(value) & 0x1F;
        generator.GenerateMeshVertices(this, x, y, z, BlockMeshes[num], Color.White, null, geometry.SubsetOpaque);
        generator.GenerateWireVertices(value, x, y, z, GetFace(value), CenterBoxSize, Vector2.Zero,
            geometry.SubsetOpaque);
    }

    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        ComponentMiner componentMiner,
        int value,
        TerrainRaycastResult raycastResult
    )
    {
        var rotation = 0;
        if (raycastResult.CellFace.Face >= 4)
        {
            var forward = Matrix
                .CreateFromQuaternion(componentMiner.ComponentCreature.ComponentCreatureModel.EyeRotation).Forward;
            var num = Vector3.Dot(forward, Vector3.UnitZ);
            var num2 = Vector3.Dot(forward, Vector3.UnitX);
            var num3 = Vector3.Dot(forward, -Vector3.UnitZ);
            var num4 = Vector3.Dot(forward, -Vector3.UnitX);
            if (num.CloseTo(MathUtils.Max(num, num2, num3, num4)))
            {
                rotation = 2;
            }
            else if (num2.CloseTo(MathUtils.Max(num, num2, num3, num4)))
            {
                rotation = 1;
            }
            else if (num3.CloseTo(MathUtils.Max(num, num2, num3, num4)))
            {
                rotation = 0;
            }
            else if (num4.CloseTo(MathUtils.Max(num, num2, num3, num4)))
            {
                rotation = 3;
            }
        }

        var num5 = Terrain.ExtractData(value);
        num5 &= -29;
        num5 |= raycastResult.CellFace.Face << 2;
        BlockPlacementData result = default;
        result.Value = Terrain.MakeBlockValue(BlockIndex, 0, SetRotation(num5, rotation));
        result.CellFace = raycastResult.CellFace;
        return result;
    }

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        var num = Terrain.ExtractData(value) & 0x1F;
        return CollisionBoxes[num];
    }

    public static int GetRotation(int data)
    {
        return data & 3;
    }

    public static int SetRotation(int data, int rotation)
    {
        return (data & -4) | (rotation & 3);
    }
}
