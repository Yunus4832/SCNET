using Game.Modding.Blocks;

namespace Game.ClientContent;

public sealed class BuiltInClientContentModule : IClientContentModule
{
    public IEnumerable<BlockPresentationRegistration> BlockPresentations =>
        BuiltInBlockPresentationRegistry.Registrations;
}
