using System.Security.Cryptography;

using Engine.Core.Security;

using EntitySystem.Core;

using Game.Admission;
using Game.Network;
using Game.Network.Transport;
using Game.Protocol.Identity;
using Game.Subsystems;
using Game.World;

using SCNET.Authentication.Protocol;
using SCNET.Authentication.Protocol.Contracts;
using SCNET.Authentication.Protocol.Http;
using SCNET.Authentication.Protocol.Security;

using Survivalcraft.Test.Subsystems;

namespace Survivalcraft.Test.Admission;

[Collection(NodeRoleCollection.Name)]
public sealed class PlayerAdmissionTest
{
    private static PlayerAddress Address(byte value) =>
        new(IdentityAddress.Encode(Enumerable.Repeat(value, 32).ToArray()));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PreparingServerDoesNotCreateOrAdmitTheLocalPlayer(bool hasPresentation)
    {
        var node = new NetNode(
            NetNodeOptions.Default,
            hasPresentation,
            Game.Network.Handling.PackageHandlerCatalog.Empty,
            NodeRole.Inactive
        );
        Assert.Equal(NodeRole.Inactive, node.Role);
        node.PrepareAuthorityWorld();
        Assert.Equal(NodeRole.Authority, node.Role);
        Assert.True(node.IsServer);
        Assert.False(node.IsTransportRunning);
        Assert.Null(node.Self);
        Assert.Null(node.Server);
        Assert.Equal(0, node.Connections.PlayerCount);
        Assert.Empty(node.Clients);
        node.StopImmediate();
        Assert.Equal(NodeRole.Inactive, node.Role);
    }

    [Fact]
    public void InProcessAdmissionAllocatesAPlayerWithoutAnAuthorityRosterEntry()
    {
        using var project = new AdmissionProject();
        project.Info.WorldSettings.MaxOnlinePlayerCount = 2;
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            Game.Network.Handling.PackageHandlerCatalog.Empty,
            NodeRole.Inactive
        );
        node.PrepareAuthorityWorld();
        node.Runtime.BindProject(project);
        var player = node.AdmitInProcessPlayer(Address(1), Guid.NewGuid());
        Assert.Equal(1, player.ID);
        Assert.NotNull(player.PlayerAddress);
        Assert.Null(node.Server);
        Assert.Null(node.Self);
        Assert.False(node.Clients.ContainsKey(0));
    }

    [Fact]
    public void LocalAuthorityUsesTheSameCapacityRules()
    {
        using var project = new AdmissionProject();
        project.Info.WorldSettings.MaxOnlinePlayerCount = 0;
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            Game.Network.Handling.PackageHandlerCatalog.Empty,
            NodeRole.Inactive
        );
        node.PrepareAuthorityWorld();
        var address = Address(1);
        var token = Guid.NewGuid();
        Assert.False(
            PlayerAdmission.TryCreateSession(
                project,
                node,
                address,
                token,
                character => new Client(null, 0, token, character) { PlayerAddress = address },
                out var session,
                out var error
            )
        );
        Assert.NotNull(error);
        Assert.Null(session);
        Assert.Null(project.Players.Store.Find(address));
    }

    [Fact]
    public void DuplicateAndCapacityRejectionsDoNotCreateSubjectsOrInvokeSessionFactory()
    {
        using var project = new AdmissionProject();
        project.Info.WorldSettings.MaxOnlinePlayerCount = 1;
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            Game.Network.Handling.PackageHandlerCatalog.Empty,
            NodeRole.Inactive
        );
        node.PrepareAuthorityWorld();
        node.Runtime.BindProject(project);
        var first = Address(1);
        var second = Address(2);
        var token = Guid.NewGuid();
        var calls = 0;

        Client Create(Guid character)
        {
            calls++;
            return new Client(null, 1, token, character) { PlayerAddress = first };
        }

        Assert.True(
            PlayerAdmission.TryCreateSession(project, node, first, token, Create, out var admitted, out var error)
        );
        Assert.Null(error);
        node.AddClient(admitted!);
        var replica = new NetNode(
            NetNodeOptions.Default,
            false,
            Game.Network.Handling.PackageHandlerCatalog.Empty,
            NodeRole.Inactive
        );
        var link = InProcessMessageConnection.CreatePair(node, admitted!, replica, new Client(Guid.Empty));
        using var toClient = link.ToClient;
        using var toAuthority = link.ToAuthority;
        Assert.True(WorldConnectionQuery.IsPlayerOnline(project, admitted!));
        Assert.False(PlayerAdmission.TryCreateSession(project, node, first, Guid.NewGuid(), Create, out _, out error));
        Assert.Equal("AlreadyOnline", error?.Key);
        Assert.False(PlayerAdmission.TryCreateSession(project, node, second, Guid.NewGuid(), Create, out _, out error));
        Assert.Equal("ServerFull", error?.Key);
        Assert.Null(project.Players.Store.Find(second));
        Assert.Equal(1, calls);
    }

    [Fact]
    public void FailedSessionConstructionCannotPublishAWorldSubject()
    {
        using var project = new AdmissionProject();
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            Game.Network.Handling.PackageHandlerCatalog.Empty,
            NodeRole.Inactive
        );
        node.PrepareAuthorityWorld();
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
        Assert.Equal(0, node.Connections.PlayerCount);
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
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            Game.Network.Handling.PackageHandlerCatalog.Empty,
            NodeRole.Inactive
        );
        node.PrepareAuthorityWorld();
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
        Assert.Equal("Banned", error?.Key);
        Assert.Equal(0, node.Connections.PlayerCount);
        Assert.Null(node.Self);
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
