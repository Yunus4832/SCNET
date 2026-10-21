using Engine.Core.Mathematics;

using Game.Network;

using TerritoryStoneMod;

namespace Survivalcraft.Test.Modding;

public class TerritoryMessagesTest
{
    [Fact]
    public void SettingsRequireTheAuthenticatedOwner()
    {
        var owner = Guid.NewGuid();
        var sender = new Client(null, 1, Guid.NewGuid(), owner, null);
        var stranger = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid(), null);
        Assert.True(TerritoryMessages.CanChangeSettings(sender, owner));
        Assert.False(TerritoryMessages.CanChangeSettings(stranger, owner));
        Assert.False(TerritoryMessages.CanChangeSettings(null, owner));
        Assert.False(TerritoryMessages.CanChangeSettings(sender, Guid.Empty));
    }

    [Fact]
    public void SnapshotRoundTripReplacesStaleClientStateIncludingEmptySnapshots()
    {
        var territory = new Territory(
            Guid.NewGuid(),
            new Point3(48, 64, -48),
            new TerritorySettings(9, true, false, true, false, false)
        );
        using var writer = new ProtocolWriter();
        TerritoryMessages.WriteSnapshot(writer, new[] { territory });
        using var reader = new ProtocolReader(writer.Data());
        var store = new TerritoryStore();
        Assert.True(store.TryCreate(Guid.NewGuid(), new Point3(0, 64, 0)));
        store.ApplySnapshot(TerritoryMessages.ReadSnapshot(reader));
        var restored = Assert.Single(store.Territories);
        Assert.Equal(territory.Owner, restored.Owner);
        Assert.Equal(territory.StonePoint, restored.StonePoint);
        Assert.Equal(territory.Settings, restored.Settings);

        store.ApplySnapshot([]);
        Assert.Empty(store.Territories);
    }

    [Fact]
    public void InvalidSnapshotDoesNotPartiallyReplaceClientState()
    {
        var store = new TerritoryStore();
        var owner = Guid.NewGuid();
        Assert.True(store.TryCreate(owner, new Point3(0, 64, 0)));
        Assert.Throws<InvalidDataException>(() => store.ApplySnapshot(
                [
                    new Territory(Guid.NewGuid(), new Point3(128, 64, 128)),
                    new Territory(Guid.NewGuid(), new Point3(128, 65, 128))
                ]
            )
        );
        Assert.Equal(owner, Assert.Single(store.Territories).Owner);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1025)]
    public void SnapshotRejectsInvalidCountsBeforeReadingEntries(int count)
    {
        using var writer = new ProtocolWriter();
        writer.Write(count);
        using var reader = new ProtocolReader(writer.Data());
        Assert.Throws<InvalidDataException>(() => TerritoryMessages.ReadSnapshot(reader));
    }

    [Fact]
    public void SnapshotRejectsInvalidRadiusBeforeReplacingClientState()
    {
        using var writer = new ProtocolWriter();
        writer.Write(1);
        writer.Write(Guid.NewGuid());
        writer.Write(new Point3(0, 64, 0));
        TerritoryMessages.WriteSettings(writer, new TerritorySettings(Size: 0));
        using var reader = new ProtocolReader(writer.Data());
        Assert.Throws<InvalidDataException>(() => TerritoryMessages.ReadSnapshot(reader));
    }

    [Fact]
    public void AuthoritativeUpdatesAndRemovalsDoNotLeaveOldBoundaryRecords()
    {
        var store = new TerritoryStore();
        var owner = Guid.NewGuid();
        Assert.True(store.TryCreate(owner, new Point3(0, 64, 0)));
        store.ApplyUpdate(new Territory(owner, new Point3(128, 64, 128)));
        Assert.Null(store.Find(0, 0));
        Assert.Equal(owner, store.Find(128, 128)!.Owner);
        Assert.True(store.Remove(owner));
        Assert.Empty(store.Territories);
    }
}
