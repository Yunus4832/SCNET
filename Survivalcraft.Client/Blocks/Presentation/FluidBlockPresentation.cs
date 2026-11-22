using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public abstract class FluidBlockPresentation(FluidBlock block, BlockPresentationCatalog catalog)
    : CubeBlockPresentation(block, catalog)
{
    protected new FluidBlock Block => (FluidBlock)base.Block;

    public override int GetFaceTextureSlot(int face, int value)
    {
        if (face >= 4)
        {
            return TextureSlot;
        }

        return TextureSlot + 16;
    }

    public override bool ShouldGenerateFace(
        SubsystemTerrain subsystemTerrain,
        int face,
        int value,
        int neighborValue,
        int x,
        int y,
        int z
    )
    {
        var contents = Terrain.ExtractContents(neighborValue);
        if (Block.IsTheSameFluid(contents))
        {
            return false;
        }

        return base.ShouldGenerateFace(subsystemTerrain, face, value, neighborValue, x, y, z);
    }

    public override bool ShouldGenerateFace(SubsystemTerrain subsystemTerrain, int face, int value, int neighborValue)
    {
        var contents = Terrain.ExtractContents(neighborValue);
        if (Block.IsTheSameFluid(contents))
        {
            return false;
        }

        return base.ShouldGenerateFace(subsystemTerrain, face, value, neighborValue);
    }

    public void GenerateFluidTerrainVertices(
        BlockGeometryGenerator generator,
        int value,
        int x,
        int y,
        int z,
        Color sideColor,
        Color topColor,
        TerrainGeometrySubset[] subset,
        bool extendUnknownSurface = false,
        bool topOnly = false
    )
    {
        var data = Terrain.ExtractData(value);
        if (FluidBlock.GetIsTop(data))
        {
            var cellValueFast = ReadNeighbor(x - 1, z - 1);
            var cellValueFast2 = ReadNeighbor(x, z - 1);
            var cellValueFast3 = ReadNeighbor(x + 1, z - 1);
            var cellValueFast4 = ReadNeighbor(x - 1, z);
            var cellValueFast5 = ReadNeighbor(x + 1, z);
            var cellValueFast6 = ReadNeighbor(x - 1, z + 1);
            var cellValueFast7 = ReadNeighbor(x, z + 1);
            var cellValueFast8 = ReadNeighbor(x + 1, z + 1);
            var h = Block.CalculateNeighborHeight(cellValueFast);
            var negativeZHeight = Block.CalculateNeighborHeight(cellValueFast2);
            var h2 = Block.CalculateNeighborHeight(cellValueFast3);
            var negativeXHeight = Block.CalculateNeighborHeight(cellValueFast4);
            var positiveXHeight = Block.CalculateNeighborHeight(cellValueFast5);
            var h3 = Block.CalculateNeighborHeight(cellValueFast6);
            var positiveZHeight = Block.CalculateNeighborHeight(cellValueFast7);
            var h4 = Block.CalculateNeighborHeight(cellValueFast8);
            var levelHeight = Block.GetLevelHeight(FluidBlock.GetLevel(data));
            var height = FluidBlock.CalculateFluidVertexHeight(h, negativeZHeight, negativeXHeight, levelHeight);
            var height2 = FluidBlock.CalculateFluidVertexHeight(negativeZHeight, h2, levelHeight, positiveXHeight);
            var height3 = FluidBlock.CalculateFluidVertexHeight(levelHeight, positiveXHeight, positiveZHeight, h4);
            var height4 = FluidBlock.CalculateFluidVertexHeight(negativeXHeight, levelHeight, h3, positiveZHeight);
            var x2 = FluidBlock.ZeroSubst(positiveXHeight, levelHeight) -
                     FluidBlock.ZeroSubst(negativeXHeight, levelHeight);
            var x3 = FluidBlock.ZeroSubst(positiveZHeight, levelHeight) -
                     FluidBlock.ZeroSubst(negativeZHeight, levelHeight);
            var overrideTopTextureSlot = TextureSlot - (int)MathUtils.Sign(x2) - 16 * (int)MathUtils.Sign(x3);
            generator.GenerateCubeVertices(
                this,
                value,
                x,
                y,
                z,
                height,
                height2,
                height3,
                height4,
                sideColor,
                topColor,
                topColor,
                topColor,
                topColor,
                overrideTopTextureSlot,
                subset,
                extendUnknownSurface ? value : null,
                topOnly
            );
        }
        else if (!topOnly)
        {
            generator.GenerateCubeVertices(this, value, x, y, z, sideColor, subset);
        }

        int ReadNeighbor(int neighborX, int neighborZ)
        {
            return extendUnknownSurface
                ? generator.ReadFluidNeighbor(neighborX, y, neighborZ, value)
                : generator.Terrain.GetCellValueFast(neighborX, y, neighborZ);
        }
    }
}
