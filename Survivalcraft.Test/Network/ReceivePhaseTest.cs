using Game.Network;
using Game.Network.Handling;
using Game.Network.Transport;

namespace Survivalcraft.Test.Network;

public sealed class ReceivePhaseTest
{
    [Fact]
    public void GameplayCannotBypassBootstrapByClaimingPlayingState()
    {
        var player = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid())
        {
            State = ClientState.Playing
        };
        foreach (var phase in Enum.GetValues<ConnectionPhase>())
        {
            player.ConnectionPhase = phase;
            Assert.Equal(
                phase == ConnectionPhase.Live,
                ProtocolReceivePolicy.CanHandleFromClient(new SubsystemTimePackage(1, 0), player)
            );
            Assert.Equal(
                phase >= ConnectionPhase.BootstrapApplied,
                ProtocolReceivePolicy.CanHandleFromClient(new TerrainChunksRequestPackage(), player)
            );
        }

        player.State = ClientState.Connected;
        Assert.False(ProtocolReceivePolicy.CanHandleFromClient(new TerrainChunksRequestPackage(), player));
    }

    [Fact]
    public void PhaseIsCheckedWhenDeliveringSoAnEarlierAcknowledgementCanUnlockQueuedGameplay()
    {
        var authority = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Authority
        );
        var client = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Client
        );
        var player = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid())
        {
            State = ClientState.Playing,
            ConnectionEpoch = Guid.NewGuid(),
            ConnectionPhase = ConnectionPhase.WorldSnapshotSent
        };
        authority.AddClient(player);
        var server = new Client(null, 0, Guid.NewGuid(), Guid.Empty);
        var pair = InProcessMessageConnection.CreatePair(authority, player, client, server);
        using var toAuthority = pair.ToAuthority;
        using var toClient = pair.ToClient;
        var received = 0;
        authority.OnReceive += (_, messages) =>
        {
            foreach (var message in messages)
            {
                if (message.Package is ConnectionPhaseAckPackage ack)
                {
                    new ConnectionPhaseAckPackageHandler().Handle(ack, message.Context);
                }
                else { received++; }
            }
        };
        try
        {
            Send(new SubsystemTimePackage(1, 0));
            authority.PumpReceivedPackages();
            Assert.Equal(0, received);

            Send(new ConnectionPhaseAckPackage(player.ConnectionEpoch, ConnectionPhase.WorldSnapshotApplied));
            Send(new SubsystemTimePackage(2, 0));
            authority.PumpReceivedPackages();
            // A cold acknowledgement handler can exhaust the cooperative per-pump time budget.
            authority.PumpReceivedPackages();

            Assert.Equal(ConnectionPhase.Live, player.ConnectionPhase);
            Assert.Equal(1, received);
        }
        finally
        {
            authority.Runtime.Close();
            client.Runtime.Close();
        }

        void Send(IProtocolMessage package)
        {
            Assert.Equal(
                ConnectionSendResult.Accepted,
                toAuthority.TrySendOwned(new OutboundPackage(package, PackageAudience.Global, client.Runtime))
            );
        }
    }
}
