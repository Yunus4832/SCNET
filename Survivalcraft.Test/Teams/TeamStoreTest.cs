using Engine.Core.Security;

using Game.Players;
using Game.Protocol.Identity;
using Game.Teams;

namespace Game.Test.Teams;

public sealed class TeamStoreTest
{
    private readonly WorldPlayerStore _players = new();

    [Fact]
    public void CharacterlessPlayersOwnMembershipAndLeaderLeavingDisbands()
    {
        var store = new TeamStore(_players);
        var leader = Player(1);
        var member = Player(2);
        Assert.True(store.TryCreate(leader, "Builders", out _));
        var team = Assert.Single(store.Teams).Value;
        Assert.NotEqual(Guid.Empty, team.Id);
        Assert.Equal(Guid.Empty, leader.CharacterId);
        Assert.False(store.TryCreate(leader, "Again", out _));
        Join(store, leader, member);
        Assert.Equal(team.Id, store.FindForPlayer(member.Address)!.Id);
        Assert.True(store.TryLeave(leader, out _));
        Assert.Empty(store.Teams);
        Assert.Null(store.FindForPlayer(member.Address));
        Assert.Same(member, _players.Find(member.Address));
    }

    [Fact]
    public void OnlyDesignatedResponderCanConsumeARequestAndResponsesAreOneShot()
    {
        var store = new TeamStore(_players);
        var leader = Player(1);
        var member = Player(2);
        var outsider = Player(3);
        store.TryCreate(leader, "Builders", out _);
        Assert.True(store.TryRequestJoin(member, store.FindForPlayer(leader.Address)!.Id, out var operation, out _));
        Assert.False(store.TryRespond(outsider, operation!.Id, true, out _, out _));
        Assert.True(store.TryRespond(leader, operation.Id, true, out _, out _));
        Assert.False(store.TryRespond(leader, operation.Id, true, out _, out _));
        Assert.Equal(2, store.FindForPlayer(member.Address)!.Members.Count);
    }

    [Fact]
    public void TimeLimitsAndRateLimitsUseThePlayerRatherThanACharacter()
    {
        var now = 0.0;
        var store = new TeamStore(_players, clock: () => now);
        var leader = Player(1);
        var member = Player(2);
        Assert.True(store.TryBeginOperation(leader, out _));
        Assert.False(store.TryBeginOperation(leader, out _));
        Assert.True(store.TryBeginOperation(member, out _));
        now = 1;
        Assert.True(store.TryBeginOperation(leader, out _));
        store.TryCreate(leader, "Builders", out _);
        Assert.True(
            store.TryInvite(leader, store.FindForPlayer(leader.Address)!.Id, member.Address, out var operation, out _)
        );
        now = 61;
        Assert.False(store.TryRespond(member, operation!.Id, true, out _, out _));
        Assert.Null(store.FindForPlayer(member.Address));
    }

    [Fact]
    public void InviterLeavingInvalidatesTheirInvitationsWithoutDisbandingOtherMembers()
    {
        var store = new TeamStore(_players);
        var leader = Player(1);
        var inviter = Player(2);
        var target = Player(3);
        store.TryCreate(leader, "Builders", out _);
        Join(store, leader, inviter);
        store.TryInvite(inviter, store.FindForPlayer(inviter.Address)!.Id, target.Address, out var operation, out _);
        Assert.True(store.TryLeave(inviter, out _));
        Assert.False(store.TryRespond(target, operation!.Id, true, out _, out _));
        Assert.Single(store.Teams);
        Assert.Null(store.FindForPlayer(target.Address));
    }

    [Fact]
    public void DisconnectionCancelsConsentWithoutRemovingPersistentMembership()
    {
        var store = new TeamStore(_players);
        var leader = Player(1);
        var member = Player(2);
        var target = Player(3);
        store.TryCreate(leader, "Builders", out _);
        Join(store, leader, member);
        store.TryInvite(member, store.FindForPlayer(member.Address)!.Id, target.Address, out var operation, out _);
        store.CancelOperationsFor(member.Address);
        Assert.False(store.TryRespond(target, operation!.Id, true, out _, out _));
        Assert.NotNull(store.FindForPlayer(member.Address));
    }

