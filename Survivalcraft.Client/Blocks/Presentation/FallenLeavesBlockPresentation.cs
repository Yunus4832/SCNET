using Engine.Graphics;

using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class FallenLeavesBlockPresentation(FallenLeavesBlock block, BlockPresentationCatalog catalog)
    : CubeBlockPresentation(block, catalog)
{
    protected new FallenLeavesBlock Block => (FallenLeavesBlock)base.Block;

    public override void GenerateTerrainVertices(
        BlockGeometryGenerator generator,
        TerrainGeometry geometry,
        int value,
        int x,
        int y,
        int z
    )
    {
        var sideColor = new Color(180, 170, 160);
        var color = GetColor(x, y, z);
        var color2 = GetColor(x, y, z + 1);
        var color3 = GetColor(x + 1, y, z);
        var color4 = GetColor(x + 1, y, z + 1);
        generator.GenerateCubeVertices(
            this,
            value,
            x,
            y,
            z,
            0.0625f,
            0.0625f,
            0.0625f,
            0.0625f,
            sideColor,
            color,
            color3,
            color4,
            color2,
            -1,
            geometry.AlphaTestSubsetsByFace
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
        Catalog.DrawCubeBlock(
            primitivesRenderer,
            value,
            new Vector3(size),
            0.0625f,
            ref matrix,
            color,
            color,
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

        var color = GetColor(Terrain.ToCell(position.X), Terrain.ToCell(position.Y), Terrain.ToCell(position.Z));
        return new BlockDebrisParticleSystem(
            subsystemTerrain,
            position,
            strength,
            DestructionDebrisScale,
            color,
            GetFaceTextureSlot(4, value)
        );
    }

    private static Color GetColor(int x, int y, int z)
    {
        var num = (uint)MathUtils.Hash(x + y * 59 + z * 2411);
        return Color.Lerp(new Color(128, 110, 110), new Color(255, 255, 220), num / 4.2949673E+09f);
    }
}
