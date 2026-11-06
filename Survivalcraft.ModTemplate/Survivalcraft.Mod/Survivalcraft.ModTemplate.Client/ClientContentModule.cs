using Game.ClientContent;
using Game.Modding.Blocks;

namespace Survivalcraft.ModTemplate.Client;

public sealed class ClientContentModule : IClientContentModule
{
    public IEnumerable<BlockPresentationRegistration> BlockPresentations => [];
}
