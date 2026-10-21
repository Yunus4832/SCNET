using Engine.Graphics;

namespace Game.Blocks;

public class TorchBlock : Block
{
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
        var torchNode = model.FindNode("Torch")!;
        var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(torchNode);
        var flameNode = model.FindNode("Flame")!;
        var nodeAbsoluteTransform2 = BlockMesh.GetNodeAbsoluteTransform(flameNode);
        var m = Matrix.CreateRotationX(0.6f) *
                Matrix.CreateRotationY(0f) *
                Matrix.CreateTranslation(0.5f, 0.15f, -0.05f);
        BlockMeshesByVariant[0].AppendModelPrimitive(
            torchNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * m,
            false,
            false,
            false,
            false,
            Color.White
        );
        BlockMeshesByVariant[0].AppendModelPrimitive(
            flameNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform2 * m,
            true,
            false,
            false,
            false,
            Color.White
        );
        m = Matrix.CreateRotationX(0.6f) *
            Matrix.CreateRotationY((float)Math.PI / 2f) *
            Matrix.CreateTranslation(-0.05f, 0.15f, 0.5f);
        BlockMeshesByVariant[1].AppendModelPrimitive(
            torchNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * m,
            false,
            false,
            false,
            false,
            Color.White
        );
        BlockMeshesByVariant[1].AppendModelPrimitive(
            flameNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform2 * m,
            true,
            false,
            false,
            false,
            Color.White
        );
        m = Matrix.CreateRotationX(0.6f) *
            Matrix.CreateRotationY((float)Math.PI) *
            Matrix.CreateTranslation(0.5f, 0.15f, 1.05f);
        BlockMeshesByVariant[2].AppendModelPrimitive(
            torchNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * m,
            false,
            false,
            false,
            false,
            Color.White
        );
        BlockMeshesByVariant[2].AppendModelPrimitive(
            flameNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform2 * m,
            true,
            false,
            false,
            false,
            Color.White
        );
        m = Matrix.CreateRotationX(0.6f) *
            Matrix.CreateRotationY(4.712389f) *
            Matrix.CreateTranslation(1.05f, 0.15f, 0.5f);
        BlockMeshesByVariant[3].AppendModelPrimitive(
            torchNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * m,
            false,
            false,
            false,
            false,
            Color.White
        );
        BlockMeshesByVariant[3].AppendModelPrimitive(
            flameNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform2 * m,
            true,
            false,
            false,
            false,
            Color.White
        );
        m = Matrix.CreateTranslation(0.5f, 0f, 0.5f);
        BlockMeshesByVariant[4].AppendModelPrimitive(
            torchNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * m,
            false,
            false,
            false,
            false,
            Color.White
        );
        BlockMeshesByVariant[4].AppendModelPrimitive(
            flameNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform2 * m,
            true,
            false,
            false,
            false,
            Color.White
        );
        StandaloneBlockMesh.AppendModelPrimitive(
            torchNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * Matrix.CreateTranslation(0f, -0.25f, 0f),
            false,
            false,
            false,
            false,
            Color.White
        );
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
            generator.GenerateMeshVertices(
                this,
                x,
                y,
                z,
                BlockMeshesByVariant[num],
                Color.White,
                null,
                geometry.SubsetOpaque
            );
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
            value2 = Terrain.ReplaceData(Terrain.ReplaceContents(0, BlockIndex), 0);
        }

        if (raycastResult.CellFace.Face == 1)
        {
            value2 = Terrain.ReplaceData(Terrain.ReplaceContents(0, BlockIndex), 1);
        }

        if (raycastResult.CellFace.Face == 2)
        {
            value2 = Terrain.ReplaceData(Terrain.ReplaceContents(0, BlockIndex), 2);
        }

        if (raycastResult.CellFace.Face == 3)
        {
            value2 = Terrain.ReplaceData(Terrain.ReplaceContents(0, BlockIndex), 3);
        }

        if (raycastResult.CellFace.Face == 4)
        {
            value2 = Terrain.ReplaceData(Terrain.ReplaceContents(0, BlockIndex), 4);
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
