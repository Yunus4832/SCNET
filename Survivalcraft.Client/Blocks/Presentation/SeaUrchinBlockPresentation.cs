using Engine.Graphics;

using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class SeaUrchinBlockPresentation(SeaUrchinBlock block, BlockPresentationCatalog catalog)
    : BottomSuckerBlockPresentation(block, catalog)
{
    protected new SeaUrchinBlock Block => (SeaUrchinBlock)base.Block;

    public BlockMesh[] BlockMeshes = new BlockMesh[24];

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
        var data = Terrain.ExtractData(value);
        var face = BottomSuckerBlock.GetFace(data);
        var subvariant = BottomSuckerBlock.GetSubvariant(data);
        var color = SeaUrchinBlock.Colors[subvariant];
        generator.GenerateMeshVertices(
            this,
            x,
            y,
            z,
            BlockMeshes[4 * face + subvariant],
            color,
            null,
            geometry.SubsetOpaque
        );
        base.GenerateTerrainVertices(generator, geometry, value, x, y, z);
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
            color * new Color(40, 40, 40),
            3f * size,
            ref matrix,
            environmentData
        );
    }

    public override BlockDebrisParticleSystem? CreateDebrisParticleSystem(
        SubsystemTerrain subsystemTerrain,
        Vector3 position,
        int value,
        float strength
    )
    {
        if (DestructionDebrisScale <= 0f) { return null; }

        return new BlockDebrisParticleSystem(
            subsystemTerrain,
            position,
            0.75f * strength,
            DestructionDebrisScale,
            new Color(64, 64, 64),
            TextureSlot
        );
    }

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/SeaUrchin");
        var urchinNode = model.FindNode("Urchin")!;
        var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(urchinNode);
        var bottomNode = model.FindNode("Bottom")!;
        var nodeAbsoluteTransform2 = BlockMesh.GetNodeAbsoluteTransform(bottomNode);
        for (var i = 0; i < 6; i++)
        {
            for (var j = 0; j < 4; j++)
            {
                var zero = Vector2.Zero;
                if (i < 4)
                {
                    zero.Y = i * (float)Math.PI / 2f;
                }
                else
                {
                    zero.X = i == 4 ? -(float)Math.PI / 2f : (float)Math.PI / 2f;
                }

                var m = Matrix.CreateRotationX((float)Math.PI / 2f) *
                        Matrix.CreateRotationZ(0.3f + 2f * j) *
                        Matrix.CreateTranslation(SeaUrchinBlock.Offsets[j].X, SeaUrchinBlock.Offsets[j].Y, -0.49f) *
                        Matrix.CreateRotationX(zero.X) *
                        Matrix.CreateRotationY(zero.Y) *
                        Matrix.CreateTranslation(0.5f, 0.5f, 0.5f);
                var num = 4 * i + j;
                BlockMeshes[num] = new BlockMesh();
                BlockMeshes[num].AppendModelPrimitive(
                    urchinNode.RequireMesh().Primitives[0],
                    nodeAbsoluteTransform * m,
                    false,
                    false,
                    false,
                    false,
                    Color.White
                );
            }
        }

        StandaloneBlockMesh = new BlockMesh();
        StandaloneBlockMesh.AppendModelPrimitive(
            urchinNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * Matrix.CreateTranslation(0f, -0.1f, 0f),
            false,
            false,
            false,
            false,
            Color.White
        );
        StandaloneBlockMesh.AppendModelPrimitive(
            bottomNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform2 * Matrix.CreateTranslation(0f, -0.1f, 0f),
            false,
            false,
            false,
            false,
            Color.White
        );
        base.Initialize();
    }
}
