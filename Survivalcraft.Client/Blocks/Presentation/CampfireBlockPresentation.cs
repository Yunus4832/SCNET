using Engine.Graphics;

using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class CampfireBlockPresentation(CampfireBlock block, BlockPresentationCatalog catalog)
    : BlockPresentation(block, catalog)
{
    protected new CampfireBlock Block => (CampfireBlock)base.Block;

    public readonly BlockMesh[] MeshesByData = new BlockMesh[16];

    public BlockMesh StandaloneMesh = new();

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
            generator.GenerateMeshVertices(
                this,
                x,
                y,
                z,
                MeshesByData[num],
                Color.White,
                null,
                geometry.SubsetOpaque
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
            StandaloneMesh,
            color,
            size,
            ref matrix,
            environmentData
        );
    }

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Campfire");
        var woodNode = model.FindNode("Wood")!;
        var ashesNode = model.FindNode("Ashes")!;
        var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(woodNode);
        var nodeAbsoluteTransform2 = BlockMesh.GetNodeAbsoluteTransform(ashesNode);
        for (var i = 0; i < 16; i++)
        {
            MeshesByData[i] = new BlockMesh();
            if (i == 0)
            {
                MeshesByData[i].AppendModelPrimitive(
                    ashesNode.RequireMesh().Primitives[0],
                    nodeAbsoluteTransform2 * Matrix.CreateScale(3f) * Matrix.CreateTranslation(0.5f, 0f, 0.5f),
                    false,
                    false,
                    false,
                    false,
                    Color.White
                );
            }
            else
            {
                var scale = MathUtils.Lerp(1.5f, 4f, i / 15f);
                var radians = i * (float)Math.PI / 2f;
                MeshesByData[i].AppendModelPrimitive(
                    woodNode.RequireMesh().Primitives[0],
                    nodeAbsoluteTransform *
                    Matrix.CreateScale(scale) *
                    Matrix.CreateRotationY(radians) *
                    Matrix.CreateTranslation(0.5f, 0f, 0.5f),
                    false,
                    false,
                    false,
                    false,
                    Color.White
                );
                MeshesByData[i].AppendModelPrimitive(
                    ashesNode.RequireMesh().Primitives[0],
                    nodeAbsoluteTransform2 *
                    Matrix.CreateScale(scale) *
                    Matrix.CreateRotationY(radians) *
                    Matrix.CreateTranslation(0.5f, 0f, 0.5f),
                    false,
                    false,
                    false,
                    false,
                    Color.White
                );
            }
        }

        StandaloneMesh.AppendModelPrimitive(
            woodNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * Matrix.CreateScale(3f) * Matrix.CreateTranslation(0f, 0f, 0f),
            false,
            false,
            true,
            false,
            Color.White
        );
        StandaloneMesh.AppendModelPrimitive(
            ashesNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform2 * Matrix.CreateScale(3f) * Matrix.CreateTranslation(0f, 0f, 0f),
            false,
            false,
            true,
            false,
            Color.White
        );
        base.Initialize();
    }
}
