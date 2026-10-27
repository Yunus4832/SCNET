using Engine.Core.Mathematics;
using Engine.Network;

using Game.Network;
using Game.Network.Transport;
using Game.World;

namespace Survivalcraft.Test.Network;

public sealed class ListStateTransportTest
{
    [Fact]
    public void PickableSnapshotRoundTripsItsTickAndPositions()
    {
        var package = PickablePackageBuilder.Capture([new Pickable { Id = 7, Position = new Vector3(1, 2, 3) }]) with
        {
            StateTick = uint.MaxValue
        };
        var writer = ProtocolMessageCodec.Encode(package);
        var reader = writer;
        var clone = Assert.IsType<PickablePositionsPackage>(ProtocolMessageCodec.Decode(reader));
        Assert.Equal(package.StateTick, clone.StateTick);
        Assert.Equal(package.Type, clone.Type);
        Assert.Equal(package.Positions[0].Id, Assert.Single(clone.Positions).Id);
        Assert.Equal(package.Positions[0].Position, clone.Positions[0].Position);
    }

    [Fact]
    public void FullPickableChunkFitsMinimumMtuWithoutCompression()
    {
        var items = Enumerable.Range(1, PickablePackage.MaxPositionsPerSnapshot)
            .Select(id => new Pickable { Id = (ushort)id, Position = new Vector3(id, -id, id * 3) }).ToList();
        var package = PickablePackageBuilder.Capture(items) with { StateTick = uint.MaxValue };
        var writer = ProtocolPacketCodec.Encode([package], CompressionPolicy.None);
        var datagram = CommonLib.GetWriter(writer);
        Assert.InRange(datagram.Length, 1, 507);
        Assert.Equal(DeliveryMode.Unreliable, PackageTransportPolicy.Get(package).Delivery);
        Assert.Equal(NetworkChannel.StateStream, PackageTransportPolicy.Get(package).Channel);
    }

    [Fact]
    public void LargeAtomicPlayerListUsesFragmentableCoalescedReliableTransport()
    {
        var package = new OnlineCharacterStatePackage(
            Enumerable.Range(1, 100)
                .Select(id => new OnlineCharacterState(Guid.NewGuid(), new Vector3(id, -id, id * 3), 1f, false))
        );
        var writer = ProtocolPacketCodec.Encode([package], CompressionPolicy.None);
        var datagram = CommonLib.GetWriter(writer);
        Assert.True(datagram.Length > 507);
        var transport = PackageTransportPolicy.Get(package);
        Assert.Equal(DeliveryMode.ReliableOrdered, transport.Delivery);
        Assert.True(transport.Coalesce);
    }
}
