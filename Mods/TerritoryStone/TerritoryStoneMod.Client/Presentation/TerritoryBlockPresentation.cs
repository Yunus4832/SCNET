using Game.Blocks.Presentation;
using Game.Modding.Blocks;

namespace TerritoryStoneMod.Client.Presentation;

public sealed class TerritoryBlockPresentation(TerritoryBlock block, BlockPresentationCatalog catalog)
    : CubeBlockPresentation(block, catalog)
{
    public override int GetFaceTextureSlot(int face, int value) => 255;
}
