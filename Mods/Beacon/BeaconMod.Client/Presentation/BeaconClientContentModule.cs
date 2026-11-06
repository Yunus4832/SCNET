using Game.ClientContent;
using Game.Modding.Blocks;

namespace BeaconMod.Client.Presentation;

public sealed class BeaconClientContentModule : IClientContentModule
{
    public static IReadOnlyList<BlockPresentationRegistration> Registrations { get; } =
        Array.AsReadOnly<BlockPresentationRegistration>(
            [new(BeaconBlock.Id, static (block, catalog) => new BeaconBlockPresentation((BeaconBlock)block, catalog))]
        );

    public IEnumerable<BlockPresentationRegistration> BlockPresentations => Registrations;
}
