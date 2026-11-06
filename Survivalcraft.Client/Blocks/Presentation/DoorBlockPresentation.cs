using Engine.Graphics;

using Game.Modding.Blocks;

using static Game.Blocks.DoorBlock;

namespace Game.Blocks.Presentation;

public abstract class DoorBlockPresentation(DoorBlock block, BlockPresentationCatalog catalog, string modelName)
    : BlockPresentation(block, catalog)
{
    protected new DoorBlock Block => (DoorBlock)base.Block;

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
        if (IsBottomPart(generator.Terrain, Block.Catalog, x, y, z) && num < BlockMeshesByData.Length)
        {
            generator.GenerateMeshVertices(
                this,
                x,
                y,
                z,
                BlockMeshesByData[num],
                Color.White,
                null,
                geometry.SubsetAlphaTest
            );
        }

        var centerOffset = GetRightHanded(num) ? new Vector2(-0.45f, 0f) : new Vector2(0.45f, 0f);
        generator.GenerateWireVertices(
            value,
            x,
            y,
            z,
            GetHingeFace(num),
            0.01f,
            centerOffset,
            geometry.SubsetOpaque
        );
    }

    public override TerrainVertex SetDiggingCrackingTextureTransform(TerrainVertex vertex)
    {
        var fx = vertex.Tx / 32767f;
        var fy = vertex.Ty / 37267f;
        vertex.Tx = fx <= _tx ? (short)0 : (short)32767;
        vertex.Ty = fy <= _ty ? (short)0 : (short)32767;
        return vertex;
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
            0.75f * size,
            ref matrix,
            environmentData
        );
    }

    public override int GetShadowStrength(int value)
    {
        return !GetOpen(Terrain.ExtractData(value)) ? ShadowStrength : 4;
    }

    public readonly string ModelName = modelName;

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>(ModelName);
        var doorNode = model.FindNode("Door")!;
        var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(doorNode);
        for (var i = 0; i < 16; i++)
        {
            var rotation = GetRotation(i);
            var open = GetOpen(i);
            var rightHanded = GetRightHanded(i);
            float num = !rightHanded ? 1 : -1;
            BlockMeshesByData[i] = new BlockMesh();
            var identity = Matrix.Identity;
            identity *= Matrix.CreateScale(0f - num, 1f, 1f);
            identity *= Matrix.CreateTranslation((0.5f - Block.PivotDistance) * num, 0f, 0f) *
                        Matrix.CreateRotationY(open ? num * (float)Math.PI / 2f : 0f) *
                        Matrix.CreateTranslation((0f - (0.5f - Block.PivotDistance)) * num, 0f, 0f);
            identity *= Matrix.CreateTranslation(0f, 0f, 0.5f - Block.PivotDistance) *
                        Matrix.CreateRotationY(rotation * (float)Math.PI / 2f) *
                        Matrix.CreateTranslation(0.5f, 0f, 0.5f);
            BlockMeshesByData[i].AppendModelPrimitive(
                doorNode.RequireMesh().Primitives[0],
                nodeAbsoluteTransform * identity,
                false,
                !rightHanded,
                false,
                false,
                Color.White
            );
        }

        StandaloneBlockMesh.AppendModelPrimitive(
            doorNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * Matrix.CreateTranslation(0f, -1f, 0f),
            false,
            false,
            false,
            false,
            Color.White
        );
        base.Initialize();
    }


    private const float _tx = 0.09375f;


    private const float _ty = 0.375f;
}
