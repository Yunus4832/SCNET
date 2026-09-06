using Game.Modding;

namespace Survivalcraft.Test.Modding;

public sealed class ModPackageReferenceTrackerTest
{
    private const string _hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private static readonly LocalModPackageEntry _entry = new(
        "cache.scpkg", "cache.scpkg", "example.mod", "1.0.0", _hash);

    [Fact]
    public void ExactProfileRequirementProtectsPackage()
    {
        var profile = new ModProfile
        {
            Packages =
            [
                new ModPackageRequirement
                {
                    ModId = "example.mod",
                    Version = "1.0.0",
                    PackageHash = _hash
                }
            ]
        };

        Assert.True(ModPackageReferenceTracker.IsReferenced(_entry, [profile]));
    }

    [Fact]
    public void DifferentHashDoesNotProtectPackage()
    {
        var profile = new ModProfile
        {
            Packages =
            [
                new ModPackageRequirement
                {
                    ModId = "example.mod",
                    Version = "1.0.0",
                    PackageHash = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
                }
            ]
        };

        Assert.False(ModPackageReferenceTracker.IsReferenced(_entry, [profile]));
    }
}
