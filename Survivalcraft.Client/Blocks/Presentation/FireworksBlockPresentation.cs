using Engine.Graphics;

using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class FireworksBlockPresentation(FireworksBlock block, BlockPresentationCatalog catalog)
    : BlockPresentation(block, catalog)
{
    protected new FireworksBlock Block => (FireworksBlock)base.Block;

    public BlockMesh[] BodyBlockMeshes = new BlockMesh[2];

    public BlockMesh[] FinsBlockMeshes = new BlockMesh[2];

    public BlockMesh[] HeadBlockMeshes = new BlockMesh[64];

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
        var data = Terrain.ExtractData(value);
        var color2 = FireworksBlock.GetColor(data);
        var shape = FireworksBlock.GetShape(data);
        var altitude = FireworksBlock.GetAltitude(data);
        var flickering = FireworksBlock.GetFlickering(data);
        BlocksManager.DrawMeshBlock(
            primitivesRenderer,
            HeadBlockMeshes[(int)shape * 8 + color2],
            color,
            2f * size,
            ref matrix,
            environmentData
        );
        BlocksManager.DrawMeshBlock(
            primitivesRenderer,
            BodyBlockMeshes[altitude],
            color,
            2f * size,
            ref matrix,
            environmentData
        );
        BlocksManager.DrawMeshBlock(
            primitivesRenderer,
            FinsBlockMeshes[flickering ? 1 : 0],
            color,
            2f * size,
            ref matrix,
            environmentData
        );
    }

    public override string GetDisplayName(SubsystemTerrain? subsystemTerrain, int value)
    {
        var data = Terrain.ExtractData(value);
        var color = FireworksBlock.GetColor(data);
        var shape = FireworksBlock.GetShape(data);
        var altitude = FireworksBlock.GetAltitude(data);
        var flickering = FireworksBlock.GetFlickering(data);
        return string.Format(
            Localization.ClientLanguage.Catalog.GetFireworks("Other", "1"),
            Localization.ClientLanguage.Catalog.GetFireworks("FireworksColorDisplayNames", color.ToString()),
            flickering ? Localization.ClientLanguage.Catalog.GetFireworks("Other", "2") : null,
            Localization.ClientLanguage.Catalog.GetFireworks("ShapeDisplayNames", ((int)shape).ToString()),
            altitude == 0
                ? Localization.ClientLanguage.Catalog.GetFireworks("Other", "3")
                : Localization.ClientLanguage.Catalog.GetFireworks("Other", "4")
        );
    }

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Fireworks");
        var bodyNode = model.FindNode("Body")!;
        var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(bodyNode);
        var finsNode = model.FindNode("Fins")!;
        var nodeAbsoluteTransform2 = BlockMesh.GetNodeAbsoluteTransform(finsNode);
        for (var i = 0; i < 64; i++)
        {
            var num = i / 8;
            var num2 = i % 8;
            var color = FireworksBlock.FireworksColors[num2];
            color *= 0.75f;
            color.A = byte.MaxValue;
            var headNameNode = model.FindNode(FireworksBlock.HeadNames[num])!;
            var nodeAbsoluteTransform3 = BlockMesh.GetNodeAbsoluteTransform(headNameNode);
            HeadBlockMeshes[i] = new BlockMesh();
            HeadBlockMeshes[i].AppendModelPrimitive(
                headNameNode.RequireMesh().Primitives[0],
                nodeAbsoluteTransform3 * Matrix.CreateTranslation(0f, -0.25f, 0f),
                false,
                false,
                false,
                false,
                color
            );
        }

        for (var j = 0; j < 2; j++)
        {
            var num3 = 0.5f + j * 0.5f;
            var m = Matrix.CreateScale(new Vector3(num3, 1f, num3));
            BodyBlockMeshes[j] = new BlockMesh();
            BodyBlockMeshes[j].AppendModelPrimitive(
                bodyNode.RequireMesh().Primitives[0],
                nodeAbsoluteTransform * m * Matrix.CreateTranslation(0f, -0.25f, 0f),
                false,
                false,
                false,
                false,
                Color.White
            );
        }

        for (var k = 0; k < 2; k++)
        {
            FinsBlockMeshes[k] = new BlockMesh();
            FinsBlockMeshes[k].AppendModelPrimitive(
                finsNode.RequireMesh().Primitives[0],
                nodeAbsoluteTransform2 * Matrix.CreateTranslation(0f, -0.25f, 0f),
                false,
                false,
                false,
                false,
                k == 0 ? Color.White : new Color(224, 0, 0)
            );
        }

        base.Initialize();
    }
}
