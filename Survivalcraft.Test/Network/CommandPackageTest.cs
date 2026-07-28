using Game.Commands;
using Game.Network;
using Game.Network.Packages;
using Game.Network.Serialization;

namespace Survivalcraft.Test.Network;

public class CommandPackageTest
{
    [Fact]
    public void RequestRoundTrips()
    {
        var package = CommandPackage.CreateRequest("/time get", "request-1");

        var clone = RoundTrip(package);

        Assert.Equal(CommandPackage.CommandPackageMode.Request, clone.Mode);
        Assert.Equal("request-1", clone.CorrelationId);
        Assert.Equal("/time get", clone.Input);
    }

    [Fact]
    public void CommandsUseReliableControlTransport()
    {
        var transport = PackageTransportPolicy.Get(CommandPackage.CreateRequest("/help"));

        Assert.Equal(PackageTransportPolicy.Control, transport);
    }

    [Fact]
    public void PermissionSnapshotRoundTrips()
    {
        var playerGuid = Guid.NewGuid();
        var package = CommandPackage.CreatePermissionSnapshot(
            playerGuid,
            [
                new CommandPermissionGrant("server.stop", false),
                new CommandPermissionGrant("world.*", true)
            ]);

        var clone = RoundTrip(package);

        Assert.Equal(CommandPackage.CommandPackageMode.PermissionSnapshot, clone.Mode);
        Assert.Equal(playerGuid, clone.PlayerGuid);
        Assert.Equal(
            [
                new CommandPermissionGrant("server.stop", false),
                new CommandPermissionGrant("world.*", true)
            ],
            clone.PermissionGrants);
    }

    private static CommandPackage RoundTrip(CommandPackage package)
    {
        var writer = new PackageStreamWriter();
        package.WriteData(writer);
        using var reader = new PackageStreamReader(writer.Data());
        var clone = new CommandPackage();
        clone.ReadData(reader);
        return clone;
    }
}
