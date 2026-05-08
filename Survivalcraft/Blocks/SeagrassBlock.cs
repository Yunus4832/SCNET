using Engine.Graphics;

namespace Game.Blocks;

public class SeagrassBlock : WaterPlantBlock
{
    public new const int Index = 233;

    public override int GetFaceTextureSlot(int face, int value)
    {
        return face < 0 ? 105 : base.GetFaceTextureSlot(face, value);
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
        BlocksManager.DrawFlatOrImageExtrusionBlock(
            primitivesRenderer,
            value,
            size,
            ref matrix,
            null,
            color * BlockColorsMap.SeagrassColorsMap.Lookup(
                environmentData.Temperature,
                environmentData.Humidity),
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
        var color = BlockColorsMap.SeagrassColorsMap.Lookup(generator.Terrain, x, y, z);
        generator.GenerateCrossingFaceVertices(this, value, x, y, z, color, GetFaceTextureSlot(-1, value),
            geometry.SubsetAlphaTest);
        base.GenerateTerrainVertices(generator, geometry, value, x, y, z);
    }

    public override BlockDebrisParticleSystem CreateDebrisParticleSystem(
        SubsystemTerrain subsystemTerrain,
        Vector3 position,
        int value,
        float strength
    )
    {
        return new BlockDebrisParticleSystem(subsystemTerrain, position, 0.75f * strength, DestructionDebrisScale,
            BlockColorsMap.SeagrassColorsMap.Lookup(subsystemTerrain.Terrain, Terrain.ToCell(position.X),
                Terrain.ToCell(position.Y), Terrain.ToCell(position.Z)), 104);
    }
}
