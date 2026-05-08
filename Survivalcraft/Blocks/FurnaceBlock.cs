using Engine.Graphics;

namespace Game.Blocks;

public class FurnaceBlock : Block
{
    public const int Index = 64;

    public BlockMesh[] BlockMeshesByData = new BlockMesh[4];

    public BlockMesh StandaloneBlockMesh = new();

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Furnace");
        var furnaceMesh = model.FindMesh("Furnace")!;
        var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
            furnaceMesh.ParentBone ??
            throw new InvalidOperationException("Required FurnaceMesh.ParentBone is null")
        );
        for (var i = 0; i < 4; i++)
        {
            BlockMeshesByData[i] = new BlockMesh();
            var identity = Matrix.Identity;
            identity *= Matrix.CreateRotationY(i * (float)Math.PI / 2f) * Matrix.CreateTranslation(0.5f, 0f, 0.5f);
            BlockMeshesByData[i].AppendModelMeshPart(furnaceMesh.MeshParts[0],
                boneAbsoluteTransform * identity, false, false, false, false, Color.White);
        }

        StandaloneBlockMesh.AppendModelMeshPart(furnaceMesh.MeshParts[0],
            boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.5f, 0f), false, false, false, false, Color.White);
        base.Initialize();
    }

    public override bool IsFaceTransparent(SubsystemTerrain subsystemTerrain, int face, int value)
    {
        return false;
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
            generator.GenerateShadedMeshVertices(
                this,
                x,
                y,
                z,
                BlockMeshesByData[num],
                Color.White,
                null,
                [],
                geometry.SubsetAlphaTest
            );
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
        BlocksManager.DrawMeshBlock(primitivesRenderer, StandaloneBlockMesh, color, size, ref matrix,
            environmentData);
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
            data = 2;
        }
        else if (num2.CloseTo(MathUtils.Max(num, num2, num3, num4)))
        {
            data = 3;
        }
        else if (num3.CloseTo(MathUtils.Max(num, num2, num3, num4)))
        {
            data = 0;
        }
        else if (num4.CloseTo(MathUtils.Max(num, num2, num3, num4)))
        {
            data = 1;
        }

        BlockPlacementData result = default;
        result.Value = Terrain.ReplaceData(Terrain.ReplaceContents(0, 64), data);
        result.CellFace = raycastResult.CellFace;
        return result;
    }
}
