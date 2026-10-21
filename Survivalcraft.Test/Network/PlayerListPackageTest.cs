using Engine.Core.Mathematics;

using Game.Network.Transport;

namespace Survivalcraft.Test.Network;

public class PlayerListPackageTest
{
    [Fact]
    public void PlayerListSnapshotRoundTrips()
    {
        var onlinePlayer = Guid.NewGuid();
        var offlinePlayer = Guid.NewGuid();
        var players = new List<PlayerListEntry>();
        players.Add(
            new PlayerListEntry(
                onlinePlayer,
                "Online",
                true
            )
        );
        players.Add(
            new PlayerListEntry(
                offlinePlayer,
                "Offline",
                false
            )
        );

        var package = new PlayerListPackage(players);
        var clone = RoundTrip(package);

        Assert.Equal(package.Players, clone.Players);
        Assert.Equal(PackageTransportPolicy.Control, PackageTransportPolicy.Get(package));
    }

    [Fact]
    public void OnlineStateSnapshotRoundTripsAndUsesAtomicReliableTransport()
    {
        var package = new OnlinePlayerStatePackage(
            [
                new OnlinePlayerState(
                    Guid.NewGuid(),
                    new Vector3(12.5f, 64f, -8f),
                    0.75f,
                    true
                )
            ]
        );

        var clone = RoundTrip(package);

        Assert.Equal(package.Players, clone.Players);
        Assert.Equal(PackageTransportPolicy.PlayerListState, PackageTransportPolicy.Get(package));
    }

    private static PlayerListPackage RoundTrip(PlayerListPackage package)
    {
        var writer = new ProtocolWriter();
        ProtocolMessageCodec.Write(package, writer);
        using var reader = new ProtocolReader(writer.Data());
        var clone = Assert.IsType<PlayerListPackage>(ProtocolMessageCodec.Read(package.ID, reader));
        return clone;
    }

    private static OnlinePlayerStatePackage RoundTrip(OnlinePlayerStatePackage package)
    {
        var writer = new ProtocolWriter();
        ProtocolMessageCodec.Write(package, writer);
        using var reader = new ProtocolReader(writer.Data());
        var clone = Assert.IsType<OnlinePlayerStatePackage>(ProtocolMessageCodec.Read(package.ID, reader));
        return clone;
    }
}
