using Engine.Core.Mathematics;
using Engine.Network;

using Game.Network;
using Game.Network.Diagnostics;
using Game.Network.Handling;
using Game.Network.Transport;
using Game.Subsystems;
using Game.World;

namespace Survivalcraft.Test.Network;

public sealed class PickablePackageChunkingTest
{
    [Theory]
    [InlineData(419, 0u)]
    [InlineData(419, uint.MaxValue)]
    [InlineData(461, 65536u)]
    [InlineData(507, 128u)]
    [InlineData(1428, 256u)]
    public void ActualEncodingSplitsWithoutLosingPositionsOrTick(int budget, uint tick)
    {
        var node = Node();
        var package = Package(80, tick);
        var chunks = node.SplitPickablePackage(package, PackageTransportPolicy.StateStream, budget);
        Assert.Equal(package.Positions, chunks.SelectMany(chunk => chunk.Positions).ToArray());
        Assert.All(
            chunks,
            chunk =>
            {
                Assert.Equal(tick, chunk.StateTick);
                Assert.NotEmpty(chunk.Positions);
                var data = ProtocolPacketCodec.Encode([chunk], CompressionPolicy.None);
                Assert.InRange(data.Length, 1, budget);
                var decoded = Assert.IsType<PickablePositionsPackage>(Assert.Single(ProtocolPacketCodec.Decode(data)));
                Assert.Equal(chunk.Positions, decoded.Positions);
            }
        );
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void EmptyAndSinglePositionKeepOneBoundedMessage(int count)
    {
        var package = Package(count, uint.MaxValue);
        var chunk = Assert.Single(Node().SplitPickablePackage(package, PackageTransportPolicy.StateStream, 419));
        Assert.Equal(package.Positions, chunk.Positions);
        Assert.Equal(package.StateTick, chunk.StateTick);
    }

    [Fact]
    public void APositionThatCannotFitFailsInsteadOfProducingOversizedFragments()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Node().SplitPickablePackage(Package(1, uint.MaxValue), PackageTransportPolicy.StateStream, 10)
        );
    }

    [Theory]
    [InlineData(NetworkSendResult.Accepted, 1)]
    [InlineData(NetworkSendResult.Backpressure, 0)]
    [InlineData(NetworkSendResult.Closed, 0)]
    [InlineData(NetworkSendResult.TooLarge, 0)]
    public void ActualFragmentSendMarksFanoutOnlyAfterEveryFragmentIsAccepted(
        NetworkSendResult secondResult,
        int recipients
    )
    {
        var node = Node();
        var statistics = new NetworkSendStatistics();
        var fanout = new NetworkMessageFanout(statistics, typeof(PickablePositionsPackage), NetworkChannel.StateStream);
        var package = Package(80, uint.MaxValue);
        var outbound = new OutboundPackage(package, PackageAudience.Global, node.Runtime) { Fanout = fanout };
        var client = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid());
        var calls = 0;
        var accepted = new List<PickablePosition>();
        var result = node.SendPickableFragments(
            outbound,
            client,
            PackageTransportPolicy.StateStream,
            419,
            data =>
            {
                Assert.InRange(data.Length, 1, 419);
                var decoded = Assert.IsType<PickablePositionsPackage>(Assert.Single(ProtocolPacketCodec.Decode(data)));
                calls++;
                if (calls == 2 && secondResult != NetworkSendResult.Accepted) { return secondResult; }

                accepted.AddRange(decoded.Positions);
                return NetworkSendResult.Accepted;
            }
        );
        Assert.Equal(secondResult, result);
        if (result == NetworkSendResult.Accepted) { Assert.Equal(package.Positions, accepted); }
        else { Assert.Equal(2, calls); }

        fanout.Advance(1, 0);
        Assert.Equal(recipients, Assert.Single(statistics.GetFanout()).RecipientDeliveries);
    }

    [Fact]
    public void SameTickFragmentsAndRetriesUpdateEachPickableAcrossTickWrap()
    {
        var items = new Dictionary<ushort, Pickable>();
        var package = Package(80, 0);
        foreach (var position in package.Positions)
        {
            items.Add(position.Id, new Pickable { Id = position.Id, LastStateTick = uint.MaxValue });
        }

        var chunks = Node().SplitPickablePackage(package, PackageTransportPolicy.StateStream, 419);
        foreach (var chunk in chunks.Concat(chunks))
        {
            foreach (var position in chunk.Positions)
            {
                SubsystemPickables.ApplyMotionSnapshot(items[position.Id], position, chunk.StateTick);
            }
        }

        Assert.All(
            package.Positions,
            position =>
            {
                Assert.Equal(0u, items[position.Id].LastStateTick);
                Assert.Equal(position.Position, items[position.Id].NetworkPosition);
            }
        );
    }

    private static NetNode Node() => new(NetNodeOptions.Default, true, PackageHandlerCatalog.Empty, NodeRole.Inactive);

    private static PickablePositionsPackage Package(int count, uint tick)
    {
        var ids = new ushort[] { 0, 127, 128, 255, 256, ushort.MaxValue };
        return new PickablePositionsPackage
        {
            StateTick = tick,
            Positions = Enumerable.Range(0, count).Select(index => new PickablePosition(
                    index < ids.Length ? ids[index] : (ushort)(1000 + index),
                    new Vector3(index + .25f, -index, index * 3)
                )
            ).ToArray()
        };
    }
}
