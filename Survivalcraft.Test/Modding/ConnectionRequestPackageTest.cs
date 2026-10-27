using Game.Modding;

namespace Survivalcraft.Test.Modding;

public class ConnectionRequestPackageTest
{
    private const string _hashA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private const string _hashB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    private const string _hashC = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";

    [Fact]
    public void PackageRoundTripsModDataHash()
    {
        var package = new ConnectionRequestPackage(
            Guid.NewGuid(),
            1,
            "mod-data-hash"
        );

        var writer = ProtocolMessageCodec.Encode(package);
        var reader = writer;
        var clone = (ConnectionRequestPackage)ProtocolMessageCodec.Decode(reader);

        Assert.Equal(package.ModDataHash, clone.ModDataHash);
        Assert.Equal(package.ProtocolVersion, clone.ProtocolVersion);
        Assert.Equal(package.TmpToken, clone.TmpToken);
    }

    [Fact]
    public void RejectPackageRoundTripsReasonOnly()
    {
        var package = new ConnectionRejectPackage("客户端模组与服务器不一致，请刷新服务器信息后重试");

        var writer = ProtocolMessageCodec.Encode(package);
        var reader = writer;
        var clone = (ConnectionRejectPackage)ProtocolMessageCodec.Decode(reader);

        Assert.Equal(package.Reason, clone.Reason);
    }

    [Fact]
    public void ModDataHashUsesStableKeyInformation()
    {
        var left = ModProfileManager.ComputeDataHash(
            new ModProfile
            {
                Packages =
                [
                    new ModPackageRequirement { ModId = "example.addon", Version = "2.0.0", PackageHash = _hashA },
                    new ModPackageRequirement { ModId = "other.addon", Version = "1.0.0", PackageHash = _hashB }
                ]
            }
        );
        var right = ModProfileManager.ComputeDataHash(
            new ModProfile
            {
                Packages =
                [
                    new ModPackageRequirement { ModId = "other.addon", Version = "1.0.0", PackageHash = _hashB },
                    new ModPackageRequirement { ModId = "example.addon", Version = "2.0.0", PackageHash = _hashA }
                ]
            }
        );
        var changed = ModProfileManager.ComputeDataHash(
            new ModProfile
            {
                Packages =
                [
                    new ModPackageRequirement
                        { ModId = "example.addon", Version = "2.0.0", PackageHash = _hashC },
                    new ModPackageRequirement { ModId = "other.addon", Version = "1.0.0", PackageHash = _hashB }
                ]
            }
        );

        Assert.Equal(left, right);
        Assert.NotEqual(left, changed);
    }

    [Fact]
    public void ServerInfoPackageRoundTripsRequiredModProfile()
    {
        var package = new ServerInfoResponsePackage
        {
            ProtocolVersion = 1, TemporaryRepositories =
            [
                new ContentRepositoryDescriptor(Guid.NewGuid(), "http://127.0.0.1:9527", 0)
            ],
            RequiredModProfile = new RequiredModProfile(
                "server",
                [
                    new RequiredModPackage("example.addon", "2.0.0", _hashA)
                ]
            )
        };

        var writer = ProtocolMessageCodec.Encode(package);
        var reader = writer;
        var clone = Assert.IsType<ServerInfoResponsePackage>(ProtocolMessageCodec.Decode(reader));

        Assert.Equal(package.ProtocolVersion, clone.ProtocolVersion);
        var repository = Assert.Single(clone.TemporaryRepositories);
        Assert.Equal(package.TemporaryRepositories[0].Id, repository.Id);
        Assert.Equal("http://127.0.0.1:9527", repository.BaseUrl);
        Assert.NotNull(clone.RequiredModProfile);
        var requirement = Assert.Single(clone.RequiredModProfile.Packages);
        Assert.Equal("example.addon", requirement.ModId);
        Assert.Equal("2.0.0", requirement.Version);
        Assert.Equal(_hashA, requirement.PackageHash);
    }

    [Fact]
    public void ServerInfoPackageRejectsInvalidRequirementsBeforeWriting()
    {
        var package = new ServerInfoResponsePackage
        {
            RequiredModProfile = new RequiredModProfile(
                "server",
                [
                    new RequiredModPackage("example.addon", "1.0.0", string.Empty)
                ]
            )
        };

        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Encode(package));
    }

    [Fact]
    public void ServerInfoPackageRejectsUnsafeRepositoryWhileReading()
    {
        var invalid = new ServerInfoResponsePackage
            { TemporaryRepositories = [new ContentRepositoryDescriptor(Guid.NewGuid(), "file:///tmp/packages", 0)] };
        var payload = ProtocolPayloadCodec.Encode<IProtocolMessage>(invalid);
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Decode(payload));
    }

    [Fact]
    public void ServerInfoPackageRejectsEmptyHashWhileReading()
    {
        var invalid = new ServerInfoResponsePackage
        {
            RequiredModProfile = new RequiredModProfile(
                "server",
                [new RequiredModPackage("example.addon", "1.0.0", "")]
            )
        };
        var payload = ProtocolPayloadCodec.Encode<IProtocolMessage>(invalid);
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Decode(payload));
    }
}
