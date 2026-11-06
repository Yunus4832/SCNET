using EntitySystem.Core;

namespace Game.Modding;

public interface IWorldHookContext
{
    Project Project { get; }
}
