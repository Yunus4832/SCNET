using Game.ClientContent;
using Game.Modding.Blocks;

namespace TerritoryStoneMod.Client.Presentation;

public sealed class TerritoryClientContentModule : IClientContentModule
{
    public static IReadOnlyList<BlockPresentationRegistration> Registrations { get; } =
        Array.AsReadOnly<BlockPresentationRegistration>(
            [
                new(
                    TerritoryBlock.Id,
                    static (block, catalog) => new TerritoryBlockPresentation((TerritoryBlock)block, catalog)
                )
            ]
        );

    public IEnumerable<BlockPresentationRegistration> BlockPresentations => Registrations;
}
