using Engine.Graphics;

using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public abstract class LeavesBlockPresentation(LeavesBlock block, BlockPresentationCatalog catalog)
    : AlphaTestCubeBlockPresentation(block, catalog)
{
    protected new LeavesBlock Block => (LeavesBlock)base.Block;

    public override void GenerateTerrainVertices(
        BlockGeometryGenerator generator,
        TerrainGeometry geometry,
        int value,
        int x,
        int y,
        int z
    )
    {
        var leavesBlockColor = GetLeavesBlockColor(value, generator.Terrain, x, y, z);
        generator.GenerateCubeVertices(this, value, x, y, z, leavesBlockColor, geometry.AlphaTestSubsetsByFace);
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
        color *= GetLeavesItemColor(value, environmentData);
        Catalog.DrawCubeBlock(
            primitivesRenderer,
            value,
            new Vector3(size),
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

        var leavesBlockColor = GetLeavesBlockColor(
            value,
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
            leavesBlockColor,
            GetFaceTextureSlot(4, value)
        );
    }

    public abstract Color GetLeavesBlockColor(int value, Terrain terrain, int x, int y, int z);


    public abstract Color GetLeavesItemColor(int value, DrawBlockEnvironmentData environmentData);
}
