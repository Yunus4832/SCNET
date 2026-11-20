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
    public async Task TrustedLocalAdmissionUsesAnOrdinarySessionAndBootstrapWithoutPortsOrAuthoritySelf()
    {
        await using var game = new GameRuntime(false, Inline);
        Client? admitted = null;
        await game.StartAuthorityAsync(_ =>
            {
                var project = CreateProject();
                var authority = WorldNetworkBinding.For(project).RequireNode();
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
                return authority.Runtime;
            }
        );
        var address = new PlayerAddress(IdentityAddress.Encode(new byte[32]));
        var messages = new List<ReceivedPackage>();
        var client = await game.ConnectLocalClientAsync(
            address,
            Guid.NewGuid(),
            ApplicationMessageHandlers.CreatePreWorld(NodeRole.Client, true),
            endpoint => endpoint.OnReceive += (_, received) => messages.AddRange(received)
        );
        for (var i = 0; i < 100 && messages.Count == 0; i++)
        {
            game.UpdateClient(.016f, Engine.Core.Runtime.Time.RealTime);
            await Task.Delay(10);
        }

        var bootstrap = Assert.IsType<BootstrapPackage>(Assert.Single(messages).Package);
        Assert.True(BootstrapPackageHandler.CanApply(bootstrap, client));
        var valid = await game.AuthorityExecution!.InvokeAsync(world =>
            {
                var node = WorldNetworkBinding.For(world).RequireNode();
                return admitted != null &&
                       admitted.PlayerAddress == address &&
                       ReferenceEquals(admitted, node.Clients[admitted.ID]) &&
                       admitted.Connection!.IsInProcess &&
                       admitted.IsConnected &&
                       node.Self == null &&
                       node.LocalPort == null;
            }
        );
        Assert.True(valid);
        Assert.NotSame(game.Authority, client.Runtime);
        Assert.Equal(address, client.Self!.PlayerAddress);
        Assert.Null(client.Runtime.Project);
        Assert.Null(client.LocalPort);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BootstrapFailureRollsBackLocalConnectionSoTheSamePlayerCanRetry(bool waitsForPeer)
    {
        await using var game = new GameRuntime(false, Inline);
        Client? failedClient = null;
        var peer = new Client(null, 2, Guid.NewGuid(), Guid.Empty);
        await game.StartAuthorityAsync(_ =>
            {
                var node = WorldNetworkBinding.For(CreateProject()).RequireNode();
                if (waitsForPeer) { node.AgreeOnPendingPeer.Add(peer.ID); }

                node.OnClientTransportConnected += player =>
                {
                    if (failedClient != null) { return; }

                    failedClient = player;
                    throw new InvalidOperationException("Injected bootstrap failure.");
                };
                return node.Runtime;
            }
        );
        var execution = game.AuthorityExecution!;
        var subject = new PlayerAddress(IdentityAddress.Encode(new byte[32]));
        var handlers = ApplicationMessageHandlers.CreatePreWorld(NodeRole.Client, true);
        Task<NetNode> Connect() => game.ConnectLocalClientAsync(subject, Guid.NewGuid(), handlers, _ => { });
        if (waitsForPeer)
        {
            var pendingClient = await Connect();
            Assert.True(pendingClient.Server!.Connection!.IsConnected);
            await execution.InvokeAsync(world =>
                {
                    WorldNetworkBinding.For(world).RequireNode().DeliveryEvent(peer);
                    return true;
                }
            );
            Assert.False(pendingClient.Server.Connection.IsConnected);
            await game.ReleaseClientAsync();
        }
        else
        {
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(Connect);
            Assert.Equal("Injected bootstrap failure.", failure.Message);
        }

        Assert.Null(game.Client);
        await execution.InvokeAsync(world =>
            {
                var node = WorldNetworkBinding.For(world).RequireNode();
                Assert.DoesNotContain(node.Clients.Values, player => player.PlayerAddress == subject);
                Assert.False(failedClient!.Connection!.IsConnected);
                return true;
            }
        );

        var client = await Connect();
        Assert.Equal(subject, client.Self!.PlayerAddress);
        Assert.Same(client.Runtime, game.Client);
    }

    [Fact]
    public async Task LocalAdmissionStillEnforcesWorldCapacity()
    {
        await using var game = new GameRuntime(false, Inline);
        await game.StartAuthorityAsync(_ => WorldRuntimeContext.For(CreateProject(0)));
        var address = new PlayerAddress(IdentityAddress.Encode(new byte[32]));
        await Assert.ThrowsAsync<InvalidOperationException>(() => game.ConnectLocalClientAsync(
                address,
                Guid.NewGuid(),
                ApplicationMessageHandlers.CreatePreWorld(NodeRole.Client, true),
                _ => { }
            )
        );
        Assert.Null(game.Client);
        var valid = await game.AuthorityExecution!.InvokeAsync(world =>
            WorldNetworkBinding.For(world).RequireNode().Self == null &&
            world.Project!.FindSubsystem<SubsystemPlayers>(true)!.Store.Entries.Count == 0 &&
            !world.Project.IsDisposed
        );
        Assert.True(valid);
    }

    [Fact]
    public async Task ReleasingLocalClientPreservesAuthorityAndOtherParticipants()
    {
        await using var game = new GameRuntime(false, Inline);
        Client? other = null;
        await game.StartAuthorityAsync(_ =>
            {
                var project = CreateProject();
                var node = WorldNetworkBinding.For(project).RequireNode();
                other = new Client(null, 2, Guid.NewGuid(), Guid.Empty)
                {
                    PlayerAddress = new PlayerAddress(IdentityAddress.Encode(Enumerable.Repeat((byte)1, 32).ToArray()))
                };
                node.AddClient(other);
                return node.Runtime;
            }
        );
        var address = new PlayerAddress(IdentityAddress.Encode(new byte[32]));
        var client = await game.ConnectLocalClientAsync(
            address,
            Guid.NewGuid(),
            ApplicationMessageHandlers.CreatePreWorld(NodeRole.Client, true),
            _ => { }
        );
        await game.ReleaseClientAsync();
        var valid = false;
        for (var i = 0; i < 100 && !valid; i++)
        {
            valid = await game.AuthorityExecution!.InvokeAsync(world =>
                {
                    var node = WorldNetworkBinding.For(world).RequireNode();
                    return !world.Project!.IsDisposed &&
                           node.IsServer &&
                           node.Self == null &&
                           ReferenceEquals(other, node.Clients[other!.ID]) &&
                           !node.Clients.Values.Any(player => player.PlayerAddress == address);
                }
            );
            if (!valid) { await Task.Delay(10); }
        }

        Assert.True(valid);
        Assert.True(client.Runtime.IsClosed || !client.IsConnected);
        var replacement = await game.ConnectLocalClientAsync(
            address,
            Guid.NewGuid(),
            ApplicationMessageHandlers.CreatePreWorld(NodeRole.Client, true),
            _ => { }
        );
        Assert.Equal(address, replacement.Self!.PlayerAddress);
    }

    private static RuntimeTestProject CreateProject(ushort maxPlayers = 4)
    {
        var project = new RuntimeTestProject(RuntimeSide.Authority);
        project.AddSubsystem(new SubsystemPlayers());
        project.AddSubsystem(
            new SubsystemGameInfo
                { WorldSettings = new WorldSettings { Name = "Local", MaxOnlinePlayerCount = maxPlayers } }
        );
        return project;
    }

    private static Task Inline(Action action)
    {
        action();
        return Task.CompletedTask;
    }
}
