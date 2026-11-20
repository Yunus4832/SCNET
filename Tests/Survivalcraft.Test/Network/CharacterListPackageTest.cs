using Engine.Core.Mathematics;
using Engine.Core.Security;

using Game.Network.Transport;
using Game.Protocol.Identity;

namespace Survivalcraft.Test.Network;

public class CharacterListPackageTest
{
    [Fact]
    public void PlayerListSnapshotRoundTrips()
    {
        var onlinePlayer = Guid.NewGuid();
        var offlinePlayer = Guid.NewGuid();
        var players = new List<CharacterListEntry>();
        players.Add(
            new CharacterListEntry(
                onlinePlayer,
                new PlayerAddress(
                    IdentityAddress.Encode(
                        Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000")
                    )
                ),
                "Online",
                true
            )
        );
        players.Add(
            new CharacterListEntry(
                offlinePlayer,
                new PlayerAddress(
                    IdentityAddress.Encode(
                        Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000")
                    )
                ),
                "Offline",
                false
            )
        );

        var package = new CharacterListPackage(players);
        var clone = RoundTrip(package);

        Assert.Equal(package.Characters, clone.Characters);
        Assert.Equal(PackageTransportPolicy.Control, PackageTransportPolicy.Get(package));
    }

    [Fact]
    public void OnlineStateSnapshotRoundTripsAndUsesAtomicReliableTransport()
    {
        var package = new OnlineCharacterStatePackage(
            [
                new OnlineCharacterState(
                    Guid.NewGuid(),
                    new Vector3(12.5f, 64f, -8f),
                    0.75f,
                    true
                )
            ]
        );

        var clone = RoundTrip(package);

        Assert.Equal(package.Characters, clone.Characters);
        Assert.Equal(PackageTransportPolicy.PlayerListState, PackageTransportPolicy.Get(package));
    }

    private static CharacterListPackage RoundTrip(CharacterListPackage package)
    {
        var writer = ProtocolMessageCodec.Encode(package);
        var reader = writer;
        var clone = Assert.IsType<CharacterListPackage>(ProtocolMessageCodec.Decode(reader));
        return clone;
    }

    private static OnlineCharacterStatePackage RoundTrip(OnlineCharacterStatePackage package)
    {
        var writer = ProtocolMessageCodec.Encode(package);
        var reader = writer;
        var clone = Assert.IsType<OnlineCharacterStatePackage>(ProtocolMessageCodec.Decode(reader));
        return clone;
    }
}
