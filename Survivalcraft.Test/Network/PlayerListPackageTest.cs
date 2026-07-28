using Engine.Core;

using Game;
using Game.Network;
using Game.Network.Packages;
using Game.Network.Serialization;

namespace Survivalcraft.Test.Network;

public class PlayerListPackageTest
{
    [Fact]
    public void PlayerListSnapshotRoundTrips()
    {
        var onlinePlayer = Guid.NewGuid();
        var offlinePlayer = Guid.NewGuid();
        var package = new PlayerListPackage();
        package.Players.Add(new PlayerListEntry(
            onlinePlayer,
            "Online",
            true));
        package.Players.Add(new PlayerListEntry(
            offlinePlayer,
            "Offline",
            false));

        var clone = RoundTrip(package);

        Assert.Equal(package.Players, clone.Players);
        Assert.Equal(PackageTransportPolicy.Control, PackageTransportPolicy.Get(package));
    }

    [Fact]
    public void OnlineStateSnapshotRoundTripsAndUsesSnapshotTransport()
    {
        var package = new OnlinePlayerStatePackage();
        package.Players.Add(new OnlinePlayerState(
            Guid.NewGuid(),
            new Vector3(12.5f, 64f, -8f),
            0.75f,
            true));

        var clone = RoundTrip(package);

        Assert.Equal(package.Players, clone.Players);
        Assert.Equal(PackageTransportPolicy.Snapshot, PackageTransportPolicy.Get(package));
    }

    private static PlayerListPackage RoundTrip(PlayerListPackage package)
    {
        var writer = new PackageStreamWriter();
        package.WriteData(writer);
        using var reader = new PackageStreamReader(writer.Data());
        var clone = new PlayerListPackage();
        clone.ReadData(reader);
        return clone;
    }

    private static OnlinePlayerStatePackage RoundTrip(OnlinePlayerStatePackage package)
    {
        var writer = new PackageStreamWriter();
        package.WriteData(writer);
        using var reader = new PackageStreamReader(writer.Data());
        var clone = new OnlinePlayerStatePackage();
        clone.ReadData(reader);
        return clone;
    }
}
