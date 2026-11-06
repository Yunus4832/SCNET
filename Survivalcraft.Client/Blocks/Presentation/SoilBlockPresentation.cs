using Engine.Graphics;

using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class SoilBlockPresentation(SoilBlock block, BlockPresentationCatalog catalog)
    : CubeBlockPresentation(block, catalog)
{
    protected new SoilBlock Block => (SoilBlock)base.Block;

    public override int GetFaceTextureSlot(int face, int value)
    {
        var nitrogen = SoilBlock.GetNitrogen(Terrain.ExtractData(value));
        if (face != 4)
        {
            return 2;
        }

        return nitrogen <= 0 ? 37 : 53;
    }

    public override string GetDisplayName(SubsystemTerrain? subsystemTerrain, int value)
    {
        var data = Terrain.ExtractData(value);
        var nitrogen = SoilBlock.GetNitrogen(data);
        var hydration = SoilBlock.GetHydration(data);
        switch (nitrogen)
        {
            case > 0 when hydration:
                _ = Localization.ClientLanguage.Catalog.Get(SoilBlock.TypeName, 2);
                return Localization.ClientLanguage.Catalog.Get(SoilBlock.TypeName, 1);
            case > 0:
                _ = Localization.ClientLanguage.Catalog.Get(SoilBlock.TypeName, 2);
                return Localization.ClientLanguage.Catalog.Get(SoilBlock.TypeName, 2);
        }

        return Localization.ClientLanguage.Catalog.Get(SoilBlock.TypeName, hydration ? 3 : 4);
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
        var color = SoilBlock.GetHydration(Terrain.ExtractData(value)) ? new Color(180, 170, 150) : Color.White;
        generator.GenerateCubeVertices(
            this,
            value,
            x,
            y,
            z,
            0.9375f,
            0.9375f,
            0.9375f,
            0.9375f,
            color,
            color,
            color,
            color,
            color,
            -1,
            geometry.OpaqueSubsetsByFace
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
        var c = SoilBlock.GetHydration(Terrain.ExtractData(value)) ? new Color(180, 170, 150) : Color.White;
        base.DrawBlock(primitivesRenderer, value, color * c, size, ref matrix, environmentData);
    }
}
