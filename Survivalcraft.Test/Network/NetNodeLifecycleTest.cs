using System.Reflection;
using System.Security.Cryptography;

using Engine.Network;
using Engine.Windowing;

using EntitySystem.Core;

using Game.Authentication;
using Game.Managers;
using Game.Network;
using Game.Network.Handling;
using Game.Network.Transport;
using Game.Screens;
using Game.World;

using LiteNetLib.Utils;

using SCNET.Authentication.Protocol;
using SCNET.Authentication.Protocol.Contracts;
using SCNET.Authentication.Protocol.Http;
using SCNET.Authentication.Protocol.Security;

using Survivalcraft.Test.Fixtures;
using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Network;

[Collection(ConfigFileCollection.Name)]
public sealed class NetNodeLifecycleTest
{
    [Fact]
    public void StoppingANodeClearsItsInMemoryAuthentication()
    {
        using var user = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var server = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var provider = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var endpoint = new AuthenticationServiceEndpoint(
            new AuthenticationServiceDescription(
                AuthenticationProtocol.Version,
                "Authority",
                "https://auth.example/",
                AuthenticationPublicKey.From(provider).EncodedKey
            )
        );
        var authorization = new TemporaryJoinAuthorization(
            AuthenticationPublicKey.From(user).Address,
            AuthenticationPublicKey.From(server).Address,
            AuthenticationJoinScope.ForRemote(new AddressNetworkTarget("game.example", 28887)),
            endpoint,
            null
        );
        var previousMode = CommonLib.Net.Role;
        CommonLib.Net.Role = NodeRole.Authority;
        try
        {
            var node = new NetNode(
                    NetNodeOptions.Default,
                    true,
                    PackageHandlerCatalog.Empty,
                    NodeRole.Inactive
                )
                { TemporaryAuthentication = authorization, CurrentStage = NetNode.Stage.Bootstrapping };
            node.CurrentStage = NetNode.Stage.Connected;
            node.Stop();
            Assert.Null(node.TemporaryAuthentication);
            node.TemporaryAuthentication = authorization;
            node.StopImmediate();
            Assert.Null(node.TemporaryAuthentication);
        }
        finally { CommonLib.Net.Role = previousMode; }
    }

    [Fact]
    public void AdmissionRecoveryClosesTransportWithoutNavigatingAndCannotCloseAnAdmittedSession()
    {
        var previousMode = CommonLib.Net.Role;
        var previousScreen = ScreensManager.CurrentScreen;
        try
        {
            CommonLib.Net.Role = NodeRole.Client;
            var loading = new Screen();
            ScreensManager.CurrentScreen = loading;
            var node = new NetNode(
                    NetNodeOptions.Default,
                    true,
                    PackageHandlerCatalog.Empty,
                    NodeRole.Client
                )
                { Role = NodeRole.Client, CurrentStage = NetNode.Stage.Bootstrapping };
            node.StopForAdmissionRecovery();
            Assert.Same(loading, ScreensManager.CurrentScreen);
            Assert.Equal(NetNode.Stage.NotConnected, node.CurrentStage);
            Assert.Empty(node.Clients);
            Assert.Null(node.TemporaryAuthentication);
            node.CurrentStage = NetNode.Stage.Connected;
            Assert.Throws<InvalidOperationException>(node.StopForAdmissionRecovery);
        }
        finally
        {
            CommonLib.Net.Role = previousMode;
            ScreensManager.CurrentScreen = previousScreen;
        }
    }

    [Fact]
    public void RemoteCloseReasonUsesTheTransportStringFormat()
    {
        const string reason = "服务器主动关闭";
        var information = new NetworkDisconnectInfo(
            NetworkDisconnectReason.RemoteClose,
            NetDataWriter.FromString(reason).CopyData(),
            0
        );
        Assert.Equal(reason, NetNode.DecodeDisconnectReason(information).Message);
    }

    [Fact]
    public void ConnectionRejectionReasonUsesTheProtocolPacketFormat()
    {
        const string reason = "服务器拒绝连接";
        var information = new NetworkDisconnectInfo(
            NetworkDisconnectReason.Rejected,
            NetNode.CreateUnconnectedPayload(new ConnectionRejectPackage(reason)),
            0
        );
        Assert.Equal(reason, NetNode.DecodeDisconnectReason(information).Message);
    }

