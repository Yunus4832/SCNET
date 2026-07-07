using Game.Modding;

namespace Survivalcraft.Test.Modding;

public class ModDependencyResolverTest
{
    [Fact]
    public void ResolveOrdersDependenciesBeforeDependents()
    {
        var core = Descriptor("example.core");
        var addon = Descriptor("example.addon", new ModDependency("example.core", "1.0"));

        var result = ModDependencyResolver.Resolve([addon, core]);

        Assert.Equal(["example.core", "example.addon"], result.Select(item => item.Manifest.Id));
    }

    [Fact]
    public void ResolveRejectsMissingDependency()
    {
        var addon = Descriptor("example.addon", new ModDependency("example.core"));

        var exception = Assert.Throws<ModDependencyException>(() => ModDependencyResolver.Resolve([addon]));

        Assert.Contains("missing mod example.core", exception.Message);
    }

    [Fact]
    public void ResolveRejectsCircularDependency()
    {
        var first = Descriptor("example.first", new ModDependency("example.second"));
        var second = Descriptor("example.second", new ModDependency("example.first"));

        var exception = Assert.Throws<ModDependencyException>(() =>
            ModDependencyResolver.Resolve([first, second]));

        Assert.Contains("Circular mod dependency", exception.Message);
    }

    private static ModDescriptor Descriptor(string id, params ModDependency[] dependencies)
    {
        return new ModDescriptor(new ModManifest(id, id, "1.0", dependencies), () => new EmptyMod());
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
