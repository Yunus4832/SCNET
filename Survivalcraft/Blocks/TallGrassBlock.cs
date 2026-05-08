using Engine.Graphics;

namespace Game.Blocks;

public class TallGrassBlock : CrossBlock
{
    public const int Index = 19;

    public override void GetDropValues(
        SubsystemTerrain subsystemTerrain,
        int oldValue,
        int newValue,
        int toolLevel,
        List<BlockDropValue> dropValues,
        out bool showDebris
    )
    {
        var data = Terrain.ExtractData(oldValue);
        if (!GetIsSmall(data))
        {
            dropValues.Add(new BlockDropValue
            {
                Value = Terrain.MakeBlockValue(19, 0, data),
                Count = 1
            });
        }

        showDebris = true;
    }

    public override int GetFaceTextureSlot(int face, int value)
    {
        return !GetIsSmall(Terrain.ExtractData(value)) ? 85 : 84;
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
            color * BlockColorsMap.GrassColorsMap.Lookup(
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
        generator.GenerateCrossingFaceVertices(this, value, x, y, z,
            BlockColorsMap.GrassColorsMap.Lookup(generator.Terrain, x, y, z), GetFaceTextureSlot(0, value),
            geometry.SubsetAlphaTest);
    }

    public override int GetShadowStrength(int value)
    {
        if (!GetIsSmall(Terrain.ExtractData(value)))
        {
            return ShadowStrength;
        }

        return ShadowStrength / 2;
    }

    public override BlockDebrisParticleSystem CreateDebrisParticleSystem(SubsystemTerrain subsystemTerrain,
        Vector3 position, int value, float strength)
    {
        var color = BlockColorsMap.GrassColorsMap.Lookup(subsystemTerrain.Terrain, Terrain.ToCell(position.X),
            Terrain.ToCell(position.Y), Terrain.ToCell(position.Z));
        return new BlockDebrisParticleSystem(subsystemTerrain, position, strength, DestructionDebrisScale, color,
            GetFaceTextureSlot(4, value));
    }

    public static bool GetIsSmall(int data)
    {
        return (data & 8) != 0;
    }

    public static int SetIsSmall(int data, bool isSmall)
    {
        if (!isSmall)
        {
            return data & -9;
        }

        return data | 8;
    }
}
