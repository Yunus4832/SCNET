using Game.Network;
using Game.Network.Transport;
using Game.Protocol.Characters;
using Game.Protocol.Connection;
using Game.World;

namespace Survivalcraft.Core.Test.Network;

public sealed class ConnectionLifetimeTest
{
    [Fact]
    public void AuthorityAllocatesExactlyOnceAndNewRecordGetsLaterGeneration()
    {
        var world = new WorldRuntimeContext(RuntimeSide.Authority, false);
        using var roster = new WorldConnections(world);
        var first = new WorldConnection(1);
        roster.BeginConnectionLifetime(first);
        var epoch = first.ConnectionEpoch;
        var generation = first.ConnectionGeneration;
        roster.Register(first);
        Assert.Throws<InvalidOperationException>(() => roster.BeginConnectionLifetime(new WorldConnection(1)));
        roster.BeginConnectionLifetime(first);
        Assert.NotEqual(Guid.Empty, epoch);
        Assert.Equal(epoch, first.ConnectionEpoch);
        Assert.Equal(generation, first.ConnectionGeneration);
        roster.Remove(first);
        Assert.Throws<InvalidOperationException>(() => roster.BeginConnectionLifetime(first));
        var second = new WorldConnection(1);
        roster.Register(second);
        Assert.True(second.ConnectionGeneration > generation);
        Assert.NotEqual(epoch, second.ConnectionEpoch);
    }

    [Fact]
    public void TombstoneRejectsStaleResurrectionAndAudienceDoesNotRetargetNewGeneration()
    {
        var world = new WorldRuntimeContext(RuntimeSide.Client, false);
        using var roster = new WorldConnections(world);
        var snapshot = new CharacterControllerSnapshot(1, Guid.NewGuid(), null, ClientState.Playing, Guid.NewGuid(), 1);
        Assert.True(roster.TryInstallController(snapshot, out var first));
        var audience = PackageAudience.To(first!);
        Assert.True(roster.RemoveLifetime(1, snapshot.ConnectionEpoch, 1, out _));
        Assert.False(roster.TryInstallController(snapshot, out _));
        var replacement = snapshot with { ConnectionEpoch = Guid.NewGuid(), ConnectionGeneration = 2 };
        Assert.True(roster.TryInstallController(replacement, out var second));
        Assert.NotSame(first, second);
        Assert.False(audience.Includes(second!));
        Assert.False(roster.RemoveLifetime(1, snapshot.ConnectionEpoch, 1, out _));
        Assert.False(roster.TryInstallController(snapshot, out _));
    }

    [Fact]
    public void SameGenerationOnlyInitialEmptyCharacterCanAcquireIdentity()
    {
        var world = new WorldRuntimeContext(RuntimeSide.Client, false);
        using var roster = new WorldConnections(world);
        var snapshot = new CharacterControllerSnapshot(1, Guid.Empty, null, ClientState.Connected, Guid.NewGuid(), 1);
        Assert.True(roster.TryInstallController(snapshot, out var record));
        var bound = snapshot with { CharacterId = Guid.NewGuid() };
        Assert.True(roster.TryInstallController(bound, out var same));
        Assert.Same(record, same);
        Assert.False(roster.TryInstallController(bound with { CharacterId = Guid.NewGuid() }, out _));
        Assert.False(roster.TryInstallController(bound with { ConnectionEpoch = Guid.NewGuid() }, out _));
        Assert.False(roster.TryInstallController(snapshot, out _));
    }
}
