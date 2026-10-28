using System.Security.Cryptography;

using Engine.Core.Runtime;
using Engine.Core.Security;

using EntitySystem.Core;

using Game.Admission;
using Game.Network;
using Game.Protocol.Identity;
using Game.Subsystems;
using Game.World;

using SCNET.Authentication.Protocol;
using SCNET.Authentication.Protocol.Contracts;
using SCNET.Authentication.Protocol.Http;
using SCNET.Authentication.Protocol.Security;

using Survivalcraft.Test.Subsystems;

namespace Survivalcraft.Test.Admission;

[Collection(WorldRoleCollection.Name)]
public sealed class PlayerAdmissionTest
{
    private static PlayerAddress Address(byte value) =>
        new(IdentityAddress.Encode(Enumerable.Repeat(value, 32).ToArray()));

    [Theory]
    [InlineData(RunModeType.Gui)]
    [InlineData(RunModeType.HeadlessServer)]
    public void PreparingServerDoesNotCreateOrAdmitTheLocalPlayer(RunModeType mode)
    {
        var previous = RunMode.Value;
        try
        {
            RunMode.Value = mode;
            var node = new NetNode();
            Assert.Equal(WorldRole.None, node.Role);
            node.PrepareServerWorld();
            Assert.Equal(WorldRole.Server, node.Role);
            Assert.True(node.IsServer);
            Assert.False(node.IsTransportRunning);
            Assert.Null(node.Self);
            Assert.NotNull(node.Server);
            Assert.Null(node.Server.PlayerAddress);
            Assert.Equal(Guid.Empty, node.Server.CharacterId);
            Assert.Equal(0, node.ClientCount);
            Assert.Single(node.Clients);
            node.StopImmediate();
            Assert.Equal(WorldRole.None, node.Role);
        }
        finally { RunMode.Value = previous; }
    }

    [Fact]
    public void PreparingSinglePlayerWorldDoesNotCreateOrAdmitItsPlayer()
    {
        var node = new NetNode();
        node.PrepareSinglePlayerWorld();
        Assert.Equal(WorldRole.SinglePlayer, node.Role);
        Assert.Null(node.Self);
        Assert.Null(node.Server);
        Assert.Empty(node.Clients);
        Assert.False(node.IsTransportRunning);
        Assert.Throws<InvalidOperationException>(node.JoinSinglePlayerWorld);
    }

    [Fact]
    public void SinglePlayerAdmissionIgnoresMultiplayerCapacity()
    {
        using var project = new AdmissionProject();
        project.Info.WorldSettings.MaxOnlinePlayerCount = 0;
        var node = new NetNode();
        node.PrepareSinglePlayerWorld();
        var address = Address(1);
        var token = Guid.NewGuid();
        Assert.True(
            PlayerAdmission.TryCreateSession(
                project,
                node,
                address,
                token,
                character => new Client(null, 0, token, character, project) { PlayerAddress = address },
                out var session,
                out var error
            )
        );
        Assert.Null(error);
        Assert.NotNull(session);
        Assert.NotNull(project.Players.Store.Find(address));
    }

    [Fact]
    public void DuplicateAndCapacityRejectionsDoNotCreateSubjectsOrInvokeSessionFactory()
    {
        using var project = new AdmissionProject();
        project.Info.WorldSettings.MaxOnlinePlayerCount = 1;
        var node = new NetNode();
        node.PrepareServerWorld();
        var first = Address(1);
        var second = Address(2);
        var token = Guid.NewGuid();
        var calls = 0;

        Client Create(Guid character)
        {
            calls++;
            return new Client(null, 1, token, character, project) { PlayerAddress = first };
        }

        Assert.True(
            PlayerAdmission.TryCreateSession(project, node, first, token, Create, out var admitted, out var error)
        );
        Assert.Null(error);
        node.AddClient(admitted!);
        Assert.True(node.IsPlayerOnline(admitted!));
        Assert.False(PlayerAdmission.TryCreateSession(project, node, first, Guid.NewGuid(), Create, out _, out error));
        Assert.Equal("该用户已经在线", error);
        Assert.False(PlayerAdmission.TryCreateSession(project, node, second, Guid.NewGuid(), Create, out _, out error));
        Assert.Equal("在线人数达到最大，拒绝加入", error);
        Assert.Null(project.Players.Store.Find(second));
        Assert.Equal(1, calls);
    }

