using System.Reflection;

using Engine.Core.Mathematics;

using Game.Blocks;
using Game.Network.Handling;
using Game.Network.Transport;
using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Fixtures;

using EngineRandom = Engine.Core.Mathematics.Random;

namespace Survivalcraft.Test.Subsystems;

public sealed class SubsystemPickablesPresentationTest
{
    [Fact]
    public void EffectListenersDoNotChangeTheExistingRandomPitchAndCreationVelocitySequence()
    {
        var silent = new SubsystemPickables();
        var observed = new SubsystemPickables();
        RandomOf(silent).Seed(12345);
        RandomOf(observed).Seed(12345);
        var effects = new List<PickableEffect>();
        observed.EffectRequested += effects.Add;
        var first = new Pickable { SplashGenerated = false, Position = new Vector3(1, 2, 3) };
        var second = new Pickable { SplashGenerated = false, Position = first.Position };
        using var water = new WaterBlock();
        Assert.True(FluidContact(silent, first, water));
        Assert.True(FluidContact(observed, second, water));
        var expected = new EngineRandom(12345);
        var pitch = expected.Float(-0.2f, 0.2f);
        var velocity = new Vector3(expected.Float(-0.5f, 0.5f), expected.Float(1f, 1.2f), expected.Float(-0.5f, 0.5f));
        Assert.Equal(new PickableEffect(PickableEffectKind.WaterEntry, first.Position, pitch), Assert.Single(effects));
        var unobservedItem = silent.CreatePickable(1, 1, 1, Vector3.Zero, null, null)!;
        var observedItem = observed.CreatePickable(1, 1, 1, Vector3.Zero, null, null)!;
        Assert.Equal(velocity, unobservedItem.Velocity);
        Assert.Equal(unobservedItem.Velocity, observedItem.Velocity);
        Assert.Equal(expected.State, RandomOf(silent).State);
        Assert.Equal(RandomOf(silent).State, RandomOf(observed).State);
    }

    [Fact]
    public void FluidEntryKeepsItsInitialLatchResetAndOneEntrySemantics()
    {
        var rules = new SubsystemPickables();
        var pickable = new Pickable();
        var effects = new List<PickableEffect>();
        rules.EffectRequested += effects.Add;
        using var magma = new MagmaBlock();
        Assert.False(FluidContact(rules, pickable, magma));
        Assert.False(FluidContact(rules, pickable, null));
        Assert.True(FluidContact(rules, pickable, magma));
        Assert.False(FluidContact(rules, pickable, magma));
        Assert.Equal(PickableEffectKind.MagmaEntry, Assert.Single(effects).Kind);
        Assert.True(pickable.SplashGenerated);
    }

    [Fact]
    public void RepeatedRemovalPackagesEmitTheCollectionFactOnlyOnce()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var rules = new SubsystemPickables();
        project.AddSubsystem(rules);
        var item = rules.CreatePickable(7, 1, 1, new Vector3(1, 2, 3), Vector3.Zero, null)!;
        var effects = new List<PickableEffect>();
        rules.EffectRequested += effects.Add;
        var runtime = WorldRuntimeContext.For(project);
        var context = new PackageReceiveContext(
            Game.Network.WorldNetworkBinding.For(runtime).RequireNode(),
            runtime,
            null
        );
        var removed = new PickableRemovedPackage { Id = item.Id, PlaySound = true };
        var handler = new PickablePackageHandler();
        handler.Handle(removed, context);
        handler.Handle(removed, context);
        Assert.Equal(new PickableEffect(PickableEffectKind.Collected, item.Position, -0.4f), Assert.Single(effects));
        Assert.Empty(rules.Pickables);
        Assert.Equal(0, Game.Network.WorldNetworkBinding.For(runtime).RequireNode().PendingPackageCount);
    }

    [Fact]
    public void LightingCacheTracksRealObjectLifetimeAndDisposeUnsubscribesOnce()
    {
        var rules = new SubsystemPickables();
        var item = rules.CreatePickable(7, 1, 1, Vector3.Zero, Vector3.Zero, null)!;
        var presentation = new SubsystemPickablesPresentation();
        typeof(SubsystemPickablesPresentation).GetField("_pickables", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(presentation, rules);
        var subscribe = typeof(SubsystemPickablesPresentation).GetMethod(
            "Subscribe",
            BindingFlags.Instance | BindingFlags.NonPublic
        )!;
        subscribe.Invoke(presentation, null);
        subscribe.Invoke(presentation, null);
        Assert.Single(Event(rules, "PickableRemoved")!.GetInvocationList());
        Assert.Single(Event(rules, "EffectRequested")!.GetInvocationList());
        var cache = (Dictionary<Pickable, int>)typeof(SubsystemPickablesPresentation)
            .GetField("_lightSamples", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(presentation)!;
        cache.Add(item, 12);
        item.Light = 3;
        Assert.True(rules.RemovePickable(item));
        Assert.Empty(cache);
        var replacement = rules.CreatePickable(7, 1, 1, Vector3.Zero, Vector3.Zero, null)!;
        Assert.NotSame(item, replacement);
        Assert.False(cache.ContainsKey(replacement));
        Assert.Equal(3, item.Light);
        presentation.Dispose();
        presentation.Dispose();
        presentation.Draw(null!, 10);
        Assert.Null(Event(rules, "PickableRemoved"));
        Assert.Null(Event(rules, "EffectRequested"));
        rules.NotifyPickableCollected(replacement);
        Assert.True(rules.RemovePickable(replacement));
    }

    private static Delegate? Event(SubsystemPickables rules, string name)
    {
        return (Delegate?)typeof(SubsystemPickables).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(rules);
    }

    private static EngineRandom RandomOf(SubsystemPickables rules)
    {
        return (EngineRandom)typeof(SubsystemPickables).GetField(
                "_random",
                BindingFlags.Instance | BindingFlags.NonPublic
            )!
            .GetValue(rules)!;
    }

    private static bool FluidContact(SubsystemPickables rules, Pickable item, FluidBlock? fluid)
    {
        return (bool)typeof(SubsystemPickables).GetMethod(
                "UpdateFluidContact",
                BindingFlags.Instance | BindingFlags.NonPublic
            )!
            .Invoke(rules, [item, fluid])!;
    }
}
