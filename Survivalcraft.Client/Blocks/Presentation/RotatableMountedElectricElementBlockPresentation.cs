using Engine.Graphics;

using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public abstract class RotatableMountedElectricElementBlockPresentation(
    RotatableMountedElectricElementBlock block,
    BlockPresentationCatalog catalog,
    string modelName
) : MountedElectricElementBlockPresentation(block, catalog)
{
    protected new RotatableMountedElectricElementBlock Block => (RotatableMountedElectricElementBlock)base.Block;

    public BlockMesh[] BlockMeshes = new BlockMesh[24];

    public BlockMesh StandaloneBlockMesh = new();

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

    public override void GenerateTerrainVertices(
        BlockGeometryGenerator generator,
        TerrainGeometry geometry,
        int value,
        int x,
        int y,
        int z
    )
    {
        var num = Terrain.ExtractData(value) & 0x1F;
        generator.GenerateMeshVertices(this, x, y, z, BlockMeshes[num], Color.White, null, geometry.SubsetOpaque);
        generator.GenerateWireVertices(
            value,
            x,
            y,
            z,
            Block.GetFace(value),
            Block.CenterBoxSize,
            Vector2.Zero,
            geometry.SubsetOpaque
        );
    }


    public string ModelName = modelName;

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>(ModelName);
        var modelNode = model.FindNode(Block.CollisionNodeName)!;
        var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(modelNode);
        for (var i = 0; i < 6; i++)
        {
            float radians;
            bool flag;
            switch (i)
            {
                case < 4:
                    radians = i * (float)Math.PI / 2f;
                    flag = false;
                    break;
                case 4:
                    radians = -(float)Math.PI / 2f;
                    flag = true;
                    break;
                default:
                    radians = (float)Math.PI / 2f;
                    flag = true;
                    break;
            }

            for (var j = 0; j < 4; j++)
            {
                var radians2 = -j * (float)Math.PI / 2f;
                var num = (i << 2) + j;
                var m = Matrix.CreateRotationX((float)Math.PI / 2f) *
                        Matrix.CreateRotationZ(radians2) *
                        Matrix.CreateTranslation(0f, 0f, -0.5f) *
                        (flag ? Matrix.CreateRotationX(radians) : Matrix.CreateRotationY(radians)) *
                        Matrix.CreateTranslation(0.5f, 0.5f, 0.5f);
                BlockMeshes[num] = new BlockMesh();
                BlockMeshes[num].AppendModelPrimitive(
                    modelNode.RequireMesh().Primitives[0],
                    nodeAbsoluteTransform * m,
                    false,
                    false,
                    false,
                    false,
                    Color.White
                );
            }
        }

        var m2 = Matrix.CreateRotationY(-(float)Math.PI / 2f) * Matrix.CreateRotationZ((float)Math.PI / 2f);
        StandaloneBlockMesh.AppendModelPrimitive(
            modelNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * m2,
            false,
            false,
            false,
            false,
            Color.White
        );
    }
}