    [Fact]
    public void FailedSessionConstructionCannotPublishAWorldSubject()
    {
        using var project = new AdmissionProject();
        var node = new NetNode();
        node.PrepareServerWorld();
        var address = Address(1);
        Assert.False(
            PlayerAdmission.TryCreateSession(
                project,
                node,
                address,
                Guid.NewGuid(),
                _ => throw new InvalidOperationException("fixture construction failure"),
                out var session,
                out var error
            )
        );
        Assert.Null(session);
        Assert.NotNull(error);
        Assert.Empty(project.Players.Store.Entries);
        Assert.Equal(0, node.ClientCount);
        Assert.True(node.IsServer);
        Assert.False(node.IsClosing);
    }

    [Theory]
    [InlineData(ServerAdmissionMode.Open)]
    [InlineData(ServerAdmissionMode.Password)]
    [InlineData(ServerAdmissionMode.External)]
    public async Task SuccessfulPolicyProofCannotBypassALaterWorldBan(ServerAdmissionMode mode)
    {
        using var provider = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var policy = mode switch
        {
            ServerAdmissionMode.Open => ServerAdmissionPolicy.Open,
            ServerAdmissionMode.Password => ServerAdmissionPolicy.WithPassword("fixture-password"),
            _ => ServerAdmissionPolicy.WithAuthority(
                new AuthenticationServiceEndpoint(
                    new AuthenticationServiceDescription(
                        AuthenticationProtocol.Version,
                        "Fixture",
                        "https://auth.example/",
                        AuthenticationPublicKey.From(provider).EncodedKey
                    )
                )
            )
        };
        var now = DateTimeOffset.UtcNow;
        var subject = Address(1);
        var attempt = new AdmissionAttempt(policy, subject.Value, Address(2).Value, new byte[32], now);
        var approval = mode == ServerAdmissionMode.External
            ? AdmissionApprovalTokens.Issue(provider, attempt.Challenge, now)
            : string.Empty;
        var proof = new AdmissionProofPackage(
            attempt.Challenge.Nonce,
            mode == ServerAdmissionMode.Password ? "fixture-password" : string.Empty,
            approval
        );
        Assert.Equal(AdmissionResultCode.Accepted, (await attempt.AuthorizeAsync(proof, now)).Code);
        using var project = new AdmissionProject();
        project.Players.Store.Ban(subject, "fixture ban", null, now);
        var node = new NetNode();
        node.PrepareServerWorld();
        Assert.False(
            PlayerAdmission.TryCreateSession(
                project,
                node,
                subject,
                Guid.NewGuid(),
                _ => throw new Exception("Must not create session"),
                out var session,
                out var error
            )
        );
        Assert.Null(session);
        Assert.Equal("你被禁止加入该服务器", error);
        Assert.Equal(0, node.ClientCount);
        Assert.Null(node.Self);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LocalRemovalOnlyNotifiesForInvoluntaryDeparture(bool voluntary)
    {
        using var project = new AdmissionProject();
        var node = new NetNode();
        node.PrepareServerWorld();
        var local = new Client(null, 1, Guid.NewGuid(), Guid.Empty, project)
            { PlayerAddress = Address(1), State = ClientState.Connected };
        node.Self = local;
        node.AddClient(local);
        var reasons = new List<string>();
        node.LocalPlayerRemoved += reasons.Add;
        if (voluntary) { node.LeaveLocalPlayer("reason"); }
        else { node.RemoveClientImmediate(local, "reason"); }

        Assert.Null(node.Self);
        Assert.True(node.IsServer);
        if (voluntary) { Assert.Empty(reasons); }
        else { Assert.Equal("reason", Assert.Single(reasons)); }
    }

    [Fact]
    public void LocalDeparturePreservesServerAndOtherParticipants()
    {
        using var project = new AdmissionProject();
        var node = new NetNode();
        node.PrepareServerWorld();
        var server = node.Server;
        var local = new Client(null, 1, Guid.NewGuid(), Guid.Empty, project)
            { PlayerAddress = Address(1), State = ClientState.Connected };
        var other = new Client(null, 2, Guid.NewGuid(), Guid.Empty, project)
            { PlayerAddress = Address(2), State = ClientState.Connected };
        node.Self = local;
        node.AddClient(local);
        node.AddClient(other);
        node.LeaveLocalPlayer();
        Assert.Null(node.Self);
        Assert.Same(server, node.Server);
        Assert.True(node.IsServer);
        Assert.False(node.IsClosing);
        Assert.Same(other, node.Clients[other.ID]);
        Assert.False(node.Clients.ContainsKey(local.ID));
        Assert.False(node.IsPlayerOnline(local));
        Assert.True(node.IsPlayerOnline(other));
    }

    private sealed class AdmissionProject : Project
    {
        public SubsystemPlayers Players { get; } = new();

        public SubsystemGameInfo Info { get; } = new() { WorldSettings = new WorldSettings() };

        public AdmissionProject()
        {
            subsystems.Add(Players);
            subsystems.Add(Info);
        }
    }
}
