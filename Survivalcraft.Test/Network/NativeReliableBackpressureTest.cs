using System.Diagnostics;
using System.Net;
using System.Reflection;

using Engine.Network;
using Engine.Network.Test.Security;

using EntitySystem.Core;

using Game.Network;
using Game.Network.Handling;
using Game.Network.Transport;

namespace Survivalcraft.Test.Network;

public sealed class NativeReliableBackpressureTest
{
    [Theory]
    [InlineData(512, false)]
    [InlineData(128, false)]
    [InlineData(512, true)]
    [InlineData(128, true)]
    public async Task ATemporaryTransportStallRetainsTheUnsentSuffixAndRecoversInOrderWithoutDuplicates(
        int capacity,
        bool sharedEmission
    )
    {
        using var project = new Project();
        var clock = new ManualTimeProvider();
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Authority,
            clock
        );
        node.Runtime.BindProject(project);
        await using var sender = new NetworkHost(
            options: new NetworkHostOptions
            {
                MaxPendingReliableBytes = capacity, DisconnectTimeout = TimeSpan.FromSeconds(15)
            }
        );
        await using var proxy = new DatagramSecurityProxy(new IPEndPoint(IPAddress.Loopback, sender.LocalPort))
        {
            duplicatePercent = 0
        };
        await using var receiver = new NetworkHost(
            options: new NetworkHostOptions { DisconnectTimeout = TimeSpan.FromSeconds(15) }
        );
        NetworkConnection? peer = null;
        sender.ConnectionRequested += request => request.Accept();
        sender.Connected += connection => peer = connection;
        var received = new List<int>();
        receiver.Received += (_, data) => received.AddRange(
            ProtocolPacketCodec.Decode(data.Payload).Cast<EntityRemovedPackage>().Select(message => message.EntityId)
        );

        void PumpUntil(Func<bool> condition)
        {
            var timer = Stopwatch.StartNew();
            while (!condition() && timer.Elapsed < TimeSpan.FromSeconds(10))
            {
                sender.DispatchEvents();
                receiver.DispatchEvents();
                Thread.Sleep(5);
            }

            Assert.True(condition(), "Reliable stream failed to converge after restoring delivery.");
        }

        try
        {
            var connecting = receiver.ConnectAsync(proxy.Endpoint, ReadOnlyMemory<byte>.Empty, CancellationToken.None);
            PumpUntil(() => peer != null && connecting.IsCompleted);
            await connecting;
            var client = new Client(peer, 1, Guid.NewGuid(), Guid.NewGuid())
            {
                State = ClientState.Playing, ConnectionPhase = ConnectionPhase.Live
            };
            node.AddClient(client);

            void Queue(int id, NetworkMessageFanout? emission = null)
            {
                var fanout = emission ??
                             new NetworkMessageFanout(
                                 node.SendStatistics,
                                 typeof(EntityRemovedPackage),
                                 NetworkChannel.Bulk
                             );
                Assert.True(
                    node.QueueNetworkPackage(
                        client,
                        new OutboundPackage(
                            new EntityRemovedPackage { EntityId = id },
                            PackageAudience.To(client),
                            node.Runtime
                        ) { Fanout = fanout }
                    )
                );
            }

            var flush = typeof(NetNode).GetMethod(
                "FlushPendingPackages",
                BindingFlags.Instance | BindingFlags.NonPublic
            )!;
            Queue(-1);
            flush.Invoke(node, null);
            PumpUntil(() => received.Count == 1 && peer!.PendingReliableBytes == 0);
            Volatile.Write(ref proxy.dropPercent, 100);
            var emission = sharedEmission
                ? new NetworkMessageFanout(node.SendStatistics, typeof(EntityRemovedPackage), NetworkChannel.Bulk)
                : null;
            emission?.Advance(1, 1000);
            for (var id = 0; id < 1000; id++) { Queue(id, emission); }

            var stalled = false;
            for (var turn = 0; turn < 40; turn++)
            {
                clock.Advance();
                var before = node.PendingPackageCount;
                flush.Invoke(node, null);
                if (node.PendingPackageCount == before && peer!.PendingReliableBytes > 0)
                {
                    stalled = true;
                    break;
                }
            }

            Assert.True(stalled);
            Assert.True(client.IsConnected);
            Assert.Equal([-1], received);
            Queue(1000);
            Volatile.Write(ref proxy.dropPercent, 0);
            PumpUntil(() =>
                {
                    clock.Advance();
                    flush.Invoke(node, null);
                    return node.PendingPackageCount == 0 && received.Count == 1002 && peer!.PendingReliableBytes == 0;
                }
            );
            Assert.Equal(Enumerable.Range(-1, 1002), received);
            Assert.Equal(0, node.PendingNetworkPackageBytes);
            var statistics = Assert.Single(node.SendStatistics.GetFanout());
            Assert.Equal(sharedEmission ? 3 : 1002, statistics.Messages);
            Assert.Equal(sharedEmission ? 3 : 1002, statistics.RecipientDeliveries);
        }
        finally { node.StopImmediate(); }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _timestamp;

        internal void Advance() => _timestamp += TimeSpan.FromMilliseconds(200).Ticks;
    }
}
