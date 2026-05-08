using Engine.Graphics;

namespace Game.Blocks;

public class TorchBlock : Block
{
    public const int Index = 31;

    public BlockMesh[] BlockMeshesByVariant = new BlockMesh[5];

    public BoundingBox[][] CollisionBoxes = new BoundingBox[5][];

    public BlockMesh StandaloneBlockMesh = new();

    public override bool FurnitureBuilt { get; set; } = true;

    public override void Initialize()
    {
        for (var i = 0; i < BlockMeshesByVariant.Length; i++)
        {
            BlockMeshesByVariant[i] = new BlockMesh();
        }

        var model = ContentManager.Get<Model>("Models/Torch");
        var torchMesh = model.FindMesh("Torch")!;
        var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
            torchMesh.ParentBone ??
            throw new InvalidOperationException("Required TorchMesh.ParentBone is null")
        );
        var flameMesh = model.FindMesh("Flame")!;
        var boneAbsoluteTransform2 = BlockMesh.GetBoneAbsoluteTransform(
            flameMesh.ParentBone ??
            throw new InvalidOperationException("Required FlameMesh.ParentBone is null")
        );
        var m = Matrix.CreateRotationX(0.6f) * Matrix.CreateRotationY(0f) *
                Matrix.CreateTranslation(0.5f, 0.15f, -0.05f);
        BlockMeshesByVariant[0].AppendModelMeshPart(torchMesh.MeshParts[0], boneAbsoluteTransform * m,
            false, false, false, false, Color.White);
        BlockMeshesByVariant[0].AppendModelMeshPart(flameMesh.MeshParts[0], boneAbsoluteTransform2 * m,
            true, false, false, false, Color.White);
        m = Matrix.CreateRotationX(0.6f) * Matrix.CreateRotationY((float)Math.PI / 2f) *
            Matrix.CreateTranslation(-0.05f, 0.15f, 0.5f);
        BlockMeshesByVariant[1].AppendModelMeshPart(torchMesh.MeshParts[0], boneAbsoluteTransform * m,
            false, false, false, false, Color.White);
        BlockMeshesByVariant[1].AppendModelMeshPart(flameMesh.MeshParts[0], boneAbsoluteTransform2 * m,
            true, false, false, false, Color.White);
        m = Matrix.CreateRotationX(0.6f) * Matrix.CreateRotationY((float)Math.PI) *
            Matrix.CreateTranslation(0.5f, 0.15f, 1.05f);
        BlockMeshesByVariant[2].AppendModelMeshPart(torchMesh.MeshParts[0], boneAbsoluteTransform * m,
            false, false, false, false, Color.White);
        BlockMeshesByVariant[2].AppendModelMeshPart(flameMesh.MeshParts[0], boneAbsoluteTransform2 * m,
            true, false, false, false, Color.White);
        m = Matrix.CreateRotationX(0.6f) * Matrix.CreateRotationY(4.712389f) *
            Matrix.CreateTranslation(1.05f, 0.15f, 0.5f);
        BlockMeshesByVariant[3].AppendModelMeshPart(torchMesh.MeshParts[0], boneAbsoluteTransform * m,
            false, false, false, false, Color.White);
        BlockMeshesByVariant[3].AppendModelMeshPart(flameMesh.MeshParts[0], boneAbsoluteTransform2 * m,
            true, false, false, false, Color.White);
        m = Matrix.CreateTranslation(0.5f, 0f, 0.5f);
        BlockMeshesByVariant[4].AppendModelMeshPart(torchMesh.MeshParts[0], boneAbsoluteTransform * m,
            false, false, false, false, Color.White);
        BlockMeshesByVariant[4].AppendModelMeshPart(flameMesh.MeshParts[0], boneAbsoluteTransform2 * m,
            true, false, false, false, Color.White);
        StandaloneBlockMesh.AppendModelMeshPart(torchMesh.MeshParts[0],
            boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.25f, 0f), false, false, false, false, Color.White);
        for (var j = 0; j < 5; j++)
        {
            CollisionBoxes[j] = new BoundingBox[1]
            {
                BlockMeshesByVariant[j].CalculateBoundingBox()
            };
        }

        base.Initialize();
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
        if (num < BlockMeshesByVariant.Length)
        {
            generator.GenerateMeshVertices(this, x, y, z, BlockMeshesByVariant[num], Color.White, null,
                geometry.SubsetOpaque);
        }
    }

    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        ComponentMiner componentMiner,
        int value,
        TerrainRaycastResult raycastResult
    )
    {
        var value2 = 0;
        if (raycastResult.CellFace.Face == 0)
        {
            value2 = Terrain.ReplaceData(Terrain.ReplaceContents(0, 31), 0);
        }

        if (raycastResult.CellFace.Face == 1)
        {
            value2 = Terrain.ReplaceData(Terrain.ReplaceContents(0, 31), 1);
        }

        if (raycastResult.CellFace.Face == 2)
        {
            value2 = Terrain.ReplaceData(Terrain.ReplaceContents(0, 31), 2);
        }

        if (raycastResult.CellFace.Face == 3)
        {
            value2 = Terrain.ReplaceData(Terrain.ReplaceContents(0, 31), 3);
        }

        if (raycastResult.CellFace.Face == 4)
        {
            value2 = Terrain.ReplaceData(Terrain.ReplaceContents(0, 31), 4);
        }

        BlockPlacementData result = default;
        result.Value = value2;
        result.CellFace = raycastResult.CellFace;
        return result;
    }

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        var num = Terrain.ExtractData(value);
        return num < CollisionBoxes.Length ? CollisionBoxes[num] : [];
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
}
