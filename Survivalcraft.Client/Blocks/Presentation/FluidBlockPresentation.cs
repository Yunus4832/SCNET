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
        TerrainGeometrySubset[] subset
    )
    {
        var data = Terrain.ExtractData(value);
        if (FluidBlock.GetIsTop(data))
        {
            var terrain = generator.Terrain;
            var cellValueFast = terrain.GetCellValueFast(x - 1, y, z - 1);
            var cellValueFast2 = terrain.GetCellValueFast(x, y, z - 1);
            var cellValueFast3 = terrain.GetCellValueFast(x + 1, y, z - 1);
            var cellValueFast4 = terrain.GetCellValueFast(x - 1, y, z);
            var cellValueFast5 = terrain.GetCellValueFast(x + 1, y, z);
            var cellValueFast6 = terrain.GetCellValueFast(x - 1, y, z + 1);
            var cellValueFast7 = terrain.GetCellValueFast(x, y, z + 1);
            var cellValueFast8 = terrain.GetCellValueFast(x + 1, y, z + 1);
            var h = Block.CalculateNeighborHeight(cellValueFast);
            var num = Block.CalculateNeighborHeight(cellValueFast2);
            var h2 = Block.CalculateNeighborHeight(cellValueFast3);
            var num2 = Block.CalculateNeighborHeight(cellValueFast4);
            var num3 = Block.CalculateNeighborHeight(cellValueFast5);
            var h3 = Block.CalculateNeighborHeight(cellValueFast6);
            var num4 = Block.CalculateNeighborHeight(cellValueFast7);
            var h4 = Block.CalculateNeighborHeight(cellValueFast8);
            var levelHeight = Block.GetLevelHeight(FluidBlock.GetLevel(data));
            var height = FluidBlock.CalculateFluidVertexHeight(h, num, num2, levelHeight);
            var height2 = FluidBlock.CalculateFluidVertexHeight(num, h2, levelHeight, num3);
            var height3 = FluidBlock.CalculateFluidVertexHeight(levelHeight, num3, num4, h4);
            var height4 = FluidBlock.CalculateFluidVertexHeight(num2, levelHeight, h3, num4);
            var x2 = FluidBlock.ZeroSubst(num3, levelHeight) - FluidBlock.ZeroSubst(num2, levelHeight);
            var x3 = FluidBlock.ZeroSubst(num4, levelHeight) - FluidBlock.ZeroSubst(num, levelHeight);
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
                subset
            );
        }
        else
        {
            generator.GenerateCubeVertices(this, value, x, y, z, sideColor, subset);
        }
    }
}
