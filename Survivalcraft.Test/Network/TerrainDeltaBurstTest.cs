using Engine.Core.Mathematics;

using EntitySystem.Core;

using Game.Network;
using Game.Network.Transport;
using Game.World;

namespace Survivalcraft.Test.Network;

public sealed class TerrainDeltaBurstTest
{
    [Fact]
    public void BatchingKeepsByteCapacityReservedAndDoesNotMixRecipients()
    {
        using var project = new Project();
        var runtime = new WorldRuntimeContext(RuntimeSide.Authority, false);
        var first = new Client(Guid.NewGuid());
        var second = new Client(Guid.NewGuid());

        OutboundPackage Message(Client recipient)
        {
            return new(
                new TerrainCellDeltaPackage
                {
                    CellDeltas = [new TerrainCellDelta(new Point3(1, 65, 1), 4, 1, 2)]
                },
                PackageAudience.To(recipient),
                runtime
            );
        }

        var charge = PendingLogicalMessages.Charge(Message(first).Package);
        var limited = new PendingLogicalMessages(capacityBytes: charge);
        Assert.True(limited.TryEnqueue(Message(first)));
        Assert.False(limited.TryEnqueue(Message(first)));
        Assert.Equal(charge, limited.PendingBytes);
        Assert.Single(Assert.IsType<TerrainCellDeltaPackage>(Assert.Single(limited).Package).CellDeltas);
        var queue = new PendingLogicalMessages();
        Assert.True(queue.TryEnqueue(Message(first)));
        Assert.True(queue.TryEnqueue(Message(second)));
        Assert.Equal(2, queue.Count);
    }

    [Fact]
    public void BatchingCannotMoveDeltasAcrossAnotherControlPublication()
    {
        var runtime = new WorldRuntimeContext(RuntimeSide.Authority, false);
        var queue = new PendingLogicalMessages();

        void Add(int value)
        {
            queue.TryEnqueue(
                new OutboundPackage(
                    new TerrainCellDeltaPackage
                    {
                        CellDeltas = [new TerrainCellDelta(new Point3(1, 65, 1), value, value + 1, value + 2)]
                    },
                    PackageAudience.Global,
                    runtime
                )
            );
        }

        Add(1);
        queue.TryEnqueue(new OutboundPackage(new SubsystemTimePackage(9, 0), PackageAudience.Global, runtime));
        Add(2);
        Assert.Equal(3, queue.Count);
        Assert.Equal(1, Assert.Single(Assert.IsType<TerrainCellDeltaPackage>(queue[0].Package).CellDeltas).Value);
        Assert.IsType<SubsystemTimePackage>(queue[1].Package);
        Assert.Equal(2, Assert.Single(Assert.IsType<TerrainCellDeltaPackage>(queue[2].Package).CellDeltas).Value);
    }

    [Fact]
    public void HealthyTerrainMutationBurstFitsPublicationCapacityWithoutLosingDeltaOrder()
    {
        var runtime = new WorldRuntimeContext(RuntimeSide.Authority, false);
        using var project = new Project();
        var recipient = new Client(Guid.NewGuid());
        var audience = PackageAudience.To(recipient);
        var queue = new PendingLogicalMessages();
        for (var i = 0; i < 4096; i++)
        {
            Assert.True(
                queue.TryEnqueue(
                    new OutboundPackage(
                        new TerrainCellDeltaPackage
                            { CellDeltas = [new TerrainCellDelta(new Point3(1, 65, 1), i, i + 1, i + 2)] },
                        audience,
                        runtime
                    )
                )
            );
        }

        Assert.Equal(64, queue.Count);
        var deltas = queue.SelectMany(item => Assert.IsType<TerrainCellDeltaPackage>(item.Package).CellDeltas)
            .ToArray();
        Assert.Equal(4096, deltas.Length);
        for (var i = 0; i < deltas.Length; i++)
        {
            Assert.Equal(i, deltas[i].Value);
            Assert.Equal(i + 1, deltas[i].BaseContentVersion);
            Assert.Equal(i + 2, deltas[i].ResultContentVersion);
        }

        queue.Discard(runtime);
        Assert.Equal(0, queue.PendingBytes);
    }
}
