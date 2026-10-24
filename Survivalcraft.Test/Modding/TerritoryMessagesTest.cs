using Engine.Core.Mathematics;

using EntitySystem.Core;

using Game.Network;
using Game.Protocol.Identity;
using Game.Protocol.Serialization;
using Game.Subsystems;

using Survivalcraft.Test.Subsystems;

using TerritoryStoneMod;

using static Survivalcraft.Test.Modding.TerritoryTestIdentity;

namespace Survivalcraft.Test.Modding;

[Collection(ConfigFileCollection.Name)]
public class TerritoryMessagesTest : IDisposable
{
    private readonly Dictionary<int, Client> _previousClients = CommonLib.Net.Clients;

    public TerritoryMessagesTest()
    {
        CommonLib.Net.Clients = [];
    }

    public void Dispose()
    {
        CommonLib.Net.Clients = _previousClients;
    }

    [Fact]
    public void SettingsRequireTheAuthenticatedOwner()
    {
        var owner = Address();
        using var project = new IdentityProject();
        project.Players.Store.GetOrCreate(owner);
        var other = Address();
        project.Players.Store.GetOrCreate(other);
        var sender = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid(), project) { PlayerAddress = owner };
        var stranger = new Client(null, 2, Guid.NewGuid(), sender.CharacterId, project) { PlayerAddress = other };
        CommonLib.Net.Clients = new Dictionary<int, Client> { [1] = sender, [2] = stranger };
        Assert.True(TerritoryMessages.CanChangeSettings(sender, owner));
        Assert.False(TerritoryMessages.CanChangeSettings(stranger, owner));
        var copiedMetadata = new Client(null, sender.ID, Guid.NewGuid(), sender.CharacterId, project)
            { PlayerAddress = owner };
        Assert.False(TerritoryMessages.CanChangeSettings(copiedMetadata, owner));
        Assert.False(TerritoryMessages.CanChangeSettings(null, owner));
        Assert.False(TerritoryMessages.CanChangeSettings(sender, default));
        project.Players.Store.Ban(owner, "rule", null, DateTimeOffset.UtcNow);
        Assert.False(TerritoryMessages.CanChangeSettings(sender, owner));
    }

    [Fact]
    public void SnapshotRoundTripReplacesStaleClientStateIncludingEmptySnapshots()
    {
        var territory = new Territory(
            Address(),
            new Point3(48, 64, -48),
            new TerritorySettings(9, true, false, true, false, false)
        );
        using var writer = new ProtocolWriter();
        TerritoryMessages.WriteSnapshot(writer, new[] { territory });
        using var reader = new ProtocolReader(writer.Data());
        var store = new TerritoryStore();
        Assert.True(store.TryCreate(Address(), new Point3(0, 64, 0)));
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
        var owner = Address();
        Assert.True(store.TryCreate(owner, new Point3(0, 64, 0)));
        Assert.Throws<InvalidDataException>(() => store.ApplySnapshot(
                [
                    new Territory(Address(), new Point3(128, 64, 128)),
                    new Territory(Address(), new Point3(128, 65, 128))
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
        PlayerAddressCodec.Write(writer, Address());
        writer.Write(new Point3(0, 64, 0));
        TerritoryMessages.WriteSettings(writer, new TerritorySettings(Size: 0));
        using var reader = new ProtocolReader(writer.Data());
        Assert.Throws<InvalidDataException>(() => TerritoryMessages.ReadSnapshot(reader));
    }

    [Fact]
    public void AuthoritativeUpdatesAndRemovalsDoNotLeaveOldBoundaryRecords()
    {
        var store = new TerritoryStore();
        var owner = Address();
        Assert.True(store.TryCreate(owner, new Point3(0, 64, 0)));
        store.ApplyUpdate(new Territory(owner, new Point3(128, 64, 128)));
        Assert.Null(store.Find(0, 0));
        Assert.Equal(owner, store.Find(128, 128)!.Owner);
        Assert.True(store.Remove(owner));
        Assert.Empty(store.Territories);
    }

    private sealed class IdentityProject : Project
    {
        public SubsystemPlayers Players { get; } = new();

        public SubsystemCharacters Characters { get; } = new();

        public SubsystemPositionMarks Marks { get; } = new();

        public IdentityProject()
        {
            subsystems.Add(Players);
            subsystems.Add(Characters);
            subsystems.Add(Marks);
            WorldSubsystemTestSupport.Attach(this, Players);
            WorldSubsystemTestSupport.Attach(this, Characters);
            WorldSubsystemTestSupport.Attach(this, Marks);
        }
    }
}
