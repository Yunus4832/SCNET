using Game.Modding;
using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Modding;

[Collection(ConfigFileCollection.Name)]
public sealed class WorldRuleHooksTest
{
    [Fact]
    public void ActualHostRegistrationsKeepPriorityAcrossBothWorldsAndUnloadInPlace()
    {
        var calls = new List<string>();
        var host = new ModHost();
        host.LoadAndStart(
            [
                new ModDescriptor(
                    new ModManifest("test.world-hooks", "World hooks", "1.0.0"),
                    () => new ProbeMod(context =>
                        {
                            context.Gameplay.OnWorldUpdating(
                                value => calls.Add($"high-first:{WorldRuntimeContext.For(value.Project).Side}"),
                                10
                            );
                            context.Gameplay.OnWorldUpdating(value =>
                                calls.Add($"low:{WorldRuntimeContext.For(value.Project).Side}")
                            );
                            context.Gameplay.OnWorldUpdating(
                                value => calls.Add($"high-second:{WorldRuntimeContext.For(value.Project).Side}"),
                                10
                            );
                            context.BlockBehaviors.OnBlockAdded(_ => calls.Add("block"));
                        }
                    )
                )
            ]
        );
        using var authority = new RuntimeTestProject(RuntimeSide.Authority, bindRuleHooks: false);
        using var client = new RuntimeTestProject(RuntimeSide.Client, bindRuleHooks: false);
        authority.BindRuleHooks(host.Gameplay, host.BlockBehaviors);
        client.BindRuleHooks(host.Gameplay, host.BlockBehaviors);
        var authorityHooks = WorldRuleHooks.For(authority);
        var clientHooks = WorldRuleHooks.For(client);
        try
        {
            authorityHooks.Invoke(new WorldUpdatingContext(authority, .1f));
            clientHooks.Invoke(new WorldUpdatingContext(client, .1f));
            Assert.Equal(
                new[]
                {
                    "high-first:Authority", "high-second:Authority", "low:Authority",
                    "high-first:Client", "high-second:Client", "low:Client"
                },
                calls
            );
            var terrain = new SubsystemTerrain();
            client.InitializeSubsystem(terrain);
            clientHooks.Invoke(new BlockAddedContext(terrain, 0, 64, 0, 1, 0, null));
            Assert.Equal("block", calls[^1]);
            host.StopAll();
            clientHooks.Invoke(new WorldUpdatingContext(client, .1f));
            clientHooks.Invoke(new BlockAddedContext(terrain, 0, 64, 0, 1, 0, null));
            Assert.Equal(7, calls.Count);
        }
        finally { host.StopAll(); }
    }

    [Fact]
    public void LiveOwnerRejectsForeignContextAndClosedOwnerNeverInvokesTheNextWorld()
    {
        var calls = 0;
        var gameplay = new GameplayHooks();
        var blocks = new BlockBehaviorHooks();
        using var registration = gameplay.ForOwner(new ModId("test.scope"))
            .OnWorldUpdating(_ => calls++);
        gameplay.Freeze();
        blocks.Freeze();
        using var first = new RuntimeTestProject(RuntimeSide.Authority, bindRuleHooks: false);
        using var second = new RuntimeTestProject(RuntimeSide.Authority, bindRuleHooks: false);
        first.BindRuleHooks(gameplay, blocks);
        second.BindRuleHooks(gameplay, blocks);
        var oldOwner = WorldRuleHooks.For(first);
        Assert.Throws<InvalidOperationException>(() => oldOwner.Invoke(new WorldUpdatingContext(second, 1)));
        oldOwner.Invoke(new WorldUpdatingContext(first, 1));
        WorldRuntimeContext.For(first).Close();
        oldOwner.Invoke(new WorldUpdatingContext(second, 1));
        WorldRuleHooks.For(second).Invoke(new WorldUpdatingContext(second, 1));
        Assert.Equal(2, calls);
    }

    [Fact]
    public void MissingEnvironmentIsNotFabricatedAndCannotBeReboundAfterExplicitSetup()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority, bindRuleHooks: false);
        Assert.Throws<InvalidOperationException>(() => WorldRuleHooks.For(project));
        project.BindNoModRules();
        Assert.Throws<ArgumentException>(project.BindNoModRules);
    }

    [Fact]
    public void ClosingInsideAnActualWorldDispatchStopsLaterCallbacks()
    {
        var gameplay = new GameplayHooks();
        var blocks = new BlockBehaviorHooks();
        using var project = new RuntimeTestProject(RuntimeSide.Authority, bindRuleHooks: false);
        var world = WorldRuntimeContext.For(project);
        var later = 0;
        var owner = gameplay.ForOwner(new ModId("test.close"));
        using var first = owner.OnWorldUpdating(_ => world.Close(), 1);
        using var second = owner.OnWorldUpdating(_ => later++);
        gameplay.Freeze();
        blocks.Freeze();
        project.BindRuleHooks(gameplay, blocks);
        var hooks = WorldRuleHooks.For(project);
        hooks.Invoke(new WorldUpdatingContext(project, .1f));
        hooks.Invoke(new WorldUpdatingContext(project, .1f));
        Assert.Equal(0, later);
    }

    [Fact]
    public void AnotherWorldCannotReplaceAnExplicitWorldDispatchEnvironment()
    {
        var calls = 0;
        var ambientCalls = 0;
        var gameplay = new GameplayHooks();
        var blocks = new BlockBehaviorHooks();
        using var registration = gameplay.ForOwner(new ModId("test.bound"))
            .OnWorldUpdating(_ => calls++);
        gameplay.Freeze();
        blocks.Freeze();
        using var project = new RuntimeTestProject(RuntimeSide.Authority, bindRuleHooks: false);
        project.BindRuleHooks(gameplay, blocks);
        using var other = new WorldCatalogScope(
            new ModDescriptor(
                new ModManifest("test.ambient", "Ambient", "1.0.0"),
                () => new ProbeMod(context => context.Gameplay.OnWorldUpdating(_ => ambientCalls++))
            )
        );


        {
            WorldRuleHooks.For(project).Invoke(new WorldUpdatingContext(project, 1));

            WorldRuleHooks.For(project).Invoke(new WorldUpdatingContext(project, 1));
            Assert.Equal(2, calls);
            Assert.Equal(0, ambientCalls);
            WorldRuleHooks.For(other.Project).Invoke(new WorldUpdatingContext(other.Project, 1));
            Assert.Equal(1, ambientCalls);
            Assert.Equal(2, calls);
        }
    }

    private sealed class ProbeMod(Action<IModContext> configure) : IMod
    {
        public void Configure(IModContext context) => configure(context);

        public void Start(IModContext context)
        {
        }

        public void Stop()
        {
        }
    }
}
