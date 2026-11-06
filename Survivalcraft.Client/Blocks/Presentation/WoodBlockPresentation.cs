using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public abstract class WoodBlockPresentation(
    WoodBlock block,
    BlockPresentationCatalog catalog,
    int cutTextureSlot,
    int sideTextureSlot
) : CubeBlockPresentation(block, catalog)
{
    protected new WoodBlock Block => (WoodBlock)base.Block;

    public override void GenerateTerrainVertices(
        BlockGeometryGenerator generator,
        TerrainGeometry geometry,
        int value,
        int x,
        int y,
        int z
    )
    {
        switch (WoodBlock.GetCutFace(Terrain.ExtractData(value)))
        {
            case 4:
                generator.GenerateCubeVertices(this, value, x, y, z, Color.White, geometry.OpaqueSubsetsByFace);
                break;
            case 0:
                generator.GenerateCubeVertices(
                    this,
                    value,
                    x,
                    y,
                    z,
                    1,
                    0,
                    0,
                    Color.White,
                    geometry.OpaqueSubsetsByFace
                );
                break;
            default:
                generator.GenerateCubeVertices(
                    this,
                    value,
                    x,
                    y,
                    z,
                    0,
                    1,
                    1,
                    Color.White,
                    geometry.OpaqueSubsetsByFace
                );
                break;
        }
    }

    public override int GetFaceTextureSlot(int face, int value)
    {
        var cutFace = WoodBlock.GetCutFace(Terrain.ExtractData(value));
        if (cutFace == face || CellFace.OppositeFace(cutFace) == face)
        {
            return CutTextureSlot;
        }

        return SideTextureSlot;
    }

    public int CutTextureSlot = cutTextureSlot;


    public int SideTextureSlot = sideTextureSlot;
}
