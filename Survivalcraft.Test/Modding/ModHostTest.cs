using Game.Modding;

namespace Survivalcraft.Test.Modding;

public class ModHostTest
{
    [Fact]
    public void HostConfiguresStartsAndStopsInDependencyOrder()
    {
        var calls = new List<string>();
        var host = new ModHost();
        var core = Descriptor("example.core", calls);
        var addon = Descriptor("example.addon", calls, new ModDependency("example.core"));

        host.LoadAndStart([addon, core]);
        host.StopAll();

        Assert.Equal(
            [
                "configure:example.core",
                "configure:example.addon",
                "start:example.core",
                "start:example.addon",
                "stop:example.addon",
                "stop:example.core"
            ],
            calls);
    }

    [Fact]
    public void HostOwnsAndRemovesModRegistrations()
    {
        var host = new ModHost();
        var descriptor = new ModDescriptor(
            new ModManifest("example.content", "Content", "1.0"),
            () => new RegisteringMod());

        host.LoadAndStart([descriptor]);
        var registry = host.Extensions.GetRegistry<object>("blocks");
        var id = new ResourceId(new ModId("example.content"), "machine");

        Assert.True(registry.IsFrozen);
        Assert.True(registry.TryGet(id, out _));
        Assert.Throws<InvalidOperationException>(() => host.Extensions.GetRegistry<object>("late-registry"));

        host.StopAll();

        Assert.False(registry.TryGet(id, out _));
    }

    [Fact]
    public void HostRollsBackStartedModsWhenStartFails()
    {
        var calls = new List<string>();
        var host = new ModHost();
        var first = Descriptor("example.first", calls);
        var failing = new ModDescriptor(
            new ModManifest("example.second", "Second", "1.0", [new ModDependency("example.first")]),
            () => new FailingMod(calls));

        Assert.Throws<InvalidOperationException>(() => host.LoadAndStart([failing, first]));

        Assert.Contains("stop:example.first", calls);
        Assert.Equal(ModState.Stopped, host.Runtimes[0].State);
        Assert.Equal(ModState.Failed, host.Runtimes[1].State);
    }

    [Fact]
    public void GameplayHooksRunByPriorityAndAreRemovedWithOwner()
    {
        var calls = new List<string>();
        var host = new ModHost();
        var descriptor = new ModDescriptor(
            new ModManifest("example.hooks", "Hooks", "1.0"),
            () => new HookMod(calls));

        host.LoadAndStart([descriptor]);
        var context = new CreatureInjuringContext(new Game.Components.ComponentHealth(), 4f, null, false, "test");

        host.Gameplay.Invoke(context);

        Assert.Equal(["high", "normal"], calls);
        Assert.Equal(1f, context.Amount);

        host.StopAll();
        calls.Clear();
        host.Gameplay.Invoke(new CreatureInjuringContext(new Game.Components.ComponentHealth(), 4f, null, false, "test"));
        Assert.Empty(calls);
    }

    [Fact]
    public void TerrainChunkHooksRunByPriorityAndAreRemovedWithOwner()
    {
        var calls = new List<string>();
        var host = new ModHost();
        var descriptor = new ModDescriptor(
            new ModManifest("example.terrain", "Terrain", "1.0"),
            () => new TerrainHookMod(calls));

        host.LoadAndStart([descriptor]);
        var context = new TerrainChunkGeneratedContext(null!, new Game.Terrains.TerrainChunk(null!, 0, 0));

        host.Gameplay.Invoke(context);

        Assert.Equal(["high", "normal"], calls);
        host.StopAll();
        calls.Clear();
        host.Gameplay.Invoke(new TerrainChunkGeneratedContext(null!, new Game.Terrains.TerrainChunk(null!, 0, 0)));
        Assert.Empty(calls);
    }

    [Fact]
    public void BlockBehaviorHooksRunByPriorityAndAreRemovedWithOwner()
    {
        var calls = new List<string>();
        var host = new ModHost();
        var descriptor = new ModDescriptor(
            new ModManifest("example.blocks", "Blocks", "1.0"),
            () => new BlockBehaviorHookMod(calls));

        host.LoadAndStart([descriptor]);
        var context = new BlockEditContext(1, 2, 3, 4, new Game.Components.ComponentPlayer());

        host.BlockBehaviors.Invoke(context);

        Assert.Equal(["high", "normal"], calls);
        host.StopAll();
        calls.Clear();
        host.BlockBehaviors.Invoke(new BlockEditContext(1, 2, 3, 4, new Game.Components.ComponentPlayer()));
        Assert.Empty(calls);
    }

    private static ModDescriptor Descriptor(
        string id,
        List<string> calls,
        params ModDependency[] dependencies)
    {
        var manifest = new ModManifest(id, id, "1.0", dependencies);
        return new ModDescriptor(manifest, () => new RecordingMod(manifest, calls));
    }

    private sealed class RecordingMod(ModManifest manifest, List<string> calls) : IMod
    {
        public void Configure(IModContext context) => calls.Add($"configure:{manifest.Id}");

        public void Start(IModContext context) => calls.Add($"start:{manifest.Id}");

        public void Stop() => calls.Add($"stop:{manifest.Id}");
    }

    private sealed class FailingMod(List<string> calls) : IMod
    {
        public void Configure(IModContext context) => calls.Add("configure:example.second");

        public void Start(IModContext context) => throw new InvalidOperationException("Start failed.");

        public void Stop() => calls.Add("stop:example.second");
    }

    private sealed class RegisteringMod : IMod
    {
        public void Configure(IModContext context)
        {
            var id = new ResourceId(context.Manifest.ModId, "machine");
            context.Extensions.Register("blocks", id, new object());
        }

        public void Start(IModContext context)
        {
        }

        public void Stop()
        {
        }
    }

    private sealed class HookMod(List<string> calls) : IMod
    {
        public void Configure(IModContext context)
        {
            context.Gameplay.OnCreatureInjuring(injury =>
            {
                calls.Add("normal");
                injury.Amount *= 0.5f;
            });
            context.Gameplay.OnCreatureInjuring(injury =>
            {
                calls.Add("high");
                injury.Amount *= 0.5f;
            }, 100);
        }

        public void Start(IModContext context)
        {
        }

        public void Stop()
        {
        }
    }

    private sealed class TerrainHookMod(List<string> calls) : IMod
    {
        public void Configure(IModContext context)
        {
            context.Gameplay.OnTerrainChunkGenerated(generated =>
            {
                calls.Add("normal");
            });
            context.Gameplay.OnTerrainChunkGenerated(generated =>
            {
                calls.Add("high");
            }, 100);
        }

        public void Start(IModContext context)
        {
        }

        public void Stop()
        {
        }
    }

    private sealed class BlockBehaviorHookMod(List<string> calls) : IMod
    {
        public void Configure(IModContext context)
        {
            context.BlockBehaviors.OnEditBlock(edit =>
            {
                calls.Add("normal");
            });
            context.BlockBehaviors.OnEditBlock(edit =>
            {
                calls.Add("high");
            }, 100);
        }

        public void Start(IModContext context)
        {
        }

        public void Stop()
        {
        }
    }
}
