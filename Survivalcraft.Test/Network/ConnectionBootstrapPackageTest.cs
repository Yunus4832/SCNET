using EntitySystem.TemplatesDatabase;

using Game.Network.Enums;
using Game.Network.Packages;
using Game.Network.Serialization;

namespace Survivalcraft.Test.Network;

public class ConnectionBootstrapPackageTest
{
    [Fact]
    public void BootstrapRoundTripsAsSinglePayload()
    {
        var epoch = Guid.NewGuid();
        var package = new BootstrapPackage
        {
            Epoch = epoch,
            ClientList = new ClientPackage([]),
            TextureData = [1, 2, 3],
            ProjectData = [4, 5, 6]
        };

        var clone = RoundTrip(package, new BootstrapPackage());

        Assert.Equal(epoch, clone.Epoch);
        Assert.Equal(ClientPackage.EventType.SyncList, clone.ClientList.PackageEventType);
        Assert.Equal(package.TextureData, clone.TextureData);
        Assert.Equal(package.ProjectData, clone.ProjectData);
    }

    [Fact]
    public void ConnectionPhaseAckRoundTripsEpochAndPhase()
    {
        var epoch = Guid.NewGuid();
        var clone = RoundTrip(
            new ConnectionPhaseAckPackage(epoch, ConnectionPhase.BootstrapApplied),
            new ConnectionPhaseAckPackage());

        Assert.Equal(epoch, clone.Epoch);
        Assert.Equal(ConnectionPhase.BootstrapApplied, clone.Phase);
    }

    [Fact]
    public void InitialWorldSnapshotKeepsPlayerDataBeforeEntityPayload()
    {
        var playerGuid = Guid.NewGuid();
        var player = new ValuesDictionary();
        player.SetValue("PlayerGUID", playerGuid);
        var package = new InitialWorldSnapshotPackage
        {
            Epoch = Guid.NewGuid(),
            ClientList = new ClientPackage([]),
            EntityData = [7, 8, 9]
        };
        package.Players.Add(player);

        var clone = RoundTrip(package, new InitialWorldSnapshotPackage());

        Assert.Equal(playerGuid, clone.Players.Single().GetValue<Guid>("PlayerGUID"));
        Assert.Equal(package.EntityData, clone.EntityData);
    }

    private static T RoundTrip<T>(T package, T clone) where T : IPackage
    {
        using var writer = new PackageStreamWriter();
        package.WriteData(writer);
        using var reader = new PackageStreamReader(writer.Data());
        clone.ReadData(reader);
        return clone;
    }
}
