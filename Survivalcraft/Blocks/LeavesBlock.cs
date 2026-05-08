using Engine.Graphics;

namespace Game.Blocks;

public abstract class LeavesBlock : AlphaTestCubeBlock
{
    public readonly Random SharedRandom = new();

    public abstract Color GetLeavesBlockColor(int value, Terrain terrain, int x, int y, int z);

    public abstract Color GetLeavesItemColor(int value, DrawBlockEnvironmentData environmentData);

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
        BlocksManager.DrawCubeBlock(
            primitivesRenderer,
            value,
            new Vector3(size),
            ref matrix,
            color,
            color,
            environmentData
        );
    }

    public override BlockDebrisParticleSystem CreateDebrisParticleSystem(
        SubsystemTerrain subsystemTerrain,
        Vector3 position,
        int value,
        float strength
    )
    {
        var leavesBlockColor = GetLeavesBlockColor(value, subsystemTerrain.Terrain, Terrain.ToCell(position.X),
            Terrain.ToCell(position.Y), Terrain.ToCell(position.Z));
        return new BlockDebrisParticleSystem(subsystemTerrain, position, strength, DestructionDebrisScale,
            leavesBlockColor, GetFaceTextureSlot(4, value));
    }

    public override void GetDropValues(
        SubsystemTerrain subsystemTerrain,
        int oldValue,
        int newValue,
        int toolLevel,
        List<BlockDropValue> dropValues,
        out bool showDebris
    )
    {
        if (SharedRandom.Bool(0.15f))
        {
            dropValues.Add(new BlockDropValue
            {
                Value = 23,
                Count = 1
            });
            showDebris = true;
        }
        else
        {
            base.GetDropValues(subsystemTerrain, oldValue, newValue, toolLevel, dropValues, out showDebris);
        }
    }
}
