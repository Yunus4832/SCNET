using Engine.Graphics;

namespace Game.Blocks;

public class MagnetBlock : Block
{
    public const int Index = 167;

    public readonly BoundingBox[][] CollisionBoxesByData = new BoundingBox[2][];

    public readonly BlockMesh[] MeshesByData = new BlockMesh[2];

    public readonly BlockMesh StandaloneMesh = new();

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Magnet");
        var magnetMesh = model.FindMesh("Magnet")!;
        var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
            magnetMesh.ParentBone ??
            throw new InvalidOperationException("Required MagnetMesh.ParentBone is null")
        );
        for (var i = 0; i < 2; i++)
        {
            MeshesByData[i] = new BlockMesh();
            MeshesByData[i].AppendModelMeshPart(magnetMesh.MeshParts[0],
                boneAbsoluteTransform * Matrix.CreateRotationY((float)Math.PI / 2f * i) *
                Matrix.CreateTranslation(0.5f, 0f, 0.5f), false, false, true, false, Color.White);
            CollisionBoxesByData[i] = [MeshesByData[i].CalculateBoundingBox()];
        }

        StandaloneMesh.AppendModelMeshPart(magnetMesh.MeshParts[0],
            boneAbsoluteTransform * Matrix.CreateScale(1.5f) * Matrix.CreateTranslation(0f, -0.25f, 0f), false, false,
            true, false, Color.White);
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
        if (num < CollisionBoxesByData.Length)
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
        BlockPlacementData result;
        if (componentMiner.Project.FindSubsystem<SubsystemMagnetBlockBehavior>(true)!.MagnetsCount < 8)
        {
            var forward = Matrix
                .CreateFromQuaternion(componentMiner.ComponentCreature.ComponentCreatureModel.EyeRotation).Forward;
            var data = !(MathUtils.Abs(forward.X) > MathUtils.Abs(forward.Z)) ? 1 : 0;
            result = default;
            result.CellFace = raycastResult.CellFace;
            result.Value = Terrain.ReplaceData(value, data);
            return result;
        }

        componentMiner.ComponentPlayer?.ComponentGui.DisplaySmallMessage("Too many magnets", Color.White, true, false);
        result = default;
        return result;
    }
}
