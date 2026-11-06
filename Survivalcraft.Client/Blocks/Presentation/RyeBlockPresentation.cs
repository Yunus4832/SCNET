using Engine.Graphics;

using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class RyeBlockPresentation(RyeBlock block, BlockPresentationCatalog catalog)
    : CrossBlockPresentation(block, catalog)
{
    protected new RyeBlock Block => (RyeBlock)base.Block;

    public override void DrawBlock(
        PrimitivesRenderer3D primitivesRenderer,
        int value,
        Color color,
        float size,
        ref Matrix matrix,
        DrawBlockEnvironmentData environmentData
    )
    {
        if (RyeBlock.GetIsWild(Terrain.ExtractData(value)))
        {
            color *= BlockColorsMap.GrassColorsMap.Lookup(environmentData.Temperature, environmentData.Humidity);
            Catalog.DrawFlatOrImageExtrusionBlock(
                primitivesRenderer,
                value,
                size,
                ref matrix,
                null,
                color,
                false,
                environmentData
            );
        }
        else
        {
            Catalog.DrawFlatOrImageExtrusionBlock(
                primitivesRenderer,
                value,
                size,
                ref matrix,
                null,
                color,
                false,
                environmentData
            );
        }
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
        if (RyeBlock.GetIsWild(Terrain.ExtractData(value)))
        {
            var color = BlockColorsMap.GrassColorsMap.Lookup(generator.Terrain, x, y, z);
            generator.GenerateCrossingFaceVertices(
                this,
                value,
                x,
                y,
                z,
                color,
                GetFaceTextureSlot(0, value),
                geometry.SubsetAlphaTest
            );
        }
        else
        {
            generator.GenerateCrossingFaceVertices(
                this,
                value,
                x,
                y,
                z,
                Color.White,
                GetFaceTextureSlot(0, value),
                geometry.SubsetAlphaTest
            );
        }
    }

    public override int GetShadowStrength(int value)
    {
        return RyeBlock.GetSize(Terrain.ExtractData(value)) + 1;
    }

    public override BlockDebrisParticleSystem? CreateDebrisParticleSystem(
        SubsystemTerrain subsystemTerrain,
        Vector3 position,
        int value,
        float strength
    )
    {
        if (DestructionDebrisScale <= 0f) { return null; }

        if (!RyeBlock.GetIsWild(Terrain.ExtractData(value)))
        {
            return new BlockDebrisParticleSystem(
                subsystemTerrain,
                position,
                strength,
                DestructionDebrisScale,
                Color.White,
                GetFaceTextureSlot(4, value)
            );
        }

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

    public override int GetFaceTextureSlot(int face, int value)
    {
        var data = Terrain.ExtractData(value);
        var size = RyeBlock.GetSize(data);
        if (!RyeBlock.GetIsWild(data))
        {
            return 88 + size;
        }

        return size > 2 ? 87 : 86;
    }
}
