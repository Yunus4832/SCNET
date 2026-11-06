using Engine.Graphics;

using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class ExperienceBlockPresentation(ExperienceBlock block, BlockPresentationCatalog catalog)
    : BlockPresentation(block, catalog)
{
    protected new ExperienceBlock Block => (ExperienceBlock)base.Block;

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

    public override int GetFaceTextureSlot(int face, int value)
    {
        return 0;
    }

    public override int GetTextureSlotCount(int value)
    {
        return 1;
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
        Catalog.DrawFlatBlock(
            primitivesRenderer,
            value,
            size * 0.18f,
            ref matrix,
            Texture,
            Color.White,
            true,
            environmentData
        );
    }

    public Texture2D Texture { get; private set; } = null!;

    public override void Initialize()
    {
        Texture = ContentManager.Get<Texture2D>("Textures/Experience");
        base.Initialize();
    }
}
