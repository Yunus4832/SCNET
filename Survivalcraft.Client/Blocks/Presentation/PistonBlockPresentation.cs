using Engine.Graphics;

using Game.Modding.Blocks;

using static Game.Blocks.PistonBlock;

namespace Game.Blocks.Presentation;

public class PistonBlockPresentation(PistonBlock block, BlockPresentationCatalog catalog)
    : BlockPresentation(block, catalog)
{
    protected new PistonBlock Block => (PistonBlock)base.Block;

    public readonly BlockMesh[] BlockMeshesByData = new BlockMesh[48];

    public readonly BlockMesh[] StandaloneBlockMeshes = new BlockMesh[4];

    public override void GenerateTerrainVertices(
        BlockGeometryGenerator generator,
        TerrainGeometry geometry,
        int value,
        int x,
        int y,
        int z
    )
    {
        var num = Terrain.ExtractData(value) & 0x3F;
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
        var mode = (int)GetMode(Terrain.ExtractData(value));
        if (mode < StandaloneBlockMeshes.Length && StandaloneBlockMeshes[mode] != null)
        {
            BlocksManager.DrawMeshBlock(
                primitivesRenderer,
                StandaloneBlockMeshes[mode],
                color,
                1f * size,
                ref matrix,
                environmentData
            );
        }
    }

    public override string GetDisplayName(SubsystemTerrain? subsystemTerrain, int value)
    {
        return GetMode(Terrain.ExtractData(value)) switch
        {
            PistonMode.Pulling => "粘性活塞",
            PistonMode.StrictPulling => "严格粘性活塞",
            _ => "活塞"
        };
    }

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Pistons");
        for (var pistonMode = PistonMode.Pushing; pistonMode <= PistonMode.StrictPulling; pistonMode++)
        {
            for (var i = 0; i < 2; i++)
            {
                var name = i == 0 ? "PistonRetracted" : "PistonExtended";
                var modelNode = model.FindNode(name)!;
                var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(modelNode);
                for (var j = 0; j < 6; j++)
                {
                    var num = SetFace(SetIsExtended(SetMode(0, pistonMode), i != 0), j);
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
                    if (i == 0)
                    {
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

            var nodeAbsoluteTransform2 =
                BlockMesh.GetNodeAbsoluteTransform(model.FindNode("PistonRetracted")!);
            StandaloneBlockMeshes[(int)pistonMode] = new BlockMesh();
            StandaloneBlockMeshes[(int)pistonMode].AppendModelPrimitive(
                model.FindNode("PistonRetracted")!.RequireMesh().Primitives[0],
                nodeAbsoluteTransform2 * Matrix.CreateTranslation(0f, -0.5f, 0f),
                false,
                false,
                false,
                false,
                Color.White
            );
            switch (pistonMode)
            {
                case PistonMode.Pulling:
                    StandaloneBlockMeshes[(int)pistonMode]
                        .TransformTextureCoordinates(Matrix.CreateTranslation(0f, 0.0625f, 0f), 4);
                    break;
                case PistonMode.StrictPulling:
                    StandaloneBlockMeshes[(int)pistonMode]
                        .TransformTextureCoordinates(Matrix.CreateTranslation(0f, 0.125f, 0f), 4);
                    break;
            }
        }
    }
}
