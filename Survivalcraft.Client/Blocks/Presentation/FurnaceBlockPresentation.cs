using Engine.Graphics;

using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class FurnaceBlockPresentation(FurnaceBlock block, BlockPresentationCatalog catalog)
    : BlockPresentation(block, catalog)
{
    protected new FurnaceBlock Block => (FurnaceBlock)base.Block;

    public BlockMesh[] BlockMeshesByData = new BlockMesh[4];

    public BlockMesh StandaloneBlockMesh = new();

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
        BlocksManager.DrawMeshBlock(
            primitivesRenderer,
            StandaloneBlockMesh,
            color,
            size,
            ref matrix,
            environmentData
        );
    }

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Furnace");
        var furnaceNode = model.FindNode("Furnace")!;
        var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(furnaceNode);
        for (var i = 0; i < 4; i++)
        {
            BlockMeshesByData[i] = new BlockMesh();
            var identity = Matrix.Identity;
            identity *= Matrix.CreateRotationY(i * (float)Math.PI / 2f) * Matrix.CreateTranslation(0.5f, 0f, 0.5f);
            BlockMeshesByData[i].AppendModelPrimitive(
                furnaceNode.RequireMesh().Primitives[0],
                nodeAbsoluteTransform * identity,
                false,
                false,
                false,
                false,
                Color.White
            );
        }

        StandaloneBlockMesh.AppendModelPrimitive(
            furnaceNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * Matrix.CreateTranslation(0f, -0.5f, 0f),
            false,
            false,
            false,
            false,
            Color.White
        );
        base.Initialize();
    }
}
