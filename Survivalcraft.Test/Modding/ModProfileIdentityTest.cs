using Game.Modding;

namespace Survivalcraft.Test.Modding;

public sealed class ModProfileIdentityTest
{
    [Fact]
    public void EquivalentProfilesIgnoreOrderAndModIdCase()
    {
        var hashA = new string('a', 64);
        var hashB = new string('b', 64);
        var first = Profile(
            Requirement("example.a", "1.0.0", hashA),
            Requirement("example.b", "2.0.0", hashB));
        var second = Profile(
            Requirement("EXAMPLE.B", "2.0.0", hashB),
            Requirement("EXAMPLE.A", "1.0.0", hashA));

        Assert.True(ModProfileIdentity.AreEquivalent(first, second));
    }

    [Fact]
    public void SameModAndVersionWithDifferentHashIsNotEquivalent()
    {
        var first = Profile(Requirement("example.mod", "1.0.0", new string('a', 64)));
        var second = Profile(Requirement("example.mod", "1.0.0", new string('b', 64)));

        Assert.False(ModProfileIdentity.AreEquivalent(first, second));
    }

    [Fact]
    public void NullAndEmptyProfilesAreEquivalent()
    {
        Assert.True(ModProfileIdentity.AreEquivalent(null, new ModProfile()));
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
