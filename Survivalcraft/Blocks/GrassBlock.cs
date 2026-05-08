using Engine.Graphics;

namespace Game.Blocks;

public class GrassBlock : CubeBlock
{
    public const int Index = 8;

    public override int GetFaceTextureSlot(int face, int value)
    {
        return face switch
        {
            4 => 0,
            5 => 2,
            _ => Terrain.ExtractData(value) == 0 ? 3 : 68
        };
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
        var topColor = color * BlockColorsMap.GrassColorsMap.Lookup(
            environmentData.Temperature,
            environmentData.Humidity
        );
        BlocksManager.DrawCubeBlock(
            primitivesRenderer,
            value,
            new Vector3(size),
            ref matrix,
            color,
            topColor,
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
        var topColor = BlockColorsMap.GrassColorsMap.Lookup(generator.Terrain, x, y, z);
        var topColor2 = BlockColorsMap.GrassColorsMap.Lookup(generator.Terrain, x + 1, y, z);
        var topColor3 = BlockColorsMap.GrassColorsMap.Lookup(generator.Terrain, x + 1, y, z + 1);
        var topColor4 = BlockColorsMap.GrassColorsMap.Lookup(generator.Terrain, x, y, z + 1);
        generator.GenerateCubeVertices(this, value, x, y, z, 1f, 1f, 1f, 1f, Color.White, topColor, topColor2,
            topColor3, topColor4, -1, geometry.OpaqueSubsetsByFace);
    }

    public override BlockDebrisParticleSystem CreateDebrisParticleSystem(
        SubsystemTerrain subsystemTerrain,
        Vector3 position,
        int value,
        float strength
    )
    {
        return new BlockDebrisParticleSystem(
            subsystemTerrain,
            position,
            strength,
            DestructionDebrisScale,
            Color.White,
            2
        );
    }
}
