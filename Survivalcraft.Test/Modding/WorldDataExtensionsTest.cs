using EntitySystem.Core;

using Game.Modding;

namespace Survivalcraft.Test.Modding;

public class WorldDataExtensionsTest
{
    [Fact]
    public void ModWorldDataUsesItsOwnerAndDoesNotFollowTheModAcrossWorlds()
    {
        var host = new ModHost();
        host.LoadAndStart([
            new ModDescriptor(new ModManifest("example.first", "First", "1.0.0"), () => new EmptyMod()),
            new ModDescriptor(new ModManifest("example.second", "Second", "1.0.0"), () => new EmptyMod())
        ]);
        try
        {
            using var world = new Project();
            using var otherWorld = new Project();
            var first = host.Runtimes.Single(runtime => runtime.Context.Manifest.Id == "example.first").Context;
            var second = host.Runtimes.Single(runtime => runtime.Context.Manifest.Id == "example.second").Context;
            first.GetWorldData(world, "counter").SetValue("value", 1);
            second.GetWorldData(world, "counter").SetValue("value", 2);

            Assert.Equal(1, world.ExtensionData.Get("example.first", "counter").GetValue<int>("value"));
            Assert.Equal(2, world.ExtensionData.Get("example.second", "counter").GetValue<int>("value"));
            Assert.Empty(first.GetWorldData(otherWorld, "counter"));
        }
        finally
        {
            host.StopAll();
        }
    }

    private sealed class EmptyMod : IMod
    {
        public void Configure(IModContext context)
        {
        }

        public void Start(IModContext context)
        {
        }

        public void Stop()
        {
        }
    }
}
