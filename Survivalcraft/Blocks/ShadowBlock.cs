using Engine.Graphics;

namespace Game.Blocks;

public class ShadowBlock : Block
{
    public const int Index = 257;

    public override void DrawBlock(
        PrimitivesRenderer3D primitivesRenderer,
        int value,
        Color color,
        float size,
        ref Matrix matrix,
        DrawBlockEnvironmentData environmentData
    )
    {
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

    public override int GetShadowStrength(int value)
    {
        return Terrain.ExtractData(value) - 128;
    }

    public static int SetShadowStrength(int data, int shadowStrength)
    {
        shadowStrength = MathUtils.Clamp(shadowStrength, -128, 128);
        return shadowStrength + 128;
    }
}
