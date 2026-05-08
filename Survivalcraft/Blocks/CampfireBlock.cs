using Engine.Graphics;

namespace Game.Blocks;

public class CampfireBlock : Block
{
    public const int Index = 209;

    public readonly BoundingBox[][] CollisionBoxesByData = new BoundingBox[16][];

    public readonly BlockMesh[] MeshesByData = new BlockMesh[16];

    public BlockMesh StandaloneMesh = new();

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Campfire");
        var woodMesh = model.FindMesh("Wood")!;
        var ashesMesh = model.FindMesh("Ashes")!;
        var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
            woodMesh.ParentBone ??
            throw new InvalidOperationException("Required WoodMesh.ParentBone is null"));
        var boneAbsoluteTransform2 = BlockMesh.GetBoneAbsoluteTransform(
            ashesMesh.ParentBone ??
            throw new InvalidOperationException("Required AshesMesh.ParentBone is null")
        );
        for (var i = 0; i < 16; i++)
        {
            MeshesByData[i] = new BlockMesh();
            if (i == 0)
            {
                MeshesByData[i].AppendModelMeshPart(ashesMesh.MeshParts[0],
                    boneAbsoluteTransform2 * Matrix.CreateScale(3f) * Matrix.CreateTranslation(0.5f, 0f, 0.5f), false,
                    false, false, false, Color.White);
            }
            else
            {
                var scale = MathUtils.Lerp(1.5f, 4f, i / 15f);
                var radians = i * (float)Math.PI / 2f;
                MeshesByData[i].AppendModelMeshPart(woodMesh.MeshParts[0],
                    boneAbsoluteTransform * Matrix.CreateScale(scale) * Matrix.CreateRotationY(radians) *
                    Matrix.CreateTranslation(0.5f, 0f, 0.5f), false, false, false, false, Color.White);
                MeshesByData[i].AppendModelMeshPart(ashesMesh.MeshParts[0],
                    boneAbsoluteTransform2 * Matrix.CreateScale(scale) * Matrix.CreateRotationY(radians) *
                    Matrix.CreateTranslation(0.5f, 0f, 0.5f), false, false, false, false, Color.White);
            }

            var boundingBox = MeshesByData[i].CalculateBoundingBox();
            // 修改 Min 和 Max 的分量
            var min = boundingBox.Min;
            min.X = MathUtils.Saturate(min.X);
            min.Y = MathUtils.Saturate(min.Y);
            min.Z = MathUtils.Saturate(min.Z);
            boundingBox.Min = min;

            var max = boundingBox.Max;
            max.X = MathUtils.Saturate(max.X);
            max.Y = MathUtils.Saturate(max.Y);
            max.Z = MathUtils.Saturate(max.Z);
            boundingBox.Max = max;
            CollisionBoxesByData[i] =
            [
                boundingBox
            ];
        }

        StandaloneMesh.AppendModelMeshPart(woodMesh.MeshParts[0],
            boneAbsoluteTransform * Matrix.CreateScale(3f) * Matrix.CreateTranslation(0f, 0f, 0f), false, false, true,
            false, Color.White);
        StandaloneMesh.AppendModelMeshPart(ashesMesh.MeshParts[0],
            boneAbsoluteTransform2 * Matrix.CreateScale(3f) * Matrix.CreateTranslation(0f, 0f, 0f), false, false, true,
            false, Color.White);
        base.Initialize();
    }

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        var num = Terrain.ExtractData(value);
        return num < CollisionBoxesByData.Length ? CollisionBoxesByData[num] : [];
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
        if (num < MeshesByData.Length)
        {
            generator.GenerateMeshVertices(this, x, y, z, MeshesByData[num], Color.White, null,
                geometry.SubsetOpaque);
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
            StandaloneMesh,
            color,
            size,
            ref matrix,
            environmentData
        );
    }

    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        ComponentMiner componentMiner,
        int value,
        TerrainRaycastResult raycastResult
    )
    {
        BlockPlacementData result = default;
        result.CellFace = raycastResult.CellFace;
        result.Value = Terrain.MakeBlockValue(209, 0, 3);
        return result;
    }

    public override bool ShouldAvoid(int value)
    {
        return Terrain.ExtractData(value) > 0;
    }

    public override int GetEmittedLightAmount(int value)
    {
        var num = Terrain.ExtractData(value);
        return num > 0 ? MathUtils.Min(8 + num / 2, 15) : 0;
    }

    public override float GetHeat(int value)
    {
        return Terrain.ExtractData(value) <= 0 ? 0f : base.GetHeat(value);
    }
}
