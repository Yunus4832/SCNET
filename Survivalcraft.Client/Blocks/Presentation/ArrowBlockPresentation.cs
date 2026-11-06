using Engine.Graphics;

using Game.Modding.Blocks;

using static Game.Blocks.ArrowBlock;

namespace Game.Blocks.Presentation;

public class ArrowBlockPresentation(ArrowBlock block, BlockPresentationCatalog catalog)
    : BlockPresentation(block, catalog)
{
    protected new ArrowBlock Block => (ArrowBlock)base.Block;

    public List<BlockMesh> StandaloneBlockMeshes = [];

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
        var arrowType = (int)GetArrowType(Terrain.ExtractData(value));
        if (arrowType >= 0 && arrowType < StandaloneBlockMeshes.Count)
        {
            BlocksManager.DrawMeshBlock(
                primitivesRenderer,
                StandaloneBlockMeshes[arrowType],
                color,
                2f * size,
                ref matrix,
                environmentData
            );
        }
    }

    public override float GetIconViewScale(int value, DrawBlockEnvironmentData environmentData)
    {
        var arrowType = (int)GetArrowType(Terrain.ExtractData(value));
        if (arrowType < 0 || arrowType >= IconViewScales.Length)
        {
            return 1f;
        }

        return IconViewScales[arrowType];
    }

    public override string GetDisplayName(SubsystemTerrain? subsystemTerrain, int value)
    {
        var arrowType = (int)GetArrowType(Terrain.ExtractData(value));
        if (arrowType < 0 || arrowType >= DisplayNames.Length)
        {
            return string.Empty;
        }

        return DisplayNames[arrowType];
    }

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Arrows");
        foreach (var enumValue in EnumUtils.GetEnumValues(typeof(ArrowType)))
        {
            if (enumValue > 15)
            {
                throw new InvalidOperationException("Too many arrow types.");
            }

            var shaftNameNode = model.FindNode(ShaftNames[enumValue])!;
            var stabilizerNameNode = model.FindNode(StabilizerNames[enumValue])!;
            var tipNameNode = model.FindNode(TipNames[enumValue])!;
            var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(shaftNameNode);
            var nodeAbsoluteTransform2 = BlockMesh.GetNodeAbsoluteTransform(stabilizerNameNode);
            var nodeAbsoluteTransform3 = BlockMesh.GetNodeAbsoluteTransform(tipNameNode);
            var blockMesh = new BlockMesh();
            blockMesh.AppendModelPrimitive(
                tipNameNode.RequireMesh().Primitives[0],
                nodeAbsoluteTransform3 * Matrix.CreateTranslation(0f, Offsets[enumValue], 0f),
                false,
                false,
                false,
                false,
                Color.White
            );
            blockMesh.TransformTextureCoordinates(
                Matrix.CreateTranslation(
                    TipTextureSlots[enumValue] % 16 / 16f,
                    TipTextureSlots[enumValue] / 16 / 16f,
                    0f
                )
            );
            var blockMesh2 = new BlockMesh();
            blockMesh2.AppendModelPrimitive(
                shaftNameNode.RequireMesh().Primitives[0],
                nodeAbsoluteTransform * Matrix.CreateTranslation(0f, Offsets[enumValue], 0f),
                false,
                false,
                false,
                false,
                Color.White
            );
            blockMesh2.TransformTextureCoordinates(
                Matrix.CreateTranslation(
                    ShaftTextureSlots[enumValue] % 16 / 16f,
                    ShaftTextureSlots[enumValue] / 16 / 16f,
                    0f
                )
            );
            var blockMesh3 = new BlockMesh();
            blockMesh3.AppendModelPrimitive(
                stabilizerNameNode.RequireMesh().Primitives[0],
                nodeAbsoluteTransform2 * Matrix.CreateTranslation(0f, Offsets[enumValue], 0f),
                false,
                false,
                true,
                false,
                Color.White
            );
            blockMesh3.TransformTextureCoordinates(
                Matrix.CreateTranslation(
                    StabilizerTextureSlots[enumValue] % 16 / 16f,
                    StabilizerTextureSlots[enumValue] / 16 / 16f,
                    0f
                )
            );
            var blockMesh4 = new BlockMesh();
            blockMesh4.AppendBlockMesh(blockMesh);
            blockMesh4.AppendBlockMesh(blockMesh2);
            blockMesh4.AppendBlockMesh(blockMesh3);
            StandaloneBlockMeshes.Add(blockMesh4);
        }

        base.Initialize();
    }

    public static int[] TipTextureSlots = [47, 1, 63, 182, 62, 63, 182, 183, 79];


    public static int[] ShaftTextureSlots = [4, 4, 4, 4, 4, 63, 63, 63, 4];


    public static int[] StabilizerTextureSlots = [15, 15, 15, 15, 15, 63, 63, 63, 15];
}
