using Game.Modding;

namespace Survivalcraft.Test.Modding;

public sealed class ModProfileValidationTest
{
    private const string _hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public void AcceptsCanonicalExactRequirements()
    {
        var profile = new ModProfile
        {
            Id = "profile",
            Packages =
            [
                new ModPackageRequirement { ModId = "example.mod", Version = "1.0.0", PackageHash = _hash }
            ]
        };

        ModProfileValidation.Validate(profile);
    }

    [Theory]
    [InlineData("", "1.0.0", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("Example.Mod", "1.0.0", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("example.mod", "1.0", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("example.mod", "1.0.0", "")]
    [InlineData("example.mod", "1.0.0", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void RejectsNoncanonicalRequirements(string modId, string version, string hash)
    {
        var requirement = new ModPackageRequirement { ModId = modId, Version = version, PackageHash = hash };

        Assert.Throws<ArgumentException>(() => ModProfileValidation.Validate(requirement));
    }

    [Fact]
    public void RejectsDuplicateModRequirements()
    {
        var profile = new ModProfile
        {
            Id = "profile",
            Packages =
            [
                new ModPackageRequirement { ModId = "example.mod", Version = "1.0.0", PackageHash = _hash },
                new ModPackageRequirement { ModId = "example.mod", Version = "2.0.0", PackageHash = _hash }
            ]
        };

        Assert.Throws<ArgumentException>(() => ModProfileValidation.Validate(profile));
    }
}
