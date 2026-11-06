using Game.Components;
using Game.Modding;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Modding;

public sealed class ContextActionLifetimeTest
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ClosingDuringProviderStopsResolution(bool dispose, bool returnsAction)
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var character = new ComponentCharacter();
        project.InitializeComponent(character);
        var hooks = new PlayerContextActionHooks();
        var owned = hooks.ForOwner(new ModId("test.actions"));
        var later = 0;
        using var first = owned.ProvideNearbyAction(_ =>
            {
                if (dispose) { project.Dispose(); }
                else { WorldRuntimeContext.For(project).Close(); }

                return returnsAction ? new PlayerContextAction("stale", _ => { }) : null;
            }
        );
        using var second = owned.ProvideNearbyAction(_ =>
            {
                later++;
                return null;
            }
        );
        hooks.Freeze();

        Assert.Null(hooks.Resolve(new PlayerContextActionQueryContext(character)));
        Assert.Equal(0, later);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    [InlineData(3, 0)]
    public void CachedActionRequiresItsOriginalActiveWorldAndOwner(int invalidation, int expected)
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        using var otherProject = new RuntimeTestProject(RuntimeSide.Client);
        var character = new ComponentCharacter();
        var other = new ComponentCharacter();
        project.InitializeComponent(character);
        otherProject.InitializeComponent(other);
        var hooks = new PlayerContextActionHooks();
        var owner = new ModId("test.actions");
        var calls = 0;
        using var provider =
            hooks.ForOwner(owner).ProvideNearbyAction(_ => new PlayerContextAction("run", _ => calls++));
        hooks.Freeze();
        var action = hooks.Resolve(new PlayerContextActionQueryContext(character));
        Assert.NotNull(action);
        if (invalidation == 1) { WorldRuntimeContext.For(project).Close(); }

        if (invalidation == 3) { hooks.RemoveOwner(owner); }

        action.Execute(new PlayerContextActionExecutionContext(invalidation == 2 ? other : character));

        Assert.Equal(expected, calls);
    }
}
