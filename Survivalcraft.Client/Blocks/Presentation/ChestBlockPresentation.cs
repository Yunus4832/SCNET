using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class ChestBlockPresentation(ChestBlock block, BlockPresentationCatalog catalog)
    : CubeBlockPresentation(block, catalog)
{
    protected new ChestBlock Block => (ChestBlock)base.Block;

    public override int GetFaceTextureSlot(int face, int value)
    {
        return face switch
        {
            4 or 5 => 42,
            _ => Terrain.ExtractData(value) switch
            {
                0 => face switch
                {
                    0 => 27,
                    2 => 26,
                    _ => 25
                },
                1 => face switch
                {
                    1 => 27,
                    3 => 26,
                    _ => 25
                },
                2 => face switch
                {
                    2 => 27,
                    0 => 26,
                    _ => 25
                },
                _ => face switch
                {
                    3 => 27,
                    1 => 26,
                    _ => 25
                }
            }
        };
    }
}
