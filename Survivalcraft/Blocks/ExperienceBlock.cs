using Engine.Graphics;

namespace Game.Blocks;

public class ExperienceBlock : Block
{
    public const int Index = 248;

    public Texture2D Texture
    {
        get => field is not null ? field : throw new InvalidOperationException("Texture  is not initialized");
        set;
    } = null!;

    public override void Initialize()
    {
        base.Initialize();
        Texture = ContentManager.Get<Texture2D>("Textures/Experience");
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
        BlocksManager.DrawFlatBlock(
            primitivesRenderer,
            value,
            size * 0.18f,
            ref matrix,
            Texture, Color.White,
            true,
            environmentData
        );
    }
}
