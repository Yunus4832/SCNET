using Game;
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

        var writer = new ProtocolWriter();
        ProtocolMessageCodec.Write(package, writer);
        var prefix = new ProtocolReader(writer.Data());
        Assert.Equal(GameProtocol.ConnectionMagic, prefix.ReadInt32());
        Assert.Equal(package.ProtocolVersion, prefix.ReadInt32());
        var reader = new ProtocolReader(writer.Data());
        var clone = (ConnectionRequestPackage)ProtocolMessageCodec.Read(package.ID, reader);

        Assert.Equal(package.ModDataHash, clone.ModDataHash);
        Assert.Equal(package.ProtocolVersion, clone.ProtocolVersion);
        Assert.Equal(package.TmpToken, clone.TmpToken);
    }

    [Fact]
    public void RejectPackageRoundTripsReasonOnly()
    {
        var package = new ConnectionRejectPackage("客户端模组与服务器不一致，请刷新服务器信息后重试");

        var writer = new ProtocolWriter();
        ProtocolMessageCodec.Write(package, writer);
        var reader = new ProtocolReader(writer.Data());
        var clone = (ConnectionRejectPackage)ProtocolMessageCodec.Read(package.ID, reader);

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
        var package = new ServerInfoPackage
        {
            RequestInfo = false,
            ProtocolVersion = 1,
            TemporaryRepositories =
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

        var writer = new ProtocolWriter();
        ProtocolMessageCodec.Write(package, writer);
        var reader = new ProtocolReader(writer.Data());
        var prefix = new ProtocolReader(writer.Data());
        Assert.False(prefix.ReadBoolean());
        Assert.Equal(package.ProtocolVersion, prefix.ReadInt32());
        var clone = Assert.IsType<ServerInfoPackage>(ProtocolMessageCodec.Read(package.ID, reader));

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
        var package = new ServerInfoPackage
        {
            RequiredModProfile = new RequiredModProfile(
                "server",
                [
                    new RequiredModPackage("example.addon", "1.0.0", string.Empty)
                ]
            )
        };

        Assert.Throws<ArgumentException>(() => ProtocolMessageCodec.Write(package, new ProtocolWriter()));
    }

    [Fact]
    public void ServerInfoPackageRejectsUnsafeRepositoryWhileReading()
    {
        var writer = CreateServerInfoPrefix();
        writer.Write((ushort)1);
        writer.Write(Guid.NewGuid().ToString("D"));
        writer.Write("file:///tmp/packages");
        writer.Write(0);
        writer.Write(false);
        writer.Write((int)Season.Summer);
        writer.Write(0f);

        var package = new ServerInfoPackage();
        Assert.Throws<InvalidDataException>(() =>
            ProtocolMessageCodec.Read(package.ID, new ProtocolReader(writer.Data()))
        );
    }

    [Fact]
    public void ServerInfoPackageRejectsEmptyHashWhileReading()
    {
        var writer = CreateServerInfoPrefix();
        writer.Write((ushort)0);
        writer.Write(true);
        writer.Write("server");
        writer.Write((ushort)1);
        writer.Write("example.addon");
        writer.Write("1.0.0");
        writer.Write(string.Empty);
        writer.Write((int)Season.Summer);
        writer.Write(0f);

        var package = new ServerInfoPackage();
        Assert.Throws<InvalidDataException>(() =>
            ProtocolMessageCodec.Read(package.ID, new ProtocolReader(writer.Data()))
        );
    }

    private static ProtocolWriter CreateServerInfoPrefix()
    {
        var writer = new ProtocolWriter();
        writer.Write(false);
        writer.Write(1);
        writer.Write((ushort)0);
        writer.Write((ushort)4);
        writer.WriteEnum(GameMode.Survival);
        writer.Write(0.5f);
        return writer;
    }
}
