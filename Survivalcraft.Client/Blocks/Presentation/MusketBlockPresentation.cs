using Engine.Graphics;

using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class MusketBlockPresentation(MusketBlock block, BlockPresentationCatalog catalog)
    : BlockPresentation(block, catalog)
{
    protected new MusketBlock Block => (MusketBlock)base.Block;

    public BlockMesh StandaloneBlockMeshLoaded = new();

    public BlockMesh StandaloneBlockMeshUnloaded = new();

    public override void GenerateTerrainVertices(
        BlockGeometryGenerator generator,
        TerrainGeometry geometry,
        int value,
        int x,
        int y,
        int z
    )
    {
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
        if (MusketBlock.GetHammerState(Terrain.ExtractData(value)))
        {
            BlocksManager.DrawMeshBlock(
                primitivesRenderer,
                StandaloneBlockMeshLoaded,
                color,
                2f * size,
                ref matrix,
                environmentData
            );
        }
        else
        {
            BlocksManager.DrawMeshBlock(
                primitivesRenderer,
                StandaloneBlockMeshUnloaded,
                color,
                2f * size,
                ref matrix,
                environmentData
            );
        }
    }

    public override bool IsSwapAnimationNeeded(int oldValue, int newValue)
    {
        if (Terrain.ExtractContents(oldValue) != Block.BlockIndex)
        {
            return true;
        }

        var data = Terrain.ExtractData(oldValue);
        return MusketBlock.SetHammerState(Terrain.ExtractData(newValue), true) !=
               MusketBlock.SetHammerState(data, true);
    }

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Musket");
        var musketNode = model.FindNode("Musket")!;
        var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(musketNode);
        var hammerNode = model.FindNode("Hammer")!;
        var nodeAbsoluteTransform2 = BlockMesh.GetNodeAbsoluteTransform(hammerNode);
        StandaloneBlockMeshUnloaded = new BlockMesh();
        StandaloneBlockMeshUnloaded.AppendModelPrimitive(
            musketNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform,
            false,
            false,
            false,
            false,
            Color.White
        );
        StandaloneBlockMeshUnloaded.AppendModelPrimitive(
            hammerNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform2,
            false,
            false,
            false,
            false,
            Color.White
        );
        StandaloneBlockMeshLoaded = new BlockMesh();
        StandaloneBlockMeshLoaded.AppendModelPrimitive(
            musketNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform,
            false,
            false,
            false,
            false,
            Color.White
        );
        StandaloneBlockMeshLoaded.AppendModelPrimitive(
            hammerNode.RequireMesh().Primitives[0],
            Matrix.CreateRotationX(0.7f) * nodeAbsoluteTransform2,
            false,
            false,
            false,
            false,
            Color.White
        );
        base.Initialize();
    }
}
