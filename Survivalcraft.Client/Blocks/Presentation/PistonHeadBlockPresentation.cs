using Engine.Graphics;

using Game.Modding.Blocks;

using static Game.Blocks.PistonHeadBlock;

namespace Game.Blocks.Presentation;

public class PistonHeadBlockPresentation(PistonHeadBlock block, BlockPresentationCatalog catalog)
    : BlockPresentation(block, catalog)
{
    protected new PistonHeadBlock Block => (PistonHeadBlock)base.Block;

    public readonly BlockMesh[] BlockMeshesByData = new BlockMesh[48];

    public override int GetShadowStrength(int value)
    {
        if (!GetIsShaft(Terrain.ExtractData(value)))
        {
            return base.GetShadowStrength(value);
        }

        return 0;
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
        if (num < BlockMeshesByData.Length && BlockMeshesByData[num] != null)
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
    }

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Pistons");
        for (var i = 0; i < 2; i++)
        {
            var name = i == 0 ? "PistonHead" : "PistonShaft";
            var modelNode = model.FindNode(name)!;
            var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(modelNode);
            for (var pistonMode = PistonMode.Pushing; pistonMode <= PistonMode.StrictPulling; pistonMode++)
            {
                for (var j = 0; j < 6; j++)
                {
                    var num = SetFace(SetMode(SetIsShaft(0, i != 0), pistonMode), j);
                    var m = j < 4
                        ? Matrix.CreateTranslation(0f, -0.5f, 0f) *
                          Matrix.CreateRotationY(j * (float)Math.PI / 2f + (float)Math.PI) *
                          Matrix.CreateTranslation(0.5f, 0.5f, 0.5f)
                        : j != 4
                            ? Matrix.CreateTranslation(0f, -0.5f, 0f) *
                              Matrix.CreateRotationX(-(float)Math.PI / 2f) *
                              Matrix.CreateTranslation(0.5f, 0.5f, 0.5f)
                            : Matrix.CreateTranslation(0f, -0.5f, 0f) *
                              Matrix.CreateRotationX((float)Math.PI / 2f) *
                              Matrix.CreateTranslation(0.5f, 0.5f, 0.5f);
                    BlockMeshesByData[num] = new BlockMesh();
                    BlockMeshesByData[num].AppendModelPrimitive(
                        modelNode.RequireMesh().Primitives[0],
                        nodeAbsoluteTransform * m,
                        false,
                        false,
                        false,
                        false,
                        Color.White
                    );
                    switch (pistonMode)
                    {
                        case PistonMode.Pulling:
                            BlockMeshesByData[num]
                                .TransformTextureCoordinates(Matrix.CreateTranslation(0f, 0.0625f, 0f), 1 << j);
                            break;
                        case PistonMode.StrictPulling:
                            BlockMeshesByData[num]
                                .TransformTextureCoordinates(Matrix.CreateTranslation(0f, 0.125f, 0f), 1 << j);
                            break;
                    }
                }
            }
        }
    }
}
