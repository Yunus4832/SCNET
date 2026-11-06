using Engine.Graphics;

using Game.Modding.Blocks;

using static Game.Blocks.TrapdoorBlock;

namespace Game.Blocks.Presentation;

public abstract class TrapdoorBlockPresentation(TrapdoorBlock block, BlockPresentationCatalog catalog, string modelName)
    : BlockPresentation(block, catalog)
{
    protected new TrapdoorBlock Block => (TrapdoorBlock)base.Block;

    public readonly BlockMesh[] BlockMeshesByData = new BlockMesh[16];

    public readonly BlockMesh StandaloneBlockMesh = new();

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

    public string ModelName = modelName;

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>(ModelName);
        var trapdoorNode = model.FindNode("Trapdoor")!;
        var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(trapdoorNode);
        for (var i = 0; i < 16; i++)
        {
            var rotation = GetRotation(i);
            var open = GetOpen(i);
            var upsideDown = GetUpsideDown(i);
            BlockMeshesByData[i] = new BlockMesh();
            var identity = Matrix.Identity;
            identity *= Matrix.CreateTranslation(0f, -0.0625f, 0.4375f) *
                        Matrix.CreateRotationX(open ? -(float)Math.PI / 2f : 0f) *
                        Matrix.CreateTranslation(0f, 0.0625f, -0.4375f);
            identity *= Matrix.CreateRotationZ(upsideDown ? (float)Math.PI : 0f);
            identity *= Matrix.CreateRotationY(rotation * (float)Math.PI / 2f);
            identity *= Matrix.CreateTranslation(new Vector3(0.5f, upsideDown ? 1 : 0, 0.5f));
            BlockMeshesByData[i].AppendModelPrimitive(
                trapdoorNode.RequireMesh().Primitives[0],
                nodeAbsoluteTransform * identity,
                false,
                false,
                false,
                false,
                Color.White
            );
            BlockMeshesByData[i].GenerateSidesData();
        }

        StandaloneBlockMesh.AppendModelPrimitive(
            trapdoorNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * Matrix.CreateTranslation(0f, 0f, 0f),
            false,
            false,
            false,
            false,
            Color.White
        );
        base.Initialize();
    }
}
