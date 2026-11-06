using Engine.Graphics;

using Game.Modding.Blocks;

using static Game.Blocks.SpikedPlankBlock;

namespace Game.Blocks.Presentation;

public class SpikedPlankBlockPresentation(SpikedPlankBlock block, BlockPresentationCatalog catalog)
    : MountedElectricElementBlockPresentation(block, catalog)
{
    protected new SpikedPlankBlock Block => (SpikedPlankBlock)base.Block;

    public BlockMesh[] BlockMeshesByData = new BlockMesh[12];

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
        if (num >= BlockMeshesByData.Length || BlockMeshesByData[num] == null)
        {
            return;
        }

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
        generator.GenerateWireVertices(value, x, y, z, Block.GetFace(value), 1f, Vector2.Zero, geometry.SubsetOpaque);
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
            1f * size,
            ref matrix,
            environmentData
        );
    }

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/SpikedPlanks");
        var array = new[]
        {
            "SpikedPlankRetracted",
            "SpikedPlank"
        };
        for (var i = 0; i < 2; i++)
        {
            var name = array[i];
            var modelNode = model.FindNode(name)!;
            var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(modelNode);
            for (var j = 0; j < 6; j++)
            {
                var num = SetMountingFace(SetSpikesState(0, i != 0), j);
                var m = j >= 4
                    ? j != 4
                        ? Matrix.CreateRotationX((float)Math.PI) * Matrix.CreateTranslation(0.5f, 1f, 0.5f)
                        : Matrix.CreateTranslation(0.5f, 0f, 0.5f)
                    : Matrix.CreateRotationX((float)Math.PI / 2f) *
                      Matrix.CreateTranslation(0f, 0f, -0.5f) *
                      Matrix.CreateRotationY(j * (float)Math.PI / 2f) *
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
            }

            var identity = Matrix.Identity;
            StandaloneBlockMesh.AppendModelPrimitive(
                modelNode.RequireMesh().Primitives[0],
                nodeAbsoluteTransform * identity,
                false,
                false,
                false,
                false,
                Color.White
            );
        }
    }
}
