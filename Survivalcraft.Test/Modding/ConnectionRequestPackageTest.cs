using Game.Network.Packages;
using Game.Network.Serialization;

namespace Survivalcraft.Test.Modding;

public class ConnectionRequestPackageTest
{
    [Fact]
    public void PackageRoundTripsHandshakeModInfos()
    {
        var package = new ConnectionRequestPackage(
            Guid.NewGuid(),
            "1.0.0",
            "user",
            "token",
            "pwd",
            [
                new ModHandshakeInfo("Built-in Game Content", "game", "1.0.0", "abc"),
                new ModHandshakeInfo("Addon", "example.addon", "2.0.0", "def")
            ]);

        var writer = new PackageStreamWriter();
        package.WriteData(writer);
        var reader = new PackageStreamReader(writer.Data());
        var clone = new ConnectionRequestPackage();
        clone.ReadData(reader);

        Assert.Equal(package.ModInfos, clone.ModInfos);
        Assert.Equal(package.Version, clone.Version);
        Assert.Equal(package.Password, clone.Password);
    }
}
