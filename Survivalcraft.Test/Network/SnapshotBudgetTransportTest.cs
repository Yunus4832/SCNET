using System.Diagnostics;
using System.Net;
using System.Reflection;

using Engine.Core;
using Engine.Network;

using Game.Network;
using Game.Network.Transport;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Network;

[Collection(ConfigFileCollection.Name)]
public sealed class SnapshotBudgetTransportTest
{
    [Fact]
    public async Task OversizedSnapshotBatchDefersWithoutBlockingLifecycleAndThenDrains()
    {
        var node = new NetNode();
        await using var sender = new NetworkHost();
        await using var receiver = new NetworkHost();
        var previousClients = CommonLib.Net.Clients;
        var previousMode = CommonLib.WorkType;
        var receivedPlayers = new HashSet<byte>();
        var receivedRemovals = new List<int>();
        var receivedDatagrams = 0;
        NetworkConnection? senderPeer = null;
        sender.ConnectionRequested += request => request.Accept();
        sender.Connected += peer => senderPeer = peer;
        receiver.Received += (_, data) =>
        {
            receivedDatagrams++;
            using var reader = CommonLib.GetReader(data.Payload);
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                Assert.Equal(0x88, reader.ReadByte());
                var id = reader.ReadByte();
                if (id == (byte)PackageType.ComponentPlayer)
                {
                    var package = Assert.IsType<PlayerStatePackage>(ProtocolMessageCodec.Read(id, reader));
                    receivedPlayers.Add(package.FromPlayerId);
                }
                else
                {
                    Assert.Equal((byte)PackageType.Entity, id);
                    var package = (EntityPackage)ProtocolMessageCodec.Read((byte)PackageType.Entity, reader);
                    Assert.Equal(EntityPackage.EventType.Remove, package.Type);
                    receivedRemovals.Add(package.EntityId);
                }
            }
        };

        void PumpUntil(Func<bool> condition)
        {
            var timer = Stopwatch.StartNew();
            while (!condition() && timer.Elapsed < TimeSpan.FromSeconds(5))
            {
                sender.DispatchEvents();
                receiver.DispatchEvents();
                Thread.Sleep(5);
            }
            Assert.True(condition(), "Loopback transport did not reach the expected state within five seconds.");
        }

        try
        {
            var connecting = receiver.ConnectAsync(new IPEndPoint(IPAddress.Loopback, sender.LocalPort),
                ReadOnlyMemory<byte>.Empty, CancellationToken.None);
            PumpUntil(() => senderPeer is not null && connecting.IsCompleted);
            await connecting;
            var observer = new Client(senderPeer, 1, Guid.NewGuid(), Guid.NewGuid(), null)
            {
                State = ClientState.Playing,
                ConnectionPhase = ConnectionPhase.Live
            };
            node.Clients.Add(observer.ID, observer);
            node.Self = node.Server = new Client(null, 0, Guid.NewGuid(), Guid.NewGuid(), null);
            node.CurrentStage = NetNode.Stage.Connected;
            CommonLib.WorkType = WorkType.Server;
            CommonLib.Net.Clients = new Dictionary<int, Client>();
            var expectedPlayers = new HashSet<byte>();
            for (byte id = 10; id < 42; id++)
            {
                var identity = new Client(null, id, Guid.NewGuid(), Guid.NewGuid(), null);
                CommonLib.Net.Clients.Add(id, identity);
                node.QueuePackage(new PlayerStatePackage
                {
                    FromPlayerId = id,

                    PackageChangeFlag = (ComponentPlayerPackage.ChangFlag)byte.MaxValue,
                    Position = new Vector3(id, 70f, -id),
                    Rotation = Quaternion.Identity,
                    Velocity = new Vector3(id, 1f, 2f),
                    LookAngles = new Vector2(0.1f, 0.2f),
                    ChildLookAngles = new Vector2(0.3f, 0.4f)
                }, PackageAudience.To(observer));
                expectedPlayers.Add(id);
            }
            node.QueuePackage(new EntityPackage(4242), PackageAudience.To(observer));
            var flush = typeof(NetNode).GetMethod("FlushPendingPackages", BindingFlags.Instance | BindingFlags.NonPublic)!;
            flush.Invoke(node, null);
            Assert.InRange(node.PendingPackageCount, 1, 31);
            PumpUntil(() => receivedRemovals.Count == 1 && receivedPlayers.Count > 0);
            Assert.Equal(4242, Assert.Single(receivedRemovals));
            Assert.True(receivedPlayers.Count < expectedPlayers.Count);

            for (var attempt = 0; node.PendingPackageCount > 0 && attempt < 32; attempt++)
            {
                Thread.Sleep(60);
                flush.Invoke(node, null);
                sender.DispatchEvents();
                receiver.DispatchEvents();
            }
            Assert.Equal(0, node.PendingPackageCount);
            PumpUntil(() => receivedPlayers.SetEquals(expectedPlayers));
            Assert.True(receivedDatagrams > 2);
            var fanout = node.SendStatistics.GetFanout()
                .Single(statistic => statistic.PackageType == typeof(PlayerStatePackage));
            Assert.Equal(expectedPlayers.Count, fanout.Messages);
            Assert.Equal(expectedPlayers.Count, fanout.RecipientDeliveries);
            Assert.Equal(1, fanout.MaximumFanout);
        }
        finally
        {
            CommonLib.Net.Clients = previousClients;
            CommonLib.WorkType = previousMode;
        }
    }
}
