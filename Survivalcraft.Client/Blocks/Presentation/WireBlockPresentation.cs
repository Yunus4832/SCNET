using Engine.Graphics;

using Game.Modding.Blocks;
using Game.World.Presentation;

namespace Game.Blocks.Presentation;

public class WireBlockPresentation(WireBlock block, BlockPresentationCatalog catalog)
    : BlockPresentation(block, catalog)
{
    protected new WireBlock Block => (WireBlock)base.Block;

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
        for (var i = 0; i < 6; i++)
        {
            if (Block.WireExistsOnFace(value, i))
            {
                generator.GenerateWireVertices(value, x, y, z, i, 0f, Vector2.Zero, geometry.SubsetOpaque);
            }
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
        var paintColor = Block.GetPaintColor(value);
        var color2 = paintColor.HasValue
            ? color * PalettePresentation.GetColor(environmentData.SubsystemTerrain, paintColor)
            : 1.25f * WireBlock.WireColor * color;
        BlocksManager.DrawMeshBlock(
            primitivesRenderer,
            StandaloneBlockMesh,
            color2,
            2f * size,
            ref matrix,
            environmentData
        );
    }

    public override string GetDisplayName(SubsystemTerrain? subsystemTerrain, int value)
    {
        var paintColor = Block.GetPaintColor(value);
        return PalettePresentation.GetName(paintColor, base.GetDisplayName(subsystemTerrain, value));
    }

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Wire");
        var wireNode = model.FindNode("Wire")!;
        var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(wireNode);
        StandaloneBlockMesh.AppendModelPrimitive(
            wireNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * Matrix.CreateTranslation(0f, -0.5f, 0f),
            false,
            false,
            false,
            false,
            Color.White
        );
        StandaloneBlockMesh.TransformTextureCoordinates(Matrix.CreateTranslation(0.9375f, 0f, 0f));
    }
}
