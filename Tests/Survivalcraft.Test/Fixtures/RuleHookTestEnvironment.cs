using EntitySystem.Core;

using Game.Modding;
using Game.World;

namespace Survivalcraft.Test.Fixtures;

internal static class RuleHookTestEnvironment
{
    public static void BindEmpty(Project project)
    {
        var gameplay = new GameplayHooks();
        var blocks = new BlockBehaviorHooks();
        gameplay.Freeze();
        blocks.Freeze();
        _ = new WorldRuleHooks(WorldRuntimeContext.For(project), gameplay, blocks);
    }
}
