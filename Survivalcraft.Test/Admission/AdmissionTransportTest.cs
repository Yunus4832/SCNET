using System.Net;
using System.Reflection;
using System.Security.Cryptography;

using Engine.Network;
using Engine.Network.Security;

using EntitySystem.Core;

using Game;
using Game.Admission;
using Game.Managers;
using Game.Network;
using Game.Network.Handling;
using Game.Subsystems;

using SCNET.Authentication.Protocol;
using SCNET.Authentication.Protocol.Contracts;
using SCNET.Authentication.Protocol.Http;
using SCNET.Authentication.Protocol.Security;

using Survivalcraft.Test.Network;
using Survivalcraft.Test.Subsystems;

namespace Survivalcraft.Test.Admission;

[Collection(PackageDecodingCollection.Name)]
public sealed class AdmissionTransportTest
{
    [Theory]
    [InlineData(ServerAdmissionMode.Open, false, false, false)]
    [InlineData(ServerAdmissionMode.Password, false, false, false)]
    [InlineData(ServerAdmissionMode.Password, false, true, false)]
    [InlineData(ServerAdmissionMode.Password, true, false, false)]
    [InlineData(ServerAdmissionMode.External, false, false, false)]
    [InlineData(ServerAdmissionMode.External, false, false, true)]
    [InlineData(ServerAdmissionMode.External, true, false, false)]
    public async Task SecureProvisionalPeerReceivesNoWorldSessionUntilAdmissionAndBusinessChecks(
        ServerAdmissionMode mode,
        bool banDuringWait,
        bool cancelBeforeCompletion,
        bool saturateVerificationBudget
    )
    {
        var previousProject = GameManager.Project;
        var previousWorkType = CommonLib.WorkType;
        var registrations = typeof(PackageManager).GetField(
            "_packageRegistrations",
            BindingFlags.Static | BindingFlags.NonPublic
        )!;
        var previousRegistrations = registrations.GetValue(null);
        var handlers = (Dictionary<Type, IPackageHandler>)typeof(PackageDispatcher)
            .GetField("_handlers", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var previousHandlers = handlers.ToArray();
        try
        {
            PackageManager.Initialize();
            CommonLib.WorkType = WorkType.Server;
            using var project = new AdmissionProject();
            typeof(GameManager).GetProperty(nameof(GameManager.Project))!.SetValue(null, project);
            using var serverKey = NetworkIdentity.Create();
            using var userKey = NetworkIdentity.Create();
            await using var server = new NetworkHost(
                options: new NetworkHostOptions
                    { Identity = serverKey, DisconnectTimeout = TimeSpan.FromMilliseconds(CommonLib.DisconnectTimeout) }
            );
            await using var remote = new NetworkHost(
                options: new NetworkHostOptions
                    { Identity = userKey, DisconnectTimeout = TimeSpan.FromMilliseconds(CommonLib.DisconnectTimeout) }
            );
            var node = new NetNode { CurrentStage = NetNode.Stage.Connected };
            var self = new Client(Guid.Empty, project);
            node.Self = self;
            node.Server = self;
            node.AddClient(self);
            using var provider = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var policy = mode switch
            {
                ServerAdmissionMode.Open => ServerAdmissionConfiguration.Open,
                ServerAdmissionMode.Password => ServerAdmissionConfiguration.WithPassword("transport-test-password"),
                _ => ServerAdmissionConfiguration.WithAuthority(
                    new AuthenticationServiceEndpoint(
                        new AuthenticationServiceDescription(
                            AuthenticationProtocol.Version,
                            "Test",
                            "https://auth.test/",
                            AuthenticationPublicKey.From(provider).EncodedKey
                        )
                    )
                )
            };
            var controller = new ServerAdmissionController(node, project, policy, serverKey.PublicIdentity.Address);
            typeof(NetNode).GetProperty("AdmissionController", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(node, controller);
            var gameplay = 0;
            var admitted = 0;
            node.OnReceive += (_, messages) =>
            {
                foreach (var received in messages)
                {
                    if (received.Package is AdmissionProofPackage) { PackageDispatcher.Handle(received); }
                    else { gameplay++; }
                }
            };
            node.OnClientTransportConnected += _ => admitted++;
            var receive = typeof(NetNode).GetMethod(
                    "NetworkReceiveEvent",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )!
                .CreateDelegate<Action<NetworkConnection, NetworkMessage>>(node);
            server.Received += receive;
            server.ConnectionRequested += request =>
            {
                var metadata = ProtocolPacketCodec.Decode(request.Handshake).OfType<ConnectionRequestPackage>()
                    .Single();
                new ConnectionRequestPackageHandler().Handle(metadata, new(node, true, new Client(request)));
            };
            AdmissionChallengePackage? challenge = null;
            var replies = new List<AdmissionResultPackage>();
            var rejects = new List<ConnectionRejectPackage>();
            var token = Guid.NewGuid();
            var clientNode = new NetNode { CurrentStage = NetNode.Stage.Bootstrapping, TokenId = token };
            var bootstraps = new List<BootstrapPackage>();
            clientNode.OnReceive += (_, messages) =>
                bootstraps.AddRange(messages.Select(message => message.Package).OfType<BootstrapPackage>());
            remote.Received += (source, message) =>
            {
                foreach (var package in ProtocolPacketCodec.Decode(message.Payload))
                {
                    if (package is AdmissionChallengePackage found) { challenge = found; }

                    if (package is ConnectionRejectPackage reject)
                    {
                        rejects.Add(reject);
                        new ConnectionRejectPackageHandler().Handle(reject, new(clientNode, false, new Client(source)));
                    }

                    if (package is AdmissionResultPackage result)
                    {
                        replies.Add(result);
                        clientNode.ReceiveAdmissionResult(new Client(source), result);
                    }
                }
            };
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var connecting = remote.ConnectAsync(
                new IPEndPoint(IPAddress.Loopback, server.LocalPort),
                ProtocolPacketCodec.Encode(
                    [
                        new ConnectionRequestPackage(
                            Guid.NewGuid(),
                            VersionsManager.ProtocolVersion,
                            GameProtocol.EmptyModDataHash
                        )
                    ]
                ),
                cancellation.Token
            );

            async Task Until(Func<bool> condition)
            {
                while (!condition())
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    server.DispatchEvents();
                    remote.DispatchEvents();
                    controller.Update();
                    await Task.Delay(2, cancellation.Token);
                }
            }

            await Until(() => challenge is not null && connecting.IsCompleted);
            var peer = await connecting;
            var receivedChallenge = Assert.IsType<AdmissionChallengePackage>(challenge);
            var subject = new Game.Protocol.Identity.PlayerAddress(userKey.PublicIdentity.Address);
            Assert.Single(node.Clients);
            Assert.Empty(project.Players.Store.Entries);
            Assert.Equal(0, admitted);
            peer.Send(
                ProtocolPacketCodec.Encode([new SubsystemTimePackage(0, 0)]),
                (byte)NetworkChannel.Control,
                DeliveryMode.ReliableOrdered
            );
            if (saturateVerificationBudget)
            {
                var budget = Assert.IsType<SemaphoreSlim>(
                    typeof(ServerAdmissionController)
                        .GetField("_verificationSlots", BindingFlags.Instance | BindingFlags.NonPublic)
                        ?.GetValue(controller)
                );
                for (var index = 0; index < 32; index++) { Assert.True(budget.Wait(0)); }

                try
                {
                    peer.Send(
                        ProtocolPacketCodec.Encode([new AdmissionProofPackage(receivedChallenge.Nonce, "wrong")]),
                        (byte)NetworkChannel.Control,
                        DeliveryMode.ReliableOrdered
                    );
                    await Until(() => replies.Count == 1);
                    Assert.Equal(AdmissionResultCode.RateLimited, replies[0].Code);
                    Assert.Equal(0, admitted);
                    Assert.Empty(project.Players.Store.Entries);
                    Assert.Single(node.Clients);
                }
                finally { budget.Release(32); }

                replies.Clear();
            }

            // A control-channel barrier proves the forbidden packet was processed before the proof.
            if (mode != ServerAdmissionMode.Open)
            {
                peer.Send(
                    ProtocolPacketCodec.Encode([new AdmissionProofPackage(receivedChallenge.Nonce, "wrong")]),
                    (byte)NetworkChannel.Control,
                    DeliveryMode.ReliableOrdered
                );
                await Until(() => replies.Count == 1);
                Assert.Equal(
                    mode switch
                    {
                        ServerAdmissionMode.Password => AdmissionResultCode.IncorrectPassword,
                        ServerAdmissionMode.External => AdmissionResultCode.ApprovalRejected,
                        _ => AdmissionResultCode.Denied
                    },
                    replies[0].Code
                );
                Assert.Equal(0, gameplay);
                Assert.Empty(project.Players.Store.Entries);
            }

            if (banDuringWait) { project.Players.Store.Ban(subject, "test", null, DateTimeOffset.UtcNow); }

            // Respect the actual retry deadline; this also keeps the DTLS transport alive while awaiting input.
            if (replies.Count > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(replies[0].RetryAfterSeconds), cancellation.Token);
            }

            if (cancelBeforeCompletion)
            {
                peer.Send(
                    ProtocolPacketCodec.Encode(
                        [
                            new AdmissionProofPackage(receivedChallenge.Nonce, "transport-test-password"),
                            new AdmissionProofPackage(receivedChallenge.Nonce, Cancel: true)
                        ]
                    ),
                    (byte)NetworkChannel.Control,
                    DeliveryMode.ReliableOrdered
                );
                await Until(() => replies.Any(reply => reply.Code == AdmissionResultCode.Cancelled));
                controller.Update();
                Assert.Equal(0, admitted);
                Assert.Empty(project.Players.Store.Entries);
                Assert.Single(node.Clients);
                Assert.DoesNotContain(replies, reply => reply.Code == AdmissionResultCode.Accepted);
                return;
            }

            if (mode == ServerAdmissionMode.Open)
            {
                // Force bulk/control reordering at the receive boundary without relying on network scheduling.
                var receiveClient = typeof(NetNode).GetMethod(
                        "NetworkReceiveEvent",
                        BindingFlags.Instance | BindingFlags.NonPublic
                    )!
                    .CreateDelegate<Action<NetworkConnection, NetworkMessage>>(clientNode);
                var early = new BootstrapPackage
                {
                    Epoch = Guid.NewGuid(),
                    World = new WorldMetadata("early", 1, ProtocolEnum.Encode(GameMode.Creative), 20),
                    ClientList = ClientPackageBuilder.Snapshot(
                        [new Client(null, 1, token, Guid.Empty, null) { PlayerAddress = subject }]
                    )
                };
                receiveClient(
                    peer,
                    new NetworkMessage(ProtocolPacketCodec.Encode([early]), DeliveryMode.ReliableOrdered)
                );
                receiveClient(
                    peer,
                    new NetworkMessage(
                        ProtocolPacketCodec.Encode([early with { Epoch = Guid.NewGuid() }]),
                        DeliveryMode.ReliableOrdered
                    )
                );
                Assert.Empty(bootstraps);
            }

            clientNode.ReceiveAdmissionChallenge(new Client(peer), receivedChallenge);
            if (mode != ServerAdmissionMode.Open)
            {
                if (mode == ServerAdmissionMode.External && !banDuringWait)
                {
                    var waiting = System.Diagnostics.Stopwatch.StartNew();
                    while (waiting.Elapsed < TimeSpan.FromSeconds(12))
                    {
                        server.DispatchEvents();
                        remote.DispatchEvents();
                        controller.Update();
                        await Task.Delay(20, cancellation.Token);
                    }

                    Assert.True(peer.IsConnected);
                    Assert.True(clientNode.AdmissionDeadline > DateTimeOffset.UtcNow);
                }

                var approval = mode == ServerAdmissionMode.External
                    ? AdmissionApprovalTokens.Issue(
                        provider,
                        new AdmissionChallenge(
                            userKey.PublicIdentity.Address,
                            peer.PeerIdentity.Address,
                            receivedChallenge.Nonce,
                            AuthenticationNonce.Encode(peer.ChannelBinding.Span),
                            receivedChallenge.ExpiresAt
                        ),
                        DateTimeOffset.UtcNow
                    )
                    : string.Empty;
                var proof = new AdmissionProofPackage(
                    receivedChallenge.Nonce,
                    mode == ServerAdmissionMode.Password ? "transport-test-password" : string.Empty,
                    approval
                );
                clientNode.TrySubmitAdmission(proof);
            }

            await Until(() =>
                banDuringWait ? rejects.Count > 0 : replies.Any(reply => reply.Code == AdmissionResultCode.Accepted)
            );
            if (banDuringWait)
            {
                Assert.Contains("禁止", Assert.Single(rejects).Reason);
                Assert.DoesNotContain(replies, reply => reply.Code == AdmissionResultCode.Accepted);
                Assert.False(clientNode.AdmissionAccepted);
                Assert.Equal(0, admitted);
                Assert.Single(node.Clients);
                return;
            }

            server.DispatchEvents();
            controller.Update();
            Assert.Null(clientNode.PendingAdmissionChallenge);
            Assert.NotNull(clientNode.AdmissionDeadline);
            Assert.True(clientNode.AdmissionAccepted);
            if (mode == ServerAdmissionMode.Open)
            {
                Assert.True(BootstrapPackageHandler.CanApply(Assert.Single(bootstraps), clientNode));
            }

            Assert.Same(self, node.Clients[0]);
            Assert.Equal(0, gameplay);
            Assert.Equal(banDuringWait ? 0 : 1, admitted);
            Assert.Equal(banDuringWait ? 1 : 2, node.Clients.Count);
            if (!banDuringWait)
            {
                Assert.Equal(subject, node.Clients.Values.Single(client => client != self).PlayerAddress);
                Assert.Single(project.Players.Store.Entries);
            }
        }
        finally
        {
            registrations.SetValue(null, previousRegistrations);
            handlers.Clear();
            foreach (var entry in previousHandlers) { handlers.Add(entry.Key, entry.Value); }

            typeof(GameManager).GetProperty(nameof(GameManager.Project))!.SetValue(null, previousProject);
            CommonLib.WorkType = previousWorkType;
        }
    }

