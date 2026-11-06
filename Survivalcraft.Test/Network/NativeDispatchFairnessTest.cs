using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Reflection;

using Engine.Network;

using EntitySystem.Core;

using Game.Network;
using Game.Network.Handling;
using Game.Network.Transport;

namespace Survivalcraft.Test.Network;

public sealed class NativeDispatchFairnessTest
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ABackloggedConnectionCannotDrainItsWholeQueueBeforeAnotherConnectionGetsItsTurn(bool byteBudget)
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
        await using var sender = new NetworkHost();
        await using var firstReceiver = new NetworkHost();
        await using var secondReceiver = new NetworkHost();
        var peers = new List<NetworkConnection>();
        var firstValues = new List<int>();
        var secondValues = new List<int>();
        sender.ConnectionRequested += request => request.Accept();
        sender.Connected += peers.Add;
        firstReceiver.Received += (_, data) => firstValues.AddRange(
            ProtocolPacketCodec.Decode(data.Payload).Select(Value)
        );
        secondReceiver.Received += (_, data) => secondValues.AddRange(
            ProtocolPacketCodec.Decode(data.Payload).Select(Value)
        );

        int Value(IProtocolMessage message)
        {
            return message is EntityRemovedPackage removed
                ? removed.EntityId
                : BinaryPrimitives.ReadInt32LittleEndian(((ModEnvelopePackage)message).Payload);
        }

        void PumpUntil(Func<bool> ready)
        {
            var timer = Stopwatch.StartNew();
            while (!ready() && timer.Elapsed < TimeSpan.FromSeconds(5))
            {
                sender.DispatchEvents();
                firstReceiver.DispatchEvents();
                secondReceiver.DispatchEvents();
                Thread.Sleep(5);
            }

            Assert.True(ready(), "Loopback delivery did not converge.");
        }

        try
        {
            var firstConnect = firstReceiver.ConnectAsync(
                new IPEndPoint(IPAddress.Loopback, sender.LocalPort),
                ReadOnlyMemory<byte>.Empty,
                CancellationToken.None
            );
            PumpUntil(() => peers.Count == 1 && firstConnect.IsCompleted);
            await firstConnect;
            var secondConnect = secondReceiver.ConnectAsync(
                new IPEndPoint(IPAddress.Loopback, sender.LocalPort),
                ReadOnlyMemory<byte>.Empty,
                CancellationToken.None
            );
            PumpUntil(() => peers.Count == 2 && secondConnect.IsCompleted);
            await secondConnect;

            Client AddClient(byte id, NetworkConnection peer)
            {
                var client = new Client(peer, id, Guid.NewGuid(), Guid.NewGuid())
                {
                    State = ClientState.Playing, ConnectionPhase = ConnectionPhase.Live
                };
                node.AddClient(client);
                return client;
            }

            var first = AddClient(1, peers[0]);
            var second = AddClient(2, peers[1]);

            void Queue(Client client, int value)
            {
                IProtocolMessage message;
                if (byteBudget)
                {
                    var payload = new byte[ReferenceEquals(client, first) ? 32 * 1024 : 3 * 1024 * 1024];
                    BinaryPrimitives.WriteInt32LittleEndian(payload, value);
                    message = new ModEnvelopePackage("scnet.test", "fairness", payload);
                }
                else { message = new EntityRemovedPackage { EntityId = value }; }

                var fanout = new NetworkMessageFanout(
                    node.SendStatistics,
                    message.GetType(),
                    PackageTransportPolicy.Get(message).Channel
                );
                Assert.True(
                    node.QueueNetworkPackage(
                        client,
                        new OutboundPackage(message, PackageAudience.To(client), node.Runtime)
                            { Fanout = fanout }
                    )
                );
            }

            for (var value = 0; value < 200; value++) { Queue(first, value); }

            var flush = typeof(NetNode).GetMethod(
                "FlushPendingPackages",
                BindingFlags.Instance | BindingFlags.NonPublic
            )!;
            if (byteBudget) { Queue(second, 999); }

            flush.Invoke(node, null);
            PumpUntil(() => firstValues.Count == 64);
            Assert.Equal(byteBudget ? 137 : 136, node.PendingPackageCount);
            if (byteBudget)
            {
                Assert.Empty(secondValues);
                clock.Advance();
            }
            else { Queue(second, 999); }

            flush.Invoke(node, null);
            PumpUntil(() => secondValues.Count == 1);
            Assert.True(firstValues.Count < 200);
            Assert.Equal([999], secondValues);
            for (var turn = 0; node.PendingPackageCount > 0 && turn < 10; turn++)
            {
                clock.Advance();
                flush.Invoke(node, null);
            }

            PumpUntil(() => firstValues.Count == 200);
            Assert.Equal(Enumerable.Range(0, 200), firstValues);
            Assert.Equal(0, node.PendingPackageCount);
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
