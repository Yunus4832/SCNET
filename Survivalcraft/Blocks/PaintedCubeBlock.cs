using Engine.Graphics;

namespace Game.Blocks;

public abstract class PaintedCubeBlock(int coloredTextureSlot) : CubeBlock, IPaintableBlock
{
    public int ColoredTextureSlot = coloredTextureSlot;

    public int? GetPaintColor(int value)
    {
        return GetColor(Terrain.ExtractData(value));
    }

    public int Paint(SubsystemTerrain? terrain, int value, int? color)
    {
        var data = Terrain.ExtractData(value);
        return Terrain.ReplaceData(value, SetColor(data, color));
    }

    public override int GetFaceTextureSlot(int face, int value)
    {
        return !IsColored(Terrain.ExtractData(value)) ? TextureSlot : ColoredTextureSlot;
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
        var data = Terrain.ExtractData(value);
        var color = SubsystemPalette.GetColor(generator, GetColor(data));
        generator.GenerateCubeVertices(this, value, x, y, z, color, geometry.OpaqueSubsetsByFace);
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
        var data = Terrain.ExtractData(value);
        color *= SubsystemPalette.GetColor(environmentData, GetColor(data));
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

    public override IEnumerable<int> GetCreativeValues()
    {
        yield return Terrain.MakeBlockValue(BlockIndex, 0, SetColor(0, null));
        var i = 0;
        while (i < 16)
        {
            yield return Terrain.MakeBlockValue(BlockIndex, 0, SetColor(0, i));
            var num = i + 1;
            i = num;
        }
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
        var data = Terrain.ExtractData(oldValue);
        if (GetColor(data).HasValue)
        {
            showDebris = true;
            if (toolLevel >= RequiredToolLevel)
            {
                dropValues.Add(new BlockDropValue
                {
                    Value = Terrain.MakeBlockValue(DropContent, 0, data),
                    Count = (int)DropCount
                });
            }
        }
        else
        {
            base.GetDropValues(subsystemTerrain, oldValue, newValue, toolLevel, dropValues, out showDebris);
        }
    }

    public override BlockDebrisParticleSystem CreateDebrisParticleSystem(
        SubsystemTerrain subsystemTerrain,
        Vector3 position,
        int value,
        float strength
    )
    {
        var data = Terrain.ExtractData(value);
        var color = SubsystemPalette.GetColor(subsystemTerrain, GetColor(data));
        return new BlockDebrisParticleSystem(subsystemTerrain, position, strength, DestructionDebrisScale, color,
            GetFaceTextureSlot(0, value));
    }

    public override string GetDisplayName(SubsystemTerrain? subsystemTerrain, int value)
    {
        var data = Terrain.ExtractData(value);
        return SubsystemPalette.GetName(
            GetColor(data),
            LanguageControl.GetBlock($"{GetType().Name}:{data.ToString()}", "DisplayName")
        );
    }

    public override string GetCategory(int value)
    {
        return !GetColor(Terrain.ExtractData(value)).HasValue ? base.GetCategory(value) : "Painted";
    }

    public static bool IsColored(int data)
    {
        return (data & 1) != 0;
    }

    public static int? GetColor(int data)
    {
        if ((data & 1) != 0)
        {
            return (data >> 1) & 0xF;
        }

        return null;
    }

    public static int SetColor(int data, int? color)
    {
        if (color.HasValue)
        {
            return (data & -32) | 1 | (color.Value << 1);
        }

        return data & -32;
    }
}
