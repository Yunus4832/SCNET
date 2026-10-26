using System.Security.Cryptography;

using Engine.Network;

using EntitySystem.Core;

using Game.Authentication;
using Game.Network;
using Game.Network.Transport;
using Game.Screens;

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
        var previousMode = CommonLib.WorkType;
        CommonLib.WorkType = WorkType.Local;
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
        finally { CommonLib.WorkType = previousMode; }
    }

    [Fact]
    public void AdmissionRecoveryClosesTransportWithoutNavigatingAndCannotCloseAnAdmittedSession()
    {
        var previousMode = CommonLib.WorkType;
        var previousScreen = ScreensManager.CurrentScreen;
        try
        {
            CommonLib.WorkType = WorkType.Client;
            var loading = new Screen();
            ScreensManager.CurrentScreen = loading;
            var node = new NetNode { CurrentStage = NetNode.Stage.Bootstrapping };
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
            CommonLib.WorkType = previousMode;
            ScreensManager.CurrentScreen = previousScreen;
        }
    }

    [Fact]
    public void StoppedNodeDoesNotRetainClientsOrTheirWorldReferences()
    {
        var previousMode = CommonLib.WorkType;
        CommonLib.WorkType = WorkType.Local;
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
            CommonLib.WorkType = previousMode;
        }
    }
}
