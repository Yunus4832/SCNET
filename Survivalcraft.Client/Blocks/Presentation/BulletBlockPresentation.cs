using Engine.Graphics;

using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class BulletBlockPresentation(BulletBlock block, BlockPresentationCatalog catalog)
    : FlatBlockPresentation(block, catalog)
{
    protected new BulletBlock Block => (BulletBlock)base.Block;

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
        var bulletType = (int)BulletBlock.GetBulletType(Terrain.ExtractData(value));
        var size2 = bulletType >= 0 && bulletType < BulletBlock.Sizes.Length
            ? size * BulletBlock.Sizes[bulletType]
            : size;
        Catalog.DrawFlatOrImageExtrusionBlock(
            primitivesRenderer,
            value,
            size2,
            ref matrix,
            null,
            color,
            false,
            environmentData
        );
    }

    public override string GetDisplayName(SubsystemTerrain? subsystemTerrain, int value)
    {
        var bulletType = (int)BulletBlock.GetBulletType(Terrain.ExtractData(value));
        if (bulletType < 0 || bulletType >= BulletBlock.DisplayNames.Length)
        {
            return string.Empty;
        }

        return BulletBlock.DisplayNames[bulletType];
    }

    public override int GetFaceTextureSlot(int face, int value)
    {
        var bulletType = (int)BulletBlock.GetBulletType(Terrain.ExtractData(value));
        if (bulletType < 0 || bulletType >= TextureSlots.Length)
        {
            return 229;
        }

        return TextureSlots[bulletType];
    }

    public static readonly int[] TextureSlots =
    [
        229,
        231,
        229
    ];
}
