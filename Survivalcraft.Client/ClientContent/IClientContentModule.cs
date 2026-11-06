using Game.Modding.Blocks;

namespace Game.ClientContent;

/// <summary>Client-only block factories supplied by an already loaded content module.</summary>
public interface IClientContentModule
{
    IEnumerable<BlockPresentationRegistration> BlockPresentations { get; }
}
