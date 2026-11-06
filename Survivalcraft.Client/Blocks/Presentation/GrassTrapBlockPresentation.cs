using Engine.Graphics;

using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class GrassTrapBlockPresentation(GrassTrapBlock block, BlockPresentationCatalog catalog)
    : BlockPresentation(block, catalog)
{
    protected new GrassTrapBlock Block => (GrassTrapBlock)base.Block;

    public readonly BlockMesh BlockMesh = new();

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
        generator.GenerateShadedMeshVertices(
            this,
            x,
            y,
            z,
            BlockMesh,
            BlockColorsMap.GrassColorsMap.Lookup(generator.Terrain, x, y, z),
            null,
            [],
            geometry.SubsetAlphaTest
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
        BlocksManager.DrawMeshBlock(
            primitivesRenderer,
            StandaloneBlockMesh,
            color,
            size,
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

        var color = BlockColorsMap.GrassColorsMap.Lookup(
            subsystemTerrain.Terrain,
            Terrain.ToCell(position.X),
            Terrain.ToCell(position.Y),
            Terrain.ToCell(position.Z)
        );
        return new BlockDebrisParticleSystem(
            subsystemTerrain,
            position,
            strength,
            DestructionDebrisScale,
            color,
            GetFaceTextureSlot(4, value)
        );
    }

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/GrassTrap");
        var grassTrapNode = model.FindNode("GrassTrap")!;
        var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(grassTrapNode);
        var color = BlockColorsMap.GrassColorsMap.Lookup(8, 15);
        BlockMesh.AppendModelPrimitive(
            grassTrapNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * Matrix.CreateTranslation(0.5f, 0.75f, 0.5f),
            false,
            false,
            false,
            false,
            Color.White
        );
        StandaloneBlockMesh.AppendModelPrimitive(
            grassTrapNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform,
            false,
            false,
            false,
            false,
            color
        );
        base.Initialize();
    }
}
