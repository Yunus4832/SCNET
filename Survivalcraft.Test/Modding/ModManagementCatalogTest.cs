using Game.Modding;

namespace Survivalcraft.Test.Modding;

public sealed class ModManagementCatalogTest
{
    [Fact]
    public void BuildsExactUnionOfCacheAndProfileScopes()
    {
        var hashA = new string('a', 64);
        var hashB = new string('b', 64);
        var cached = new LocalModPackageEntry("/cache/a.scpkg", "a.scpkg", "example.mod", "2.0.0", hashA);
        var global = Profile(Requirement("example.mod", "1.0.0", hashB));
        var world = Profile(Requirement("example.mod", "2.0.0", hashA));
        var runtime = Profile(Requirement("example.mod", "1.0.0", hashB));

        var result = ModManagementCatalog.Build([cached], global, [world], runtime);

        Assert.Equal(2, result.Count);
        var local = result.Single(item => item.PackageHash == hashA);
        Assert.NotNull(local.LocalEntry);
        Assert.True(local.IsWorld);
        Assert.False(local.IsGlobal);
        var missing = result.Single(item => item.PackageHash == hashB);
        Assert.True(missing.IsMissing);
        Assert.True(missing.IsGlobal);
        Assert.True(missing.IsRuntime);
    }

    [Fact]
    public void KeepsSameVersionDifferentHashesAsSeparateItems()
    {
        var hashA = new string('a', 64);
        var hashB = new string('b', 64);
        var global = Profile(Requirement("example.mod", "1.0.0", hashA));
        var world = Profile(Requirement("example.mod", "1.0.0", hashB));

        var result = ModManagementCatalog.Build([], global, [world], null);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, item => item.PackageHash == hashA && item.IsGlobal && !item.IsWorld);
        Assert.Contains(result, item => item.PackageHash == hashB && !item.IsGlobal && item.IsWorld);
    }

    [Fact]
    public void ContainsExactRequiresVersionAndHash()
    {
        var hashA = new string('a', 64);
        var hashB = new string('b', 64);
        var profile = Profile(Requirement("example.mod", "1.0.0", hashA));
        var differentHash = new ManagedModItem("example.mod", "1.0.0", hashB, null,
            false, false, false);

        Assert.False(ModManagementCatalog.ContainsExact(profile, differentHash));
    }

    private static ModProfile Profile(params ModPackageRequirement[] requirements)
    {
        return new ModProfile { Packages = requirements.ToList() };
    }

    private static ModPackageRequirement Requirement(string id, string version, string hash)
    {
        return new ModPackageRequirement { ModId = id, Version = version, PackageHash = hash };
    }
}