    [Fact]
    public void RejectionAndCreatingAnotherTeamDoNotPermitAStaleJoin()
    {
        var store = new TeamStore(_players);
        var leader = Player(1);
        var target = Player(2);
        store.TryCreate(leader, "One", out _);
        store.TryInvite(leader, store.FindForPlayer(leader.Address)!.Id, target.Address, out var rejected, out _);
        Assert.True(store.TryRespond(target, rejected!.Id, false, out _, out _));
        Assert.Null(store.FindForPlayer(target.Address));
        store.TryInvite(leader, store.FindForPlayer(leader.Address)!.Id, target.Address, out var old, out _);
        Assert.True(store.TryCreate(target, "Two", out _));
        Assert.False(store.TryRespond(target, old!.Id, true, out _, out _));
        Assert.NotEqual(store.FindForPlayer(leader.Address)!.Id, store.FindForPlayer(target.Address)!.Id);
    }

    [Fact]
    public void BannedPlayersAndForgedRecordsCannotMutateTeams()
    {
        var store = new TeamStore(_players);
        var actor = Player(1);
        var forged = new WorldPlayerStore().GetOrCreate(actor.Address);
        Assert.Throws<InvalidOperationException>(() => store.TryCreate(forged, "Bad", out _));
        _players.Ban(actor.Address, "rule", null, DateTimeOffset.UtcNow);
        Assert.Throws<InvalidOperationException>(() => store.TryCreate(actor, "Bad", out _));
        Assert.Empty(store.Teams);
    }

    [Fact]
    public void PersistenceIsAtomicAndDoesNotPersistPendingRequests()
    {
        var store = new TeamStore(_players);
        var leader = Player(1);
        var member = Player(2);
        store.TryCreate(leader, "Builders", out _);
        var team = store.FindForPlayer(leader.Address)!;
        store.TryInvite(leader, team.Id, member.Address, out var pending, out _);
        var copy = new TeamStore(_players);
        copy.Load(store.Save());
        Assert.Equal(team.Id, copy.FindForPlayer(leader.Address)!.Id);
        Assert.False(copy.TryRespond(member, pending!.Id, true, out _, out _));
        var invalid = store.Save();
        invalid.SetValue(
            Guid.NewGuid().ToString("N"),
            invalid.GetValue<EntitySystem.TemplatesDatabase.ValuesDictionary>(team.Id.ToString("N"))
        );
        Assert.Throws<InvalidDataException>(() => copy.Load(invalid));
        Assert.Single(copy.Teams);
        Assert.Equal(team.Id, copy.FindForPlayer(leader.Address)!.Id);
    }

    [Fact]
    public void SnapshotsAreReadOnlyAndDetachedFromTheAuthoritativeCollection()
    {
        var store = new TeamStore(_players);
        var leader = Player(1);
        var member = Player(2);
        store.TryCreate(leader, "Builders", out _);
        var replica = new TeamStore(new WorldPlayerStore(), false);
        replica.Replace(store.Teams.Values);
        Join(store, leader, member);
        Assert.Single(Assert.Single(replica.Teams).Value.Members);
        Assert.Throws<InvalidOperationException>(() => replica.TryLeave(leader, out _));
        Assert.Throws<NotSupportedException>(() => ((IDictionary<Guid, Team>)store.Teams).Clear());
    }

    [Fact]
    public void InvalidLeadersDuplicateMembersAndUnknownPlayersAreRejected()
    {
        var store = new TeamStore(_players);
        var leader = Player(1);
        var unknown = Address(9);
        Assert.Throws<InvalidDataException>(() => new Team(
                Guid.NewGuid(),
                "Bad",
                leader.Address,
                [leader.Address, leader.Address]
            )
        );
        Assert.Throws<InvalidDataException>(() =>
            store.Replace([new Team(Guid.NewGuid(), "Bad", leader.Address, [unknown])])
        );
        Assert.Throws<InvalidDataException>(() =>
            store.Replace([new Team(Guid.NewGuid(), "Bad", leader.Address, [leader.Address, unknown])])
        );
        Assert.Empty(store.Teams);
    }

    private WorldPlayer Player(byte id) => _players.GetOrCreate(Address(id));

    private static PlayerAddress Address(byte id)
    {
        var bytes = new byte[32];
        bytes[0] = id;
        return new PlayerAddress(IdentityAddress.Encode(bytes));
    }

    private static void Join(TeamStore store, WorldPlayer leader, WorldPlayer joining)
    {
        Assert.True(
            store.TryInvite(leader, store.FindForPlayer(leader.Address)!.Id, joining.Address, out var operation, out _)
        );
        Assert.True(store.TryRespond(joining, operation!.Id, true, out _, out _));
    }
}
