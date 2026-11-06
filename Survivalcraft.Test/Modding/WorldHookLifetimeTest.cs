using Game.Components;
using Game.Modding;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Modding;

public sealed class WorldHookLifetimeTest
{
    [Theory]
    [InlineData(RuntimeSide.Authority, false, false)]
    [InlineData(RuntimeSide.Client, false, false)]
    [InlineData(RuntimeSide.Authority, true, false)]
    [InlineData(RuntimeSide.Client, true, false)]
    [InlineData(RuntimeSide.Authority, false, true)]
    [InlineData(RuntimeSide.Client, false, true)]
    [InlineData(RuntimeSide.Authority, true, true)]
    [InlineData(RuntimeSide.Client, true, true)]
    public void ClosingAWorldStopsLaterGameplayAndBlockCallbacks(RuntimeSide side, bool block, bool dispose)
    {
        using var project = new RuntimeTestProject(side);
        var runtime = WorldRuntimeContext.For(project);
        var owner = new ModId("test.hook-close");
        var calls = 0;
        var firstCalls = 0;

        void Close()
        {
            firstCalls++;
            if (dispose) { project.Dispose(); }
            else { runtime.Close(); }
        }

        if (block)
        {
            var hooks = new BlockBehaviorHooks();
            var owned = hooks.ForOwner(owner);
            using var first = owned.OnEditBlock(_ => Close());
            using var second = owned.OnEditBlock(_ => calls++);
            hooks.Freeze();
            var character = new ComponentCharacter();
            project.InitializeComponent(character);
            hooks.Invoke(new BlockEditContext(1, 65, 2, 0, character));
            hooks.Invoke(new BlockEditContext(1, 65, 2, 0, character));
        }
        else
        {
            var hooks = new GameplayHooks();
            var owned = hooks.ForOwner(owner);
            using var first = owned.OnWorldUpdating(_ => Close());
            using var second = owned.OnWorldUpdating(_ => calls++);
            hooks.Freeze();
            hooks.Invoke(new WorldUpdatingContext(project, .1f));
            hooks.Invoke(new WorldUpdatingContext(project, .1f));
        }

        Assert.Equal(1, firstCalls);
        Assert.Equal(0, calls);
    }
}
