using System.Security.Cryptography;

using Engine.Network;

using EntitySystem.Core;

using Game.Authentication;
using Game.Managers;
using Game.Network;
using Game.Network.Transport;
using Game.Screens;
using Game.World;

using LiteNetLib.Utils;

using SCNET.Authentication.Protocol;
using SCNET.Authentication.Protocol.Contracts;
using SCNET.Authentication.Protocol.Http;
using SCNET.Authentication.Protocol.Security;

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
        var previousMode = GameManager.Role;
        CommonLib.Net.Role = WorldRole.SinglePlayer;
        try
        {
            var node = new NetNode
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
        var previousMode = GameManager.Role;
        var previousScreen = ScreensManager.CurrentScreen;
        try
        {
            CommonLib.Net.Role = WorldRole.RemoteClient;
            var loading = new Screen();
            ScreensManager.CurrentScreen = loading;
            var node = new NetNode { Role = WorldRole.RemoteClient, CurrentStage = NetNode.Stage.Bootstrapping };
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
        Assert.Equal(reason, NetNode.DecodeDisconnectReason(information));
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
        Assert.Equal(reason, NetNode.DecodeDisconnectReason(information));
    }

    [Theory]
    [InlineData(WorldRole.SinglePlayer)]
    [InlineData(WorldRole.Server)]
    [InlineData(WorldRole.RemoteClient)]
    public void DeferredStopNotifiesOnceAfterCleanupWithThePreviousRole(WorldRole role)
    {
        using var project = new Project();
        var node = new NetNode { Role = role, CurrentStage = NetNode.Stage.Connected };
        var client = new Client(Guid.NewGuid(), project);
        node.Clients.Add(0, client);
        node.Self = client;
        var notifications = new List<(WorldRole Role, string Reason)>();
        node.Stopped += (previousRole, reason) =>
        {
            Assert.Equal(WorldRole.None, node.Role);
            Assert.Equal(NetNode.Stage.NotConnected, node.CurrentStage);
            Assert.Empty(node.Clients);
            Assert.Null(node.Self);
            notifications.Add((previousRole, reason));
        };

        node.Stop("fixture disconnect");
        Assert.Empty(notifications);
        node.Update();
        node.Update();
        Assert.Equal([(role, "fixture disconnect")], notifications);
    }

    [Fact]
    public void ImmediateStopIsLifecycleOnlyAndDoesNotNotifyOrNavigate()
    {
        var previousScreen = ScreensManager.CurrentScreen;
        try
        {
            var screen = new Screen();
            ScreensManager.CurrentScreen = screen;
            var node = new NetNode { Role = WorldRole.RemoteClient, CurrentStage = NetNode.Stage.Connected };
            node.Stopped += (_, _) => throw new InvalidOperationException("Explicit callers own the transition.");
            node.StopImmediate();
            node.Update();
            Assert.Same(screen, ScreensManager.CurrentScreen);
            Assert.Equal(WorldRole.None, node.Role);
        }
        finally { ScreensManager.CurrentScreen = previousScreen; }
    }

    [Fact]
    public void StoppedNodeDoesNotRetainClientsOrTheirWorldReferences()
    {
        var previousMode = GameManager.Role;
        CommonLib.Net.Role = WorldRole.SinglePlayer;
        try
        {
            using var project = new Project();
            var node = new NetNode();
            var client = new Client(Guid.NewGuid(), project);
            node.Clients.Add(0, client);
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
