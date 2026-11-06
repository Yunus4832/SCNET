using Engine.Core.Security;

using Game.Modding.Blocks;
using Game.Network;
using Game.Network.Handling;
using Game.Network.Transport;
using Game.Protocol.Identity;
using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Network;

public sealed class LocalGameRuntimeTest
{
    [Fact]
    public void TrustedLocalAdmissionUsesAnOrdinarySessionAndBootstrapWithoutPortsOrAuthoritySelf()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        project.AddSubsystem(new SubsystemPlayers());
        project.AddSubsystem(new SubsystemGameInfo { WorldSettings = new WorldSettings { Name = "Local" } });
        using var gameRuntime = new GameRuntime(false);
        var authority = WorldNetworkBinding.For(project).RequireNode();
        gameRuntime.Attach(authority.Runtime);
        Client? admitted = null;
        authority.OnClientTransportConnected += player =>
        {
            admitted = player;
            authority.QueuePackage(
                WorldSnapshotBuilder.Bootstrap(
                    project,
                    player.ConnectionEpoch,
                    authority.Clients.Values,
                    null,
                    [],
                    BlockIndexMapping.Empty
                ),
                PackageAudience.To(player)
            );
        };
        var address = new PlayerAddress(IdentityAddress.Encode(new byte[32]));

        var client = gameRuntime.ConnectLocalClient(
            address,
            Guid.NewGuid(),
            ApplicationMessageHandlers.CreatePreWorld(NodeRole.Client, true)
        );
        var messages = new List<ReceivedPackage>();
        client.OnReceive += (_, received) => messages.AddRange(received);
        // Budgeted bootstrap dispatch can continue on the next update during cold startup.
        gameRuntime.UpdateMessages();
        gameRuntime.UpdateMessages();

        var bootstrap = Assert.IsType<BootstrapPackage>(Assert.Single(messages).Package);
        Assert.True(BootstrapPackageHandler.CanApply(bootstrap, client));
        Assert.NotNull(admitted);
        Assert.Equal(address, admitted.PlayerAddress);
        Assert.Same(admitted, authority.Clients[admitted.ID]);
        Assert.True(admitted.Connection!.IsInProcess);
        Assert.True(admitted.IsConnected);
        Assert.Null(authority.Self);
        Assert.NotSame(authority, client);
        Assert.Equal(address, client.Self!.PlayerAddress);
        Assert.Null(client.Runtime.Project);
        Assert.Null(authority.LocalPort);
        Assert.Null(client.LocalPort);
    }

    [Fact]
    public void LocalAdmissionStillEnforcesWorldCapacity()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        var players = new SubsystemPlayers();
        project.AddSubsystem(players);
        project.AddSubsystem(
            new SubsystemGameInfo
            {
                WorldSettings = new WorldSettings { Name = "Full", MaxOnlinePlayerCount = 0 }
            }
        );
        using var gameRuntime = new GameRuntime(false);
        gameRuntime.Attach(WorldRuntimeContext.For(project));
        var address = new PlayerAddress(IdentityAddress.Encode(new byte[32]));

        Assert.Throws<InvalidOperationException>(() => gameRuntime.ConnectLocalClient(
                address,
                Guid.NewGuid(),
                ApplicationMessageHandlers.CreatePreWorld(NodeRole.Client, true)
            )
        );

        Assert.Null(gameRuntime.Client);
        Assert.Null(WorldNetworkBinding.For(gameRuntime.Authority!).RequireNode().Self);
        Assert.Empty(players.Store.Entries);
        Assert.False(project.IsDisposed);
    }

    [Fact]
    public void ReleasingLocalClientPreservesAuthorityAndOtherParticipants()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        project.AddSubsystem(new SubsystemPlayers());
        project.AddSubsystem(new SubsystemGameInfo { WorldSettings = new WorldSettings { Name = "Local" } });
        using var gameRuntime = new GameRuntime(false);
        var authority = WorldNetworkBinding.For(project).RequireNode();
        gameRuntime.Attach(authority.Runtime);
        var address = new PlayerAddress(IdentityAddress.Encode(new byte[32]));
        var other = new Client(null, 2, Guid.NewGuid(), Guid.Empty)
        {
            PlayerAddress = new PlayerAddress(IdentityAddress.Encode(Enumerable.Repeat((byte)1, 32).ToArray()))
        };
        authority.AddClient(other);
        var client = gameRuntime.ConnectLocalClient(
            address,
            Guid.NewGuid(),
            ApplicationMessageHandlers.CreatePreWorld(NodeRole.Client, true)
        );

        gameRuntime.ReleaseClient();
        gameRuntime.UpdateMessages();

        Assert.True(client.Runtime.IsClosed || !client.IsConnected);
        Assert.False(project.IsDisposed);
        Assert.True(authority.IsServer);
        Assert.Null(authority.Self);
        Assert.Same(other, authority.Clients[other.ID]);
        Assert.DoesNotContain(authority.Clients.Values, player => player.PlayerAddress == address);
        var replacement = gameRuntime.ConnectLocalClient(
            address,
            Guid.NewGuid(),
            ApplicationMessageHandlers.CreatePreWorld(NodeRole.Client, true)
        );
        Assert.Equal(address, replacement.Self!.PlayerAddress);
    }
}
