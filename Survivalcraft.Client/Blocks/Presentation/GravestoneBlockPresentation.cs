using System.Globalization;

using Engine.Graphics;

using Game.Modding.Blocks;

using static Game.Blocks.GravestoneBlock;

namespace Game.Blocks.Presentation;

public class GravestoneBlockPresentation(GravestoneBlock block, BlockPresentationCatalog catalog)
    : BlockPresentation(block, catalog)
{
    protected new GravestoneBlock Block => (GravestoneBlock)base.Block;

    public BlockMesh[] BlockMeshes = new BlockMesh[16];

    public BlockMesh[] StandaloneBlockMeshes = new BlockMesh[16];

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
        if (num >= BlockMeshes.Length)
        {
            return;
        }

        var num2Value = y > 0 ? generator.Terrain.GetCellValueFast(x, y - 1, z) : 0;
        var num2 = Terrain.ExtractContents(num2Value);
        var num3 = Block.Catalog.Blocks[num2].GetBlockDigMethod(num2Value) != BlockDigMethod.Shovel;
        var flag = num2 is 7 or 4 or 52;
        var num4 = (int)(MathUtils.Hash((uint)(x + 172 * y + 18271 * z)) & 0xFFFF);
        var value2 = Matrix.Identity;
        if (!num3)
        {
            var radians = 0.2f * (num4 % 16 / 7.5f - 1f);
            var radians2 = 0.1f * ((num4 >> 4) % 16 / 7.5f - 1f);
            value2 = GetRotation(num) != 0
                ? Matrix.CreateTranslation(-0.5f, 0f, -0.5f) *
                  Matrix.CreateRotationZ(radians) *
                  Matrix.CreateRotationY(radians2) *
                  Matrix.CreateTranslation(0.5f, 0f, 0.5f)
                : Matrix.CreateTranslation(-0.5f, 0f, -0.5f) *
                  Matrix.CreateRotationX(radians) *
                  Matrix.CreateRotationY(radians2) *
                  Matrix.CreateTranslation(0.5f, 0f, 0.5f);
        }

        var f = num3 ? 0f : MathUtils.Sqr((num4 >> 8) % 16 / 15f);
        generator.GenerateMeshVertices(
            color: !flag
                ? Color.Lerp(Color.White, new Color(255, 233, 199), f)
                : Color.Lerp(new Color(217, 206, 123), new Color(229, 206, 123), f),
            block: this,
            x: x,
            y: y,
            z: z,
            blockMesh: BlockMeshes[num],
            matrix: value2,
            subset: geometry.SubsetOpaque
        );
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
        var num = Terrain.ExtractData(value);
        if (num < BlockMeshes.Length)
        {
            BlocksManager.DrawMeshBlock(
                primitivesRenderer,
                StandaloneBlockMeshes[num],
                color,
                size,
                ref matrix,
                environmentData
            );
        }
    }

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Graves");
        for (var i = 0; i < 16; i++)
        {
            var variant = GetVariant(i);
            var radians = GetRotation(i) == 0 ? 0f : (float)Math.PI / 2f;
            var name = "Grave" + (variant % 4 + 1).ToString(CultureInfo.InvariantCulture);
            var num = variant >= 4;
            var modelNode = model.FindNode(name)!;
            var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(modelNode);
            BlockMeshes[i] = new BlockMesh();
            BlockMeshes[i].AppendModelPrimitive(
                modelNode.RequireMesh().Primitives[0],
                nodeAbsoluteTransform * Matrix.CreateRotationY(radians) * Matrix.CreateTranslation(0.5f, 0f, 0.5f),
                false,
                false,
                false,
                false,
                Color.White
            );
            StandaloneBlockMeshes[i] = new BlockMesh();
            StandaloneBlockMeshes[i].AppendModelPrimitive(
                modelNode.RequireMesh().Primitives[0],
                nodeAbsoluteTransform * Matrix.CreateTranslation(0f, -0.5f, 0f),
                false,
                false,
                false,
                false,
                Color.White
            );
            if (num)
            {
                var nodeAbsoluteTransform2 = BlockMesh.GetNodeAbsoluteTransform(model.FindNode("Plinth")!);
                BlockMeshes[i].AppendModelPrimitive(
                    model.FindNode("Plinth")!.RequireMesh().Primitives[0],
                    nodeAbsoluteTransform2 * Matrix.CreateRotationY(radians) * Matrix.CreateTranslation(0.5f, 0f, 0.5f),
                    false,
                    false,
                    false,
                    false,
                    Color.White
                );
                StandaloneBlockMeshes[i].AppendModelPrimitive(
                    model.FindNode("Plinth")!.RequireMesh().Primitives[0],
                    nodeAbsoluteTransform2 * Matrix.CreateTranslation(0f, -0.5f, 0f),
                    false,
                    false,
                    false,
                    false,
                    Color.White
                );
            }
        }

        base.Initialize();
    }
}
