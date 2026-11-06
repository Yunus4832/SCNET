using Engine.Graphics;

using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class TallGrassBlockPresentation(TallGrassBlock block, BlockPresentationCatalog catalog)
    : CrossBlockPresentation(block, catalog)
{
    protected new TallGrassBlock Block => (TallGrassBlock)base.Block;

    public override int GetFaceTextureSlot(int face, int value)
    {
        return !TallGrassBlock.GetIsSmall(Terrain.ExtractData(value)) ? 85 : 84;
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
        Catalog.DrawFlatOrImageExtrusionBlock(
            primitivesRenderer,
            value,
            size,
            ref matrix,
            null,
            color *
            BlockColorsMap.GrassColorsMap.Lookup(
                environmentData.Temperature,
                environmentData.Humidity
            ),
            false,
            environmentData
        );
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
        generator.GenerateCrossingFaceVertices(
            this,
            value,
            x,
            y,
            z,
            BlockColorsMap.GrassColorsMap.Lookup(generator.Terrain, x, y, z),
            GetFaceTextureSlot(0, value),
            geometry.SubsetAlphaTest
        );
    }

    public override int GetShadowStrength(int value)
    {
        if (!TallGrassBlock.GetIsSmall(Terrain.ExtractData(value)))
        {
            return ShadowStrength;
        }

        return ShadowStrength / 2;
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
}
