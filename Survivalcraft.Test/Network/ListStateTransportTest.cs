using Engine.Core;

using Game;
using Game.Network;
using Game.Network.Packages;
using Game.Network.Serialization;

using LiteNetLib;

namespace Survivalcraft.Test.Network;

public sealed class ListStateTransportTest
{
    [Fact]
    public void PickableSnapshotRoundTripsItsTickAndPositions()
    {
        var package = new PickablePackage([new Pickable { Id = 7, Position = new Vector3(1, 2, 3) }])
        {
            StateTick = uint.MaxValue
        };
        using var writer = new PackageStreamWriter();
        package.WriteData(writer);
        using var reader = new PackageStreamReader(writer.Data());
        var clone = new PickablePackage();
        clone.ReadData(reader);
        Assert.Equal(package.StateTick, clone.StateTick);
        Assert.Equal(package.Type, clone.Type);
        Assert.Equal(package.Pickables[0].Id, Assert.Single(clone.Pickables).Id);
        Assert.Equal(package.Pickables[0].Position, clone.Pickables[0].Position);
    }

    [Fact]
    public void FullPickableChunkFitsMinimumMtuWithoutCompression()
    {
        var items = Enumerable.Range(1, PickablePackage.MaxPositionsPerSnapshot)
            .Select(id => new Pickable { Id = (ushort)id, Position = new Vector3(id, -id, id * 3) }).ToList();
        var package = new PickablePackage(items) { StateTick = uint.MaxValue };
        using var writer = new PackageStreamWriter();
        writer.Write((byte)0);
        writer.Write(package.ID);
        package.WriteData(writer);
        var datagram = CommonLib.GetWriter(writer, out _, CommonLib.CompressionPolicy.None);
        Assert.InRange(datagram.Length, 1, 507);
        Assert.Equal(DeliveryMethod.Unreliable, PackageTransportPolicy.Get(package).DeliveryMethod);
        Assert.Equal(NetworkChannel.StateStream, PackageTransportPolicy.Get(package).Channel);
    }

    [Fact]
    public void LargeAtomicPlayerListUsesFragmentableCoalescedReliableTransport()
    {
        var package = new OnlinePlayerStatePackage(Enumerable.Range(1, 100)
            .Select(id => new OnlinePlayerState(Guid.NewGuid(), new Vector3(id, -id, id * 3), 1f, false)));
        using var writer = new PackageStreamWriter();
        writer.Write((byte)0);
        writer.Write(package.ID);
        package.WriteData(writer);
        var datagram = CommonLib.GetWriter(writer, out _, CommonLib.CompressionPolicy.None);
        Assert.True(datagram.Length > 507);
        var transport = PackageTransportPolicy.Get(package);
        Assert.Equal(DeliveryMethod.ReliableOrdered, transport.DeliveryMethod);
        Assert.True(transport.Coalesce);
    }
}