    [Theory]
    [InlineData(NodeRole.Authority)]
    [InlineData(NodeRole.Client)]
    public void DeferredStopNotifiesOnceAfterCleanupWithThePreviousRole(NodeRole role)
    {
        using var project = new Project();
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            role
        ) { Role = role, CurrentStage = NetNode.Stage.Connected };
        var client = new Client(Guid.NewGuid());
        node.AddClient(client);
        node.Self = client;
        var notifications = new List<(NodeRole Role, string Reason)>();
        var originalRuntime = node.Runtime;
        node.Stopped += (source, stoppedRuntime, previousRole, reason) =>
        {
            Assert.Same(node, source);
            Assert.Same(originalRuntime, stoppedRuntime);
            Assert.True(stoppedRuntime.IsClosed);
            Assert.NotSame(stoppedRuntime, source.Runtime);
            Assert.Equal(NodeRole.Inactive, node.Role);
            Assert.Equal(NetNode.Stage.NotConnected, node.CurrentStage);
            Assert.Empty(node.Clients);
            Assert.Null(node.Self);
            notifications.Add((previousRole, reason.Message));
        };

        node.Stop("fixture disconnect");
        Assert.Empty(notifications);
        node.Update();
        node.Update();
        Assert.Equal([(role, "fixture disconnect")], notifications);
    }

    [Theory]
    [InlineData(NodeRole.Authority)]
    [InlineData(NodeRole.Client)]
    public void AnObservedOldEndpointCannotNavigateOrDisposeTheCurrentSession(NodeRole oldRole)
    {
        using var current = new GameRuntime(false);
        using var authority = new RuntimeTestProject(RuntimeSide.Authority);
        using var client = new RuntimeTestProject(RuntimeSide.Client);
        current.Attach(WorldRuntimeContext.For(authority));
        current.Attach(WorldRuntimeContext.For(client));
        var controller = typeof(GameManager).GetField("_worlds", BindingFlags.Static | BindingFlags.NonPublic)!
            .GetValue(null)!;
        var runtimeProperty = typeof(GameWorldController).GetProperty(nameof(GameWorldController.Runtime))!;
        var windowState = typeof(Window).GetField("_state", BindingFlags.Static | BindingFlags.NonPublic)!;
        var previousRuntime = GameManager.Runtime;

        var previousWindow = windowState.GetValue(null);
        var oldNode = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            oldRole
        );
        try
        {
            runtimeProperty.SetValue(controller, current);

            windowState.SetValue(null, Enum.Parse(windowState.FieldType, "Active"));
            GameManager.ObserveEndpoint(oldNode);

            oldNode.Stop("Late failure from a retired endpoint.");
            oldNode.Update();

            Assert.Same(current, GameManager.Runtime);
            Assert.Same(client, GameManager.ClientProject);
            Assert.False(current.IsDisposed);
            Assert.False(WorldRuntimeContext.For(authority).IsClosed);
            Assert.False(WorldRuntimeContext.For(client).IsClosed);
        }
        finally
        {
            windowState.SetValue(null, previousWindow);

            runtimeProperty.SetValue(controller, previousRuntime);
            oldNode.StopImmediate();
        }
    }

    [Fact]
    public void ImmediateStopIsLifecycleOnlyAndDoesNotNotifyOrNavigate()
    {
        var previousScreen = ScreensManager.CurrentScreen;
        try
        {
            var screen = new Screen();
            ScreensManager.CurrentScreen = screen;
            var node = new NetNode(
                    NetNodeOptions.Default,
                    true,
                    PackageHandlerCatalog.Empty,
                    NodeRole.Client
                )
                { Role = NodeRole.Client, CurrentStage = NetNode.Stage.Connected };
            node.Stopped += (_, _, _, _) => throw new InvalidOperationException("Explicit callers own the transition.");
            node.StopImmediate();
            node.Update();
            Assert.Same(screen, ScreensManager.CurrentScreen);
            Assert.Equal(NodeRole.Inactive, node.Role);
        }
        finally { ScreensManager.CurrentScreen = previousScreen; }
    }

    [Fact]
    public void StoppedNodeDoesNotRetainClientsOrTheirWorldReferences()
    {
        var previousMode = CommonLib.Net.Role;
        CommonLib.Net.Role = NodeRole.Authority;
        try
        {
            using var project = new Project();
            var node = new NetNode(
                NetNodeOptions.Default,
                true,
                PackageHandlerCatalog.Empty,
                NodeRole.Inactive
            );
            var client = new Client(Guid.NewGuid());
            node.AddClient(client);
            node.Self = client;
            node.Server = client;
            node.StopImmediate();
            node.StopImmediate();
            node.QueuePackage(
                new ConnectionPhaseAckPackage(Guid.Empty, ConnectionPhase.TransportConnected),
                PackageAudience.Global
            );
            Assert.Equal(0, node.PendingPackageCount);
            Assert.Empty(node.Clients);
            Assert.Null(node.Self);
            Assert.Null(node.Server);
        }
        finally
        {
            CommonLib.Net.Role = previousMode;
        }
    }
}
