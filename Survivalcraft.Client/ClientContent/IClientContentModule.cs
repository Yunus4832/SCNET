using Game.Modding.Blocks;

namespace Game.ClientContent;

/// <summary>由已加载的内容模块提供的客户端专用方块工厂。</summary>
public interface IClientContentModule
{
    IEnumerable<BlockPresentationRegistration> BlockPresentations { get; }
}
