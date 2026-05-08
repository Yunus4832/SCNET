namespace Game.Blocks;

public class MagmaBlock() : FluidBlock(MagmaMaxLevel)
{
    public const int Index = 92;

    public const int MagmaMaxLevel = 4;

    public override bool FurnitureBuilt { get; set; } = true;

    public override bool IsFaceTransparent(SubsystemTerrain subsystemTerrain, int face, int value)
    {
        if (GetIsTop(Terrain.ExtractData(value)))
        {
            return face != 5;
        }

        return false;
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
        GenerateFluidTerrainVertices(
            generator,
            value,
            x,
            y,
            z,
            Color.White,
            Color.White,
            geometry.OpaqueSubsetsByFace
        );
    }

    public override bool ShouldAvoid(int value)
    {
        return true;
    }
}
