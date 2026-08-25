using Game.Modding;
using Game.Network.Packages;
using Game.Network.Serialization;

namespace Survivalcraft.Test.Modding;

public class ConnectionRequestPackageTest
{
    [Fact]
    public void PackageRoundTripsModDataHash()
    {
        var package = new ConnectionRequestPackage(
            Guid.NewGuid(),
            "1.0.0",
            "user",
            "token",
            "pwd",
            "mod-data-hash");

        var writer = new PackageStreamWriter();
        package.WriteData(writer);
        var reader = new PackageStreamReader(writer.Data());
        var clone = new ConnectionRequestPackage();
        clone.ReadData(reader);

        Assert.Equal(package.ModDataHash, clone.ModDataHash);
        Assert.Equal(package.Version, clone.Version);
        Assert.Equal(package.Password, clone.Password);
    }

    [Fact]
    public void RejectPackageRoundTripsReasonOnly()
    {
        var package = new ConnectionRejectPackage("客户端模组与服务器不一致，请刷新服务器信息后重试");

        var writer = new PackageStreamWriter();
        package.WriteData(writer);
        var reader = new PackageStreamReader(writer.Data());
        var clone = new ConnectionRejectPackage();
        clone.ReadData(reader);

        Assert.Equal(package.Reason, clone.Reason);
    }

    [Fact]
    public void ModDataHashUsesStableKeyInformation()
    {
        var left = ModProfileManager.ComputeDataHash(new ModProfile
        {
            Packages =
            [
                new ModPackageRequirement { ModId = "example.addon", Version = "2.0.0", PackageHash = "package-hash" },
                new ModPackageRequirement { ModId = "other.addon", Version = "1.0.0", PackageHash = "other-hash" }
            ]
        });
        var right = ModProfileManager.ComputeDataHash(new ModProfile
        {
            Packages =
            [
                new ModPackageRequirement { ModId = "other.addon", Version = "1.0.0", PackageHash = "other-hash" },
                new ModPackageRequirement { ModId = "example.addon", Version = "2.0.0", PackageHash = "package-hash" }
            ]
        });
        var changed = ModProfileManager.ComputeDataHash(new ModProfile
        {
            Packages =
            [
                new ModPackageRequirement { ModId = "example.addon", Version = "2.0.0", PackageHash = "changed-package-hash" },
                new ModPackageRequirement { ModId = "other.addon", Version = "1.0.0", PackageHash = "other-hash" }
            ]
        });

        Assert.Equal(left, right);
        Assert.NotEqual(left, changed);
    }

    [Fact]
    public void ServerInfoPackageRoundTripsRequiredModProfile()
    {
        var package = new ServerInfoPackage
        {
            RequestInfo = false,
            Version = "1.0.0",
            ModRepositoryUrl = "http://127.0.0.1:9527",
            RequiredModProfile = new ModProfile
            {
                Id = "server",
                RepositoryUrl = "http://127.0.0.1:9527",
                Packages =
                [
                    new ModPackageRequirement
                    {
                        ModId = "example.addon",
                        Version = "2.0.0",
                        PackageHash = "hash"
                    }
                ]
            }
        };

        var writer = new PackageStreamWriter();
        package.WriteData(writer);
        var reader = new PackageStreamReader(writer.Data());
        var clone = new ServerInfoPackage();
        clone.ReadData(reader);

        Assert.Equal(package.ModRepositoryUrl, clone.ModRepositoryUrl);
        Assert.NotNull(clone.RequiredModProfile);
        Assert.Equal("http://127.0.0.1:9527", clone.RequiredModProfile.RepositoryUrl);
        var requirement = Assert.Single(clone.RequiredModProfile.Packages);
        Assert.Equal("example.addon", requirement.ModId);
        Assert.Equal("2.0.0", requirement.Version);
        Assert.Equal("hash", requirement.PackageHash);
    }
}