    [Fact]
    public void UnregisteredRoutingSlotZeroCannotRemoveTheActualHostOrDispatchGameplay()
    {
        using var project = new Project();
        var node = new NetNode { CurrentStage = NetNode.Stage.Connected };
        var self = new Client(Guid.Empty, project);
        node.Self = self;
        node.Server = self;
        node.AddClient(self);
        var provisional = new Client(null, 0, Guid.NewGuid(), Guid.Empty, project);
        Assert.False(node.CanReceiveFromSession(provisional, new SubsystemTimePackage(0, 0)));
        node.RemoveClientImmediate(provisional);
        Assert.Same(self, node.Clients[0]);
        Assert.True(node.CanReceiveFromSession(self, new SubsystemTimePackage(0, 0)));
    }

    [Fact]
    public void RemoteClientsCannotStopAServerWithAConnectionRejectionMessage()
    {
        var node = new NetNode { CurrentStage = NetNode.Stage.Connected };
        new ConnectionRejectPackageHandler().Handle(
            new ConnectionRejectPackage("untrusted stop"),
            new(node, true, null)
        );
        Assert.Equal(string.Empty, node.Error);
    }

    private sealed class AdmissionProject : Project
    {
        internal SubsystemPlayers Players { get; } = new();

        internal AdmissionProject()
        {
            subsystems.Add(Players);
            subsystems.Add(new SubsystemGameInfo { WorldSettings = new WorldSettings { MaxOnlinePlayerCount = 8 } });
            foreach (var subsystem in subsystems) { WorldSubsystemTestSupport.Attach(this, subsystem); }
        }
    }
}
